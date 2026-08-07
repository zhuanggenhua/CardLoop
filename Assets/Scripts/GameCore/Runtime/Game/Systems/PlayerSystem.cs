using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using YokiFrame;

namespace GameCore
{
    [Serializable]
    public class PlayerDataBlock : DataBlock
    {
        public CharacterActorRuntimeStateData primaryCharacterData;
        public PersistableReference<CharacterBase>[] currentControlledCharacters;
    }

    /// <summary>
    /// 玩家实体与当前控制目标的正式真相源。
    /// 主角色只承担玩家长期存档归属；当前控制对象可以是队伍中的任意 CharacterActor。
    /// </summary>
    public class PlayerSystem : AGameSystem, IDataBlockHandler<PlayerDataBlock>
    {
        private static readonly Type[] SystemStartupDependencies = { typeof(PersistenceSystem) };

        [Header("Scene References")]
        [FormerlySerializedAs("m_playerInstance")]
        [SerializeField] private CharacterActor m_primaryPlayerCharacter = null;

        private readonly UnityEvent<CharacterBase> m_currentControlledCharacterChanged = new();
        private IPlayerInputTarget m_currentInputTarget;
        private PlayerControlGroup m_currentControlGroup;
        private CharacterBase m_boundControlledCharacter;
        private readonly List<CharacterBase> m_boundControlledCharacters = new();
        private PersistableReference<CharacterBase>[] m_pendingControlledCharacterReferences = Array.Empty<PersistableReference<CharacterBase>>();
        private bool m_hasPendingControlledCharacterRestore = false;
        private bool m_pendingPlayerControlRestore = false;

        public override IReadOnlyCollection<Type> StartupDependencies => SystemStartupDependencies;

        public override void OnSystemStart()
        {
            EnsurePlayerInstance();
            EnsurePrimaryPlayerInputTargetConfigured(nameof(OnSystemStart));
            ResetCurrentControlToPlayerInstance();
            m_pendingPlayerControlRestore = m_currentInputTarget == null;
            GameManager.PersistenceSystem.RegisterCustomInstancedPersistable(m_primaryPlayerCharacter, Constants.UniquePlayerIdentifier);
            EventKit.Type.Register<MapLoadedEvent>(OnMapLoaded);
            EventKit.Type.Register<SaveFileLoadedEvent>(OnSaveFileLoaded);
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<MapLoadedEvent>(OnMapLoaded);
            EventKit.Type.UnRegister<SaveFileLoadedEvent>(OnSaveFileLoaded);
            BindControlledCharacters(Array.Empty<CharacterBase>());
            ClearPendingControlledCharacterRestore();
            m_pendingPlayerControlRestore = false;
        }

        private void OnMapLoaded(MapLoadedEvent _)
        {
            TryRestorePendingControlledCharacters();
            TryRestorePendingPlayerControl();
        }

        private void OnSaveFileLoaded(SaveFileLoadedEvent _)
        {
            TryRestorePendingControlledCharacters();
            TryRestorePendingPlayerControl();
        }

        internal void NotifyCharacterKilled(CharacterActor character)
        {
            if (character == m_primaryPlayerCharacter)
            {
                GameManager.UISystem.CloseAllMenus();
                if (GameManager.Config.hasPlayerDeathAction)
                {
                    GameManager.Config.ExecutePlayerDeathAction(GameCommandContext.LocalPlayer(character));
                }
            }
        }

        internal void NotifyCharacterDied(CharacterBase character)
        {
            if (character != null && IsCurrentControlledMember(character))
            {
                RevalidateCurrentControlledCharacter();
            }
        }

        internal void NotifyCharacterRevived(CharacterBase character)
        {
            if (m_currentInputTarget == null &&
                character == m_primaryPlayerCharacter &&
                TryResolveControllableInputTarget(character, out IPlayerInputTarget _))
            {
                SetCurrentControlledCharacter(character);
            }
        }

        /// <summary>
        /// 切换当前消费玩家输入的正式目标。
        /// 当前默认是角色 prefab 上的 CharacterPlayerControl；控制组或编队应继续实现同一接口，而不是另造输入入口。
        /// </summary>
        public void SetCurrentInputTarget(IPlayerInputTarget inputTarget)
        {
            if (ReferenceEquals(m_currentInputTarget, inputTarget))
            {
                if (m_currentInputTarget != null)
                {
                    BindControlledCharacters(CreateCurrentControlledCharacterSnapshot());
                    NotifyCurrentControlledTargetChanged();
                }

                return;
            }

            m_currentInputTarget = inputTarget;
            m_currentControlGroup = inputTarget as PlayerControlGroup;
            if (m_currentInputTarget != null)
            {
                m_pendingPlayerControlRestore = false;
            }
            BindControlledCharacters(CreateCurrentControlledCharacterSnapshot());
            NotifyCurrentControlledTargetChanged();
        }

