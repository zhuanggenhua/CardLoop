using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 箱子存档块，在实体 Transform 之外额外保存是否已经打开。
    /// </summary>
    [Serializable]
    public class ChestDataBlock : EntityDataBlock
    {
        /// <summary>
        /// 箱子是否已经执行过首次开启逻辑。
        /// </summary>
        public bool opened;
    }

    /// <summary>
    /// 可交互宝箱，负责开启动画、掉落展示、容器背包初始化和打开转移菜单。
    /// </summary>
    public class Chest : Entity
    {
        [Header("引用")]
        [InspectorName("箱子动画器")]
        [Tooltip("播放箱子打开状态的 Animator。")]
        [SerializeField] private Animator m_chestAnimator = null;

        [InspectorName("内容动画器")]
        [Tooltip("播放掉落物展示动画的 Animator。")]
        [SerializeField] private Animator m_contentAnimator = null;

        [InspectorName("内容图标渲染器")]
        [Tooltip("掉落物展示动画中用于轮换显示物品图标的 SpriteRenderer。")]
        [SerializeField] private SpriteRenderer m_contentSpriteRenderer = null;

        [Header("箱子设置")]
        [InspectorName("箱子掉落")]
        [Tooltip("首次开启时放入容器背包和展示给玩家的掉落内容。")]
        [SerializeField] private ChestLoot m_loot;

        [InspectorName("打开动画参数")]
        [Tooltip("箱子 Animator 中表示已打开状态的布尔参数名。")]
        [SerializeField] private string m_openedAnimationParameter = "opened";

        [InspectorName("内容揭示动画参数")]
        [Tooltip("内容 Animator 中触发掉落物展示的 Trigger 参数名。")]
        [SerializeField] private string m_contentRevealAnimationParameter = "reveal";

        [InspectorName("内容图标轮播时长")]
        [Tooltip("掉落图标在揭示动画中轮播一轮的总时长。")]
        [SerializeField] private float m_contentRevealIconCycleDuration = 1.0f;

        [InspectorName("空箱对话")]
        [Tooltip("箱子没有任何掉落时播放的对话。")]
        [SerializeField] private DialogueSequence m_noItemDialogue = null;

        [InspectorName("获得物品对话")]
        [Tooltip("箱子包含金钱或物品时，用于展示获得内容的对话模板。")]
        [SerializeField] private DialogueSequence m_hasItemDialogue = null;

        [Header("音频")]
        [InspectorName("开启音效")]
        [Tooltip("箱子首次打开且存在掉落内容时播放的音效。")]
        [SerializeField] private AudioClipResolver m_openingSound;

        private bool m_hasOpeningAnimation = false;
        private bool m_hasRevealAnimation = false;
        private bool m_opened = false;
        private bool m_opening = false;
        private Coroutine m_contentRevealCoroutine = null;

        protected override void Awake()
        {
            base.Awake();

            Debug.Assert(m_chestAnimator, ErrorMessages.InspectorMissingComponentReference<Animator>());
            Debug.Assert(m_contentAnimator, ErrorMessages.InspectorMissingComponentReference<Animator>());
            Debug.Assert(m_contentSpriteRenderer, ErrorMessages.InspectorMissingComponentReference<SpriteRenderer>());

            if (m_chestAnimator)
            {
                m_hasOpeningAnimation = AnimationUtils.HasParameter(m_chestAnimator, m_openedAnimationParameter);
            }

            if (m_contentAnimator)
            {
                m_hasRevealAnimation = AnimationUtils.HasParameter(m_contentAnimator, m_contentRevealAnimationParameter);
            }
        }

        protected virtual void Start()
        {
            if (m_opened)
            {
                TryPlayOpeningAnimation(true);
            }
        }

        private void OnDisable()
        {
            StopContentRevealCoroutine();
        }

        protected override void OnDestroy()
        {
            StopContentRevealCoroutine();
            base.OnDestroy();
        }

        /// <summary>
        /// 尝试播放箱子打开或关闭动画；动画器缺少参数时返回 false。
        /// </summary>
        public bool TryPlayOpeningAnimation(bool open)
        {
            if (m_chestAnimator && m_hasOpeningAnimation)
            {
                m_chestAnimator.SetBool(m_openedAnimationParameter, open);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 尝试播放掉落内容揭示动画，并轮换显示掉落图标。
        /// </summary>
        public bool TryPlayContentRevealAnimation()
        {
            if (m_contentSpriteRenderer && m_contentAnimator && m_hasRevealAnimation)
            {
                Sprite[] sprites = m_loot.GetLootSprites();

                if (sprites.Length > 0)
                {
                    StopContentRevealCoroutine();
                    m_contentRevealCoroutine = StartCoroutine(UpdateContentSprite(sprites, m_contentRevealIconCycleDuration));
                    m_contentAnimator.SetTrigger(m_contentRevealAnimationParameter);
                    return true;
                }

                return false;
            }

            return false;
        }

        private IEnumerator UpdateContentSprite(Sprite[] sprites, float duration)
        {
            if (sprites.Length == 0) yield break;

            float interval = duration / sprites.Length;

            for (int index = 0; index < sprites.Length; ++index)
            {
                m_contentSpriteRenderer.sprite = sprites[index];
                yield return new WaitForSeconds(interval);
            }

            m_contentRevealCoroutine = null;
        }

        private void StopContentRevealCoroutine()
        {
            if (m_contentRevealCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_contentRevealCoroutine);
            m_contentRevealCoroutine = null;
        }

        /// <summary>
        /// 用当前控制角色尝试打开箱子。
        /// </summary>
        public async Task<bool> TryOpen()
        {
            return await TryOpen(GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance());
        }

        /// <summary>
        /// 用指定角色尝试打开箱子；首次打开会初始化容器掉落，之后打开转移菜单。
        /// </summary>
        public async Task<bool> TryOpen(CharacterBase opener)
        {
            if (m_opening)
            {
                return false;
            }

            bool firstOpen = !m_opened;
            GameCommandContext commandContext = ResolveCommandContext(opener);

            if (!m_opened)
            {
                m_opening = true;
                try
                {
                    if (!m_loot.IsEmpty())
                    {
                        InitializeContainerLoot(commandContext);

                        TryPlayOpeningAnimation(true);
                        TryPlayContentRevealAnimation();
                        GameRuntimeEvents.RequestAudioPlayback(m_openingSound);

                        if (m_loot.HasMoney())
                        {
                            GameManager.DialogueSystem.AddToQueue(
                                m_hasItemDialogue.ToDialogueTree(
                                    string.Empty, commandContext, $"{m_loot.money} <currency.fullName>"
                                )
                            );
                        }
                    }
                    else
                    {
                        TryPlayOpeningAnimation(true);
                        TryPlayContentRevealAnimation();

                        GameManager.DialogueSystem.AddToQueue(
                            m_noItemDialogue.ToDialogueTree(string.Empty, commandContext)
                        );
                    }

                    m_opened = true;

                    await GameManager.DialogueSystem.PlayQueue();
                }
                finally
                {
                    m_opening = false;
                }
            }

            if (firstOpen && m_loot.IsEmpty())
            {
                return true;
            }

            return await TryOpenContainerInventory(opener, commandContext);
        }

        /// <summary>
        /// 返回该箱子容器背包对应的所有者句柄。
        /// </summary>
        public InventoryOwnerHandle GetInventoryOwner()
        {
            return InventoryOwnerHandle.ForPersistable(EInventoryOwnerKind.Container, this);
        }

        private void InitializeContainerLoot(GameCommandContext commandContext)
        {
            GameManager.InventorySystem.ExecuteChestLootInitialization(GetInventoryOwner(), m_loot);

            foreach (var entry in m_loot.GetEntries())
            {
                GameManager.DialogueSystem.AddToQueue(
                    m_hasItemDialogue.ToDialogueTree(
                        string.Empty, commandContext, $"{entry.item.displayName} x{entry.quantity}"
                    )
                );
            }
        }

        private async Task<bool> TryOpenContainerInventory(CharacterBase opener, GameCommandContext commandContext)
        {
            InventoryOwnerHandle containerOwner = GetInventoryOwner();
            if (GameManager.InventorySystem.GetBagEntries(containerOwner).Length == 0)
            {
                return !m_loot.IsEmpty();
            }

            TaskCompletionSource<bool> menuClosedTask = new();
            GameRuntimeEvents.RequestInventory(
                InventoryMenuContext.TransferToCharacter(
                    commandContext,
                    opener,
                    containerOwner,
                    EItemTransferType.Chest),
                menuClosedTask);
            return await menuClosedTask.Task;
        }

        private static GameCommandContext ResolveCommandContext(CharacterBase opener)
        {
            return GameCommandContext.ResolveForActor(opener);
        }

        protected override Type GetDataBlockType() => typeof(ChestDataBlock);

        protected override void OnSave(PersistableDataBlock block)
        {
            base.OnSave(block);
            block.As<ChestDataBlock>().opened = m_opened;
        }

        protected override void OnLoad(PersistableDataBlock block)
        {
            base.OnLoad(block);
            m_opened = block.As<ChestDataBlock>().opened;
            m_opening = false;
            StopContentRevealCoroutine();
        }
    }
}

