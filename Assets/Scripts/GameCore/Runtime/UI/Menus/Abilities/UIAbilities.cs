using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using azixMcAze.SerializableDictionary;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 能力菜单中的能力分类。
    /// </summary>
    public enum EAbilityType
    {
        Passive,
        Active
    }

    /// <summary>
    /// 能力菜单面板，展示角色主动能力、装备槽位和能力说明，并处理能力装备流程。
    /// </summary>
    public class UIAbilities : UIKitMenuPanelBase, IAbilityMenuEventReceiver
    {
        [Header("引用")]
        [LabelText("能力条目预制体")]
        [Tooltip("能力列表条目对象池使用的预制体。")]
        [SerializeField] private GameObject m_abilityBarEntryPrefab = null;

        [LabelText("能力列表根节点")]
        [Tooltip("能力列表条目实例挂载的父节点。")]
        [SerializeField] private GameObject m_abilityListRoot = null;

        [LabelText("列表 CanvasGroup")]
        [Tooltip("进入装备模式时会临时禁用列表交互。")]
        [SerializeField] private CanvasGroup m_listCanvasGroup = null;

        [LabelText("能力描述")]
        [Tooltip("显示当前悬停能力或分类说明的文本。")]
        [SerializeField] private TextMeshProUGUI m_description = null;

        [LabelText("能力栏")]
        [Tooltip("显示和修改角色已装备能力槽位的 UIAbilityBar。")]
        [SerializeField] private UIAbilityBar m_abilityBar = null;

        [LabelText("能力分类")]
        [Tooltip("能力分类按钮映射，键为分类类型，值为对应 UI 分类控件。")]
        [SerializeField] private SerializableDictionary<EAbilityType, UIAbilityCategory> m_categories = null;

        [LabelText("装备模式显示对象")]
        [Tooltip("进入能力装备模式时启用、退出时关闭的辅助 UI 对象。")]
        [SerializeField] private List<GameObject> m_toEnableWhenEquippingAnAbility = null;

        [LabelText("能力条目池大小")]
        [Tooltip("能力列表条目对象池容量。")]
        [SerializeField] private int m_abilityListEntryPoolSize = 16;

        [Header("设置")]
        [LabelText("主动能力说明")]
        [Tooltip("悬停主动能力分类时显示的说明文本。")]
        [SerializeField][TextArea] private string m_activeAbilityDescription;

        [LabelText("被动能力说明")]
        [Tooltip("悬停被动能力分类时显示的说明文本。")]
        [SerializeField][TextArea] private string m_passiveAbilityDescription;

        private UIAbilityListEntry[] m_entries = System.Array.Empty<UIAbilityListEntry>();
        private UIAbilityListEntry m_abilitySelected = null;
        private CharacterBase m_currentCharacter = null;
        private bool m_followCurrentControlledCharacter = true;
        private EAbilityType m_selectedCategory = EAbilityType.Active;
        private readonly List<GameObject> m_activeAbilityEntries = new();
        private bool m_currentControlledCharacterListening = false;

        protected override bool HandleBackRequested()
        {
            if (m_abilitySelected != null)
            {
                ExitEquipMode(m_abilitySelected);
                return true;
            }

            return false;
        }

        protected override void OnPanelInit()
        {
            ConfigureAbilityEntryPool();
        }

        private void OnDestroy()
        {
            StopCurrentControlledCharacterListening();
            m_abilityBar.PresentCharacter(null);
            ReturnAbilityEntries();
        }

        private int GetAbilityCount(EAbilityType type)
        {
            if (m_currentCharacter == null)
            {
                return 0;
            }

            return
                type == EAbilityType.Active ?
                m_currentCharacter.GetActiveAbilityMenuEntrySnapshots().Length :
                0;
        }

        protected override GameObject ResolveDefaultFocusTarget()
        {
            foreach (UIAbilityListEntry entry in m_entries)
            {
                if (entry != null)
                {
                    return entry.gameObject;
                }
            }

            return base.ResolveDefaultFocusTarget();
        }

        protected override void OnPanelShown(UIKitMenuOpenData openData)
        {
            m_followCurrentControlledCharacter = !TryResolveTargetCharacter(openData, out CharacterBase targetCharacter);
            m_abilityBar.Init();
            BindCurrentControlledCharacterListenerForContext();
            BindCharacter(m_followCurrentControlledCharacter ? ResolveCurrentControlledCharacter() : targetCharacter);
            m_abilityBar.UpdateUI();
            SelectCategory(m_selectedCategory);
            UpdateUI();
        }

        protected override void OnPanelHidden()
        {
            StopCurrentControlledCharacterListening();
            ExitEquipMode();
            m_abilityBar.PresentCharacter(null);
            m_currentCharacter = null;
            ReturnAbilityEntries();
        }

        private void UpdateUI()
        {
            foreach (var category in m_categories)
            {
                category.Value.SetCategory(category.Key, GetAbilityCount(category.Key));
            }
        }

        private void ClearSpellBookList() => ReturnAbilityEntries();

        private void FillSpellBookList(EAbilityType type = EAbilityType.Active)
        {
            CharacterAbilityMenuEntry[] abilities =
                m_currentCharacter == null ?
                System.Array.Empty<CharacterAbilityMenuEntry>() :
                type == EAbilityType.Active ?
                m_currentCharacter.GetActiveAbilityMenuEntrySnapshots() :
                System.Array.Empty<CharacterAbilityMenuEntry>();

            m_entries = new UIAbilityListEntry[abilities.Length];

            for (int i = 0; i < abilities.Length; ++i)
            {
                CharacterAbilityMenuEntry ability = abilities[i];

                GameObject entryInstance = GameObjectPoolService.Rent(m_abilityBarEntryPrefab, m_abilityListRoot.transform);
                if (entryInstance == null)
                {
                    Debug.LogWarning("没有可用的能力列表条目实例，请检查能力列表对象池容量。", this);
                    continue;
                }

                if (!entryInstance.TryGetComponent(out UIAbilityListEntry entry))
                {
                    Debug.LogError("能力列表条目预制体缺少 UIAbilityListEntry 组件。", entryInstance);
                    GameObjectPoolService.Return(entryInstance);
                    continue;
                }

                entry.Initialize(ability, type);
                m_entries[i] = entry;
                m_activeAbilityEntries.Add(entryInstance);
            }
        }

        private void EnterEquipMode(UIAbilityListEntry ability)
        {
            m_abilitySelected = ability;

            if (!GameManager.InputSystem.IsPointerActive(EActionMap.UI))
            {
                m_abilityBar.SelectFirstElement();
            }

            m_toEnableWhenEquippingAnAbility.ForEach(go => go.SetActive(true));
            m_listCanvasGroup.interactable = false;
        }

        private void ExitEquipMode(UIAbilityListEntry toSelect = null)
        {
            m_toEnableWhenEquippingAnAbility.ForEach(go => go.SetActive(false));
            m_abilitySelected = null;
            m_listCanvasGroup.interactable = true;

            if (toSelect != null && !GameManager.InputSystem.IsPointerActive(EActionMap.UI))
            {
                toSelect.ForceSelection();
            }
        }

        public void HandleAbilityHovered(CharacterAbilityMenuEntry entry)
        {
            if (!entry.HasDisplaySource)
            {
                HandleNullAbilityHovered();
                return;
            }

            List<AbilityDescriptionLine> lines = new();
            entry.GenerateAdditionalDescriptionLines(lines);
            SetAbilityDescriptionText(entry.Description, lines);
        }

        public void HandleAbilityHovered(CharacterEquippedAbilitySlotView slot)
        {
            if (!slot.HasDisplaySource)
            {
                HandleNullAbilityHovered();
                return;
            }

            List<AbilityDescriptionLine> lines = new();
            if (slot.HasFormalGasAbility)
            {
                FormalGasAbilityDescriptionResolver.TryAppendFormalDamageLines(slot.FormalGasAbilityCode, lines);
            }

            SetAbilityDescriptionText(slot.Description, lines);
        }

        private void SetAbilityDescriptionText(string description, List<AbilityDescriptionLine> lines)
        {
            m_description.text = description ?? string.Empty;

            // 额外说明和主描述之间保留一行空白，避免菜单文本粘连。
            if (lines.Count > 0)
            {
                m_description.text += "\n";
            }

            foreach (var line in lines)
            {
                string header = !string.IsNullOrEmpty(line.header) ? $"<u>{line.header}</u>: " : string.Empty;
                m_description.text += $"\n{header}{line.content}";
            }
        }

        public void HandleNullAbilityHovered()
        {
            m_description.text = string.Empty;
        }

        public void HandleAbilitySelectedFromList(UIAbilityListEntry ability) => EnterEquipMode(ability);

        public void HandleAbilityCategorySelected(EAbilityType type) => SelectCategory(type);

        private void SelectCategory(EAbilityType type)
        {
            m_selectedCategory = type;

            foreach (var entry in m_categories)
            {
                entry.Value.SetHighlight(false);
            }

            m_categories[type].SetHighlight(true);

            ClearSpellBookList();
            FillSpellBookList(type);
        }

        public void HandleAbilityCategoryHovered(EAbilityType type)
        {
            m_description.text =
                type == EAbilityType.Active ?
                m_activeAbilityDescription :
                m_passiveAbilityDescription;
        }

        public void HandleAbilitySlotClicked(int abilityIndex)
        {
            if (m_currentCharacter == null)
            {
                return;
            }

            if (m_abilitySelected == null)
            {
                m_currentCharacter.ClearEquippedAbilitySlot(abilityIndex);
            }
            else
            {
                CharacterAbilityMenuEntry ability = m_abilitySelected.GetTarget();
                if (ability.HasFormalGasAbility)
                {
                    m_currentCharacter.TryEquipFormalGasAbilityCodeToSlot(ability.FormalGasAbilityCode, abilityIndex);
                }
                ExitEquipMode(m_abilitySelected);
            }
        }

        private void OnCurrentControlledCharacterChanged(CharacterBase character)
        {
            if (m_followCurrentControlledCharacter)
            {
                BindCharacter(ResolveCurrentControlledCharacter());
            }
        }

        private void BindCurrentControlledCharacterListenerForContext()
        {
            if (m_followCurrentControlledCharacter)
            {
                StartCurrentControlledCharacterListeningIfReady();
            }
            else
            {
                StopCurrentControlledCharacterListening();
            }
        }

        private void StartCurrentControlledCharacterListeningIfReady()
        {
            if (m_currentControlledCharacterListening)
            {
                return;
            }

            if (!GameManager.Exists() || !GameManager.HasSystem<PlayerSystem>())
            {
                return;
            }

            m_currentControlledCharacterListening = true;
            GameManager.PlayerSystem.AddCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
        }

        private void StopCurrentControlledCharacterListening()
        {
            if (!m_currentControlledCharacterListening)
            {
                return;
            }

            m_currentControlledCharacterListening = false;
            if (GameManager.Exists() && GameManager.HasSystem<PlayerSystem>())
            {
                GameManager.PlayerSystem.RemoveCurrentControlledCharacterChangedListener(OnCurrentControlledCharacterChanged);
            }
        }

        private void BindCharacter(CharacterBase character)
        {
            if (ReferenceEquals(m_currentCharacter, character))
            {
                return;
            }

            m_currentCharacter = character;
            m_abilityBar.PresentCharacter(m_currentCharacter);

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            ExitEquipMode();
            SelectCategory(m_selectedCategory);
            UpdateUI();
        }

        private static bool TryResolveTargetCharacter(UIKitMenuOpenData openData, out CharacterBase character)
        {
            character = null;
            if (openData == null || openData.ArgumentCount != 1)
            {
                return false;
            }

            return openData.TryGetArgument(0, out character);
        }

        private static CharacterBase ResolveCurrentControlledCharacter()
        {
            return GameManager.TryGetSystem(out PlayerSystem playerSystem)
                ? playerSystem.GetCurrentControlledCharacterOrPlayerInstance()
                : null;
        }

        private void ConfigureAbilityEntryPool()
        {
            if (m_abilityBarEntryPrefab == null)
            {
                return;
            }

            GameObjectPoolService.SetMaxCapacity(m_abilityBarEntryPrefab, m_abilityListEntryPoolSize);
            GameObjectPoolService.Prewarm(m_abilityBarEntryPrefab, m_abilityListEntryPoolSize);
        }

        private void ReturnAbilityEntries()
        {
            foreach (GameObject entry in m_activeAbilityEntries)
            {
                if (entry)
                {
                    GameObjectPoolService.Return(entry);
                }
            }

            m_activeAbilityEntries.Clear();
            m_entries = System.Array.Empty<UIAbilityListEntry>();
        }
    }
}