        /// <summary>
        /// 以角色实体切换当前控制对象。
        /// 角色必须挂有实现 <see cref="IPlayerInputTarget"/> 的玩家控制组件，否则它只能继续作为世界实体存在，不能直接接玩家输入。
        /// </summary>
        public void SetCurrentControlledCharacter(CharacterBase character)
        {
            IPlayerInputTarget inputTarget = null;
            if (character != null && !TryResolveControllableInputTarget(character, out inputTarget))
            {
                RevalidateCurrentControlledCharacter();
                return;
            }

            Debug.Assert(character == null || inputTarget != null, "The selected controlled character must expose an IPlayerInputTarget controller.");
            SetCurrentInputTarget(inputTarget);
        }

        public void SetCurrentControlGroup(params CharacterBase[] characters)
        {
            CharacterBase[] controllableCharacters = CreateControllableCharacterSnapshot(characters);
            if (controllableCharacters.Length == 0)
            {
                m_currentControlGroup = null;
                SetCurrentInputTarget(null);
                return;
            }

            if (controllableCharacters.Length == 1)
            {
                m_currentControlGroup = null;
                SetCurrentControlledCharacter(controllableCharacters[0]);
                return;
            }

            CharacterBase primaryMember = ResolvePreferredControlGroupPrimary(controllableCharacters);
            if (m_currentControlGroup == null)
            {
                m_currentControlGroup = new PlayerControlGroup(primaryMember, controllableCharacters);
            }
            else
            {
                m_currentControlGroup.ReplaceMembers(primaryMember, controllableCharacters);
            }

            SetCurrentInputTarget(m_currentControlGroup);
        }

        public bool TryAddCurrentControlGroupMember(CharacterBase character, bool makePrimary = false)
        {
            if (!TryResolveControllableInputTarget(character, out IPlayerInputTarget _))
            {
                return false;
            }

            if (m_currentControlGroup == null)
            {
                CharacterBase currentControlledCharacter = GetCurrentControlledCharacter();
                if (currentControlledCharacter == null || currentControlledCharacter == character)
                {
                    return false;
                }

                SetCurrentControlGroup(currentControlledCharacter, character);
                if (makePrimary)
                {
                    TrySetCurrentControlGroupPrimaryMember(character);
                }

                return true;
            }

            if (!m_currentControlGroup.TryAddMember(character, makePrimary))
            {
                return false;
            }

            RefreshCurrentInputTargetFromControlGroup();
            return true;
        }

        public bool TryRemoveCurrentControlGroupMember(CharacterBase character)
        {
            if (m_currentControlGroup == null)
            {
                return false;
            }

            if (!m_currentControlGroup.RemoveMember(character))
            {
                return false;
            }

            if (m_currentControlGroup.MemberCount == 0)
            {
                m_currentControlGroup = null;
                SetCurrentInputTarget(null);
                return true;
            }

            if (m_currentControlGroup.MemberCount == 1)
            {
                CharacterBase[] remainingMembers = m_currentControlGroup.CreateControlledCharacterSnapshot();
                m_currentControlGroup = null;
                SetCurrentControlledCharacter(remainingMembers.Length > 0 ? remainingMembers[0] : null);
                return true;
            }

            RefreshCurrentInputTargetFromControlGroup();
            return true;
        }

        public bool TrySetCurrentControlGroupPrimaryMember(CharacterBase character)
        {
            if (m_currentControlGroup == null || !m_currentControlGroup.TrySetPrimaryMember(character))
            {
                return false;
            }

            RefreshCurrentInputTargetFromControlGroup();
            return true;
        }

        /// <summary>
        /// 当前玩家命令的正式提交入口。
        /// InputSystem、后续本地 RTS 选择器或脚本命令都应先过 PlayerSystem，再由它决定命中单角色还是控制组订单链。
        /// </summary>
        public PlayerCommandResult SubmitPlayerCommand(PlayerCommandRequest commandRequest)
        {
            return SubmitPlayerOrder(PlayerOrderRequest.FromCommandRequest(commandRequest)).LastCommandResult;
        }

        /// <summary>
        /// 当前玩家订单的正式提交入口。
        /// 控制组会走正式订单链；单角色目标则由 CharacterPlayerControl 执行。
        /// 订单入口不再由 InputSystem 直接抓住输入目标后自行分发。
        /// </summary>
        public PlayerOrderResult SubmitPlayerOrder(PlayerOrderRequest orderRequest)
        {
            if (m_currentInputTarget == null)
            {
                PlayerCommandResult missingTargetResult = PlayerCommandResult.Failed(
                    orderRequest.CommandRequest,
                    EPlayerCommandFailureReason.MissingInputTarget);
                return PlayerOrderResult.Failed(orderRequest, 0, missingTargetResult);
            }

            return m_currentInputTarget.SubmitPlayerOrder(orderRequest);
        }

        private void Update()
        {
            TryRestorePendingPlayerControl();
            TryRestorePendingControlledCharacters();
            m_currentControlGroup?.Tick();
        }

        private void StageControlledCharacterRestore(PlayerDataBlock block)
        {
            m_pendingControlledCharacterReferences = block?.currentControlledCharacters ?? Array.Empty<PersistableReference<CharacterBase>>();
            m_hasPendingControlledCharacterRestore = HasResolvableControlledCharacterReferences(m_pendingControlledCharacterReferences);
        }

        private bool TryRestorePendingControlledCharacters()
        {
            if (!m_hasPendingControlledCharacterRestore)
            {
                return false;
            }

            CharacterBase[] resolvedCharacters = ResolvePendingControlledCharacters();
            if (resolvedCharacters.Length == 0)
            {
                return false;
            }

            if (resolvedCharacters.Length == 1)
            {
                SetCurrentControlledCharacter(resolvedCharacters[0]);
                ClearPendingControlledCharacterRestore();
                return true;
            }

            SetCurrentControlGroup(resolvedCharacters);
            ClearPendingControlledCharacterRestore();
            return true;
        }

        private CharacterBase[] ResolvePendingControlledCharacters()
        {
            List<CharacterBase> resolvedCharacters = new();
            foreach (PersistableReference<CharacterBase> characterReference in m_pendingControlledCharacterReferences)
            {
                if (!TryResolvePendingControlledCharacterReference(characterReference, out CharacterBase character))
                {
                    continue;
                }

                if (!resolvedCharacters.Contains(character))
                {
                    resolvedCharacters.Add(character);
                }
            }

            return resolvedCharacters.ToArray();
        }

        private static bool TryResolvePendingControlledCharacterReference(
            PersistableReference<CharacterBase> characterReference,
            out CharacterBase character)
        {
            if (characterReference.TryResolve(out character) &&
                TryResolveControllableInputTarget(character, out IPlayerInputTarget _))
            {
                return true;
            }

            character = null;
            return false;
        }

        private static bool HasResolvableControlledCharacterReferences(PersistableReference<CharacterBase>[] references)
        {
            if (references == null || references.Length == 0)
            {
                return false;
            }

            foreach (PersistableReference<CharacterBase> reference in references)
            {
                if (!string.IsNullOrWhiteSpace(reference.identifier))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearPendingControlledCharacterRestore()
        {
            m_pendingControlledCharacterReferences = Array.Empty<PersistableReference<CharacterBase>>();
            m_hasPendingControlledCharacterRestore = false;
        }

        public bool IsCurrentControlledMember(CharacterBase character)
        {
            if (!character)
            {
                return false;
            }

            CharacterBase[] controlledCharacters = CreateCurrentControlledCharacterSnapshot();
            foreach (CharacterBase controlledCharacter in controlledCharacters)
            {
                if (controlledCharacter == character)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryGetCurrentInputTarget(out IPlayerInputTarget inputTarget)
        {
            inputTarget = m_currentInputTarget;
            return inputTarget != null;
        }

        public void RevalidateCurrentControlledCharacter()
        {
            CharacterBase currentControlledCharacter = GetCurrentControlledCharacter();
            if (m_currentInputTarget == null)
            {
                return;
            }

            if (m_currentInputTarget is PlayerControlGroup controlGroup)
            {
                CharacterBase[] validMembers = controlGroup.CreateControlledCharacterSnapshot();
                if (validMembers.Length > 1)
                {
                    CharacterBase preferredPrimary = ResolvePreferredControlGroupPrimary(validMembers);
                    controlGroup.ReplaceMembers(preferredPrimary, validMembers);
                    BindControlledCharacters(validMembers);
                    NotifyCurrentControlledTargetChanged();
                    return;
                }

                if (validMembers.Length == 1)
                {
                    m_currentControlGroup = null;
                    SetCurrentControlledCharacter(validMembers[0]);
                    return;
                }
            }

            if (TryResolveControllableInputTarget(currentControlledCharacter, out IPlayerInputTarget _))
            {
                CharacterBase[] currentSnapshot = CreateCurrentControlledCharacterSnapshot();
                if (!ReferenceEquals(m_boundControlledCharacter, currentControlledCharacter) ||
                    !AreSameControlledCharacters(m_boundControlledCharacters, currentSnapshot))
                {
                    BindControlledCharacters(currentSnapshot);
                    NotifyCurrentControlledTargetChanged();
                }

                return;
            }

            if (m_primaryPlayerCharacter != null &&
                m_primaryPlayerCharacter != currentControlledCharacter &&
                TryResolveControllableInputTarget(m_primaryPlayerCharacter, out IPlayerInputTarget _))
            {
                SetCurrentControlledCharacter(m_primaryPlayerCharacter);
                return;
            }

            SetCurrentInputTarget(null);
            m_pendingPlayerControlRestore = m_primaryPlayerCharacter != null;
        }

        /// <summary>
        /// 玩家长期存档归属的主角色查询口。
        /// 这里不表示当前前台受控对象。
        /// </summary>
        public CharacterActor GetPrimaryPlayerCharacter()
        {
            return m_primaryPlayerCharacter;
        }

        public CharacterBase GetCurrentControlledCharacterOrPlayerInstance()
        {
            return GetCurrentControlledCharacter() ?? m_primaryPlayerCharacter;
        }

        public bool TryGetCurrentControlledCharacter(out CharacterBase character)
        {
            character = GetCurrentControlledCharacter();
            return character != null;
        }

        private CharacterBase[] CreateCurrentControlledCharacterSnapshot()
        {
            return m_currentInputTarget != null
                ? m_currentInputTarget.CreateControlledCharacterSnapshot()
                : Array.Empty<CharacterBase>();
        }

        /// <summary>
        /// 当前控制角色切换的正式订阅入口。
        /// 外部只允许挂监听，不允许直接持有事件对象本体。
        /// </summary>
        public void AddCurrentControlledCharacterChangedListener(UnityAction<CharacterBase> listener)
        {
            m_currentControlledCharacterChanged.AddListener(listener);
        }

        public void RemoveCurrentControlledCharacterChangedListener(UnityAction<CharacterBase> listener)
        {
            m_currentControlledCharacterChanged.RemoveListener(listener);
        }

        private void EnsurePlayerInstance()
        {
            if (!m_primaryPlayerCharacter)
            {
                throw new InvalidOperationException(
                    "PlayerSystem requires an explicitly assigned primary player character.");
            }

            UnityEngine.SceneManagement.Scene scene = m_primaryPlayerCharacter.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "PlayerSystem primary player character must belong to a loaded scene.");
            }
        }

        private void EnsurePrimaryPlayerInputTargetConfigured(string operationName)
        {
            if (m_primaryPlayerCharacter.HasConfiguredPlayerInputTarget(out CharacterPlayerControl _))
            {
                return;
            }

            throw new InvalidOperationException(
                $"{nameof(PlayerSystem)}.{operationName} requires the primary player character " +
                $"[{m_primaryPlayerCharacter.gameObject.name}] to have an enabled " +
                $"{nameof(CharacterPlayerControl)} that accepts player input.");
        }

        public void LoadDataBlock(PlayerDataBlock block)
        {
            EnsurePlayerInstance();
            EnsurePrimaryPlayerInputTargetConfigured(nameof(LoadDataBlock));
            StageControlledCharacterRestore(block);

            if (block?.primaryCharacterData != null)
            {
                m_primaryPlayerCharacter.LoadActorRuntimeState(block.primaryCharacterData);
            }

            ResetCurrentControlToPlayerInstance();
            TryRestorePendingControlledCharacters();
        }

        public PlayerDataBlock CreateDataBlock()
        {
            EnsurePlayerInstance();

            CharacterBase[] controlledCharacters = CreateCurrentControlledCharacterSnapshot();
            return new PlayerDataBlock
            {
                primaryCharacterData = m_primaryPlayerCharacter.CreateActorRuntimeState(),
                currentControlledCharacters = CreateControlledCharacterReferenceSnapshot(controlledCharacters)
            };
        }

        /// <summary>
        /// 当前控制对象一旦被销毁，正式回退规则必须只留在 PlayerSystem 闭包内。
        /// 控制对象销毁后的回退规则只留在 PlayerSystem 闭包内。
        /// </summary>
        private void OnCurrentControlledCharacterDestroyed()
        {
            RevalidateCurrentControlledCharacter();
        }

        private void ResetCurrentControlToPlayerInstance()
        {
            SetCurrentControlledCharacter(m_primaryPlayerCharacter);
        }

        private bool TryRestorePendingPlayerControl()
        {
            if (!m_pendingPlayerControlRestore)
            {
                return false;
            }

            if (m_currentInputTarget != null)
            {
                m_pendingPlayerControlRestore = false;
                return true;
            }

            if (!TryResolveControllableInputTarget(m_primaryPlayerCharacter, out IPlayerInputTarget inputTarget))
            {
                return false;
            }

            SetCurrentInputTarget(inputTarget);
            m_pendingPlayerControlRestore = m_currentInputTarget == null;
            return m_currentInputTarget != null;
        }

        private void BindControlledCharacters(CharacterBase[] characters)
        {
            CharacterBase[] controlledCharacters = CreateControllableCharacterSnapshot(characters);
            if (AreSameControlledCharacters(m_boundControlledCharacters, controlledCharacters))
            {
                return;
            }

            foreach (CharacterBase boundCharacter in m_boundControlledCharacters)
            {
                if (boundCharacter != null)
                {
                    boundCharacter.RemoveDestroyedListener(OnCurrentControlledCharacterDestroyed);
                }
            }

            m_boundControlledCharacters.Clear();
            m_boundControlledCharacters.AddRange(controlledCharacters);
            m_boundControlledCharacter = controlledCharacters.Length > 0 ? controlledCharacters[0] : null;

            foreach (CharacterBase boundCharacter in m_boundControlledCharacters)
            {
                boundCharacter.AddDestroyedListener(OnCurrentControlledCharacterDestroyed);
            }
        }

        private void NotifyCurrentControlledTargetChanged()
        {
            CharacterBase currentControlledCharacter = GetCurrentControlledCharacter();
            m_currentControlledCharacterChanged.Invoke(currentControlledCharacter);
        }

        private void RefreshCurrentInputTargetFromControlGroup()
        {
            if (m_currentControlGroup == null)
            {
                return;
            }

            BindControlledCharacters(m_currentControlGroup.CreateControlledCharacterSnapshot());
            NotifyCurrentControlledTargetChanged();
        }

        private static PersistableReference<CharacterBase>[] CreateControlledCharacterReferenceSnapshot(CharacterBase[] characters)
        {
            if (characters == null || characters.Length == 0)
            {
                return Array.Empty<PersistableReference<CharacterBase>>();
            }

            List<PersistableReference<CharacterBase>> references = new();
            foreach (CharacterBase character in characters)
            {
                if (character == null)
                {
                    continue;
                }

                PersistableReference<CharacterBase> reference = character;
                if (string.IsNullOrWhiteSpace(reference.identifier))
                {
                    continue;
                }

                references.Add(reference);
            }

            return references.ToArray();
        }

        private CharacterBase GetCurrentControlledCharacter()
        {
            return m_currentInputTarget != null && m_currentInputTarget.TryGetControlledCharacter(out CharacterBase character)
                ? character
                : null;
        }

        private static CharacterBase[] CreateControllableCharacterSnapshot(CharacterBase[] characters)
        {
            if (characters == null || characters.Length == 0)
            {
                return Array.Empty<CharacterBase>();
            }

            List<CharacterBase> snapshot = new();
            foreach (CharacterBase character in characters)
            {
                if (!TryResolveControllableInputTarget(character, out IPlayerInputTarget _))
                {
                    continue;
                }

                if (!snapshot.Contains(character))
                {
                    snapshot.Add(character);
                }
            }

            return snapshot.ToArray();
        }

        private static bool TryResolveControllableInputTarget(CharacterBase character, out IPlayerInputTarget inputTarget)
        {
            inputTarget = null;
            return character != null &&
                character.TryResolvePlayerInputTarget(out inputTarget);
        }

        private CharacterBase ResolvePreferredControlGroupPrimary(CharacterBase[] controllableCharacters)
        {
            if (controllableCharacters == null || controllableCharacters.Length == 0)
            {
                return null;
            }

            CharacterBase currentControlledCharacter = GetCurrentControlledCharacter();
            if (currentControlledCharacter != null &&
                Array.IndexOf(controllableCharacters, currentControlledCharacter) >= 0)
            {
                return currentControlledCharacter;
            }

            return controllableCharacters[0];
        }

        private static bool AreSameControlledCharacters(
            IReadOnlyList<CharacterBase> currentCharacters,
            CharacterBase[] nextCharacters)
        {
            int currentCount = currentCharacters?.Count ?? 0;
            int nextCount = nextCharacters?.Length ?? 0;
            if (currentCount != nextCount)
            {
                return false;
            }

            for (int i = 0; i < currentCount; i++)
            {
                if (currentCharacters[i] != nextCharacters[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
