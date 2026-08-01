using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;
using azixMcAze.SerializableDictionary;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 统一管理 UI 中的手柄/键盘按键提示图标，并在输入设备变化时刷新已注册按钮。
    /// </summary>
    public class UIControllerButtonManager : MonoBehaviour
    {
        /// <summary>
        /// UI 提示使用的控制器图标库类型，和 SpriteLibraryAsset 的一级分类保持一致。
        /// </summary>
        public enum EControllerType
        {
            Keyboard,
            XBOX,
            Playstation
        }

        /// <summary>
        /// UI 可展示的输入动作集合，负责把 UI 图标请求映射回正式输入系统动作。
        /// </summary>
        public enum EAction
        {
            Interact,
            FireAbility1,
            FireAbility2,
            FireAbility3,
            FireAbility4,
            FireAbility5,
            OpenGameMenu,
            Submit,
            Cancel
        }

        /// <summary>
        /// 同一按键提示在普通态和按下态之间切换的图标状态。
        /// </summary>
        public enum EButtonState
        {
            Idle,
            Pressed
        }

        [InspectorName("控制器图标库")]
        [Tooltip("按控制器类型配置 SpriteLibraryAsset；每个库内需要用动作名作为分类、按钮状态作为标签。")]
        [SerializeField] private SerializableDictionary<EControllerType, SpriteLibraryAsset> m_controllerSpriteLibraries = null;

        [InspectorName("显示按键提示")]
        [Tooltip("默认关闭，避免测试时按键提示遮挡画面；需要检查提示时可在 Inspector 或代码中打开。")]
        [SerializeField] private bool m_buttonPromptsVisible = false;

        private static readonly EButtonState[] s_buttonStates = (EButtonState[])Enum.GetValues(typeof(EButtonState));

        private readonly HashSet<UIControllerButton> m_buttons = new();
        private readonly List<UIControllerButton> m_staleButtons = new();
        private EControllerType m_controllerType;
        private bool m_controlsChangedListening = false;

        public bool buttonPromptsVisible => m_buttonPromptsVisible;

        private void OnEnable()
        {
            StartControlsChangedListeningIfReady();
        }

        private void Start()
        {
            StartControlsChangedListeningIfReady();
        }

        private void OnDisable()
        {
            StopControlsChangedListening();
        }

        private void OnDestroy()
        {
            StopControlsChangedListening();
        }

        private void StartControlsChangedListeningIfReady()
        {
            if (m_controlsChangedListening)
            {
                return;
            }

            if (!GameManager.Exists() || !GameManager.HasSystem<InputSystem>())
            {
                return;
            }

            m_controllerType = MapDisplayType(GameManager.InputSystem.GetCurrentControlDisplayType());
            m_controlsChangedListening = true;
            GameManager.InputSystem.AddControlsChangedListener(UpdateControllerType);
            UpdateControllerButtons();
        }

        private void StopControlsChangedListening()
        {
            if (!m_controlsChangedListening)
            {
                return;
            }

            m_controlsChangedListening = false;
            if (GameManager.Exists() && GameManager.HasSystem<InputSystem>())
            {
                GameManager.InputSystem.RemoveControlsChangedListener(UpdateControllerType);
            }
        }

        private void UpdateControllerType()
        {
            if (!GameManager.Exists() || !GameManager.HasSystem<InputSystem>())
            {
                return;
            }

            SetControllerType(MapDisplayType(GameManager.InputSystem.GetCurrentControlDisplayType()));
        }

        /// <summary>
        /// 注册一个需要随控制器类型和按下状态自动刷新的 UI 按钮提示。
        /// </summary>
        public void RegisterButton(UIControllerButton button)
        {
            if (button == null)
            {
                return;
            }

            m_buttons.Add(button);
            UpdateButton(button);
        }

        /// <summary>
        /// 注销已销毁或不再显示的按钮提示，避免后续设备切换时继续访问旧对象。
        /// </summary>
        public void UnregisterButton(UIControllerButton button)
        {
            if (button == null)
            {
                return;
            }

            m_buttons.Remove(button);
        }

        private void SetControllerType(EControllerType controllerType)
        {
            if (m_controllerType == controllerType)
            {
                return;
            }

            m_controllerType = controllerType;
            UpdateControllerButtons();
        }

        private static EControllerType MapDisplayType(EInputControlDisplayType displayType)
        {
            return displayType switch
            {
                EInputControlDisplayType.XBOX => EControllerType.XBOX,
                EInputControlDisplayType.Playstation => EControllerType.Playstation,
                _ => EControllerType.Keyboard
            };
        }

        private void UpdateControllerButtons()
        {
            m_staleButtons.Clear();
            foreach (UIControllerButton button in m_buttons)
            {
                if (button == null || !button.isActiveAndEnabled)
                {
                    m_staleButtons.Add(button);
                    continue;
                }

                UpdateButton(button);
            }

            for (int i = 0; i < m_staleButtons.Count; i++)
            {
                m_buttons.Remove(m_staleButtons[i]);
            }
        }

        private bool IsActionPressed(EAction action)
        {
            switch (action)
            {
                case EAction.Interact: return GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.Interact);
                case EAction.FireAbility1: return GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.FireAbility1);
                case EAction.FireAbility2: return GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.FireAbility2);
                case EAction.FireAbility3: return GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.FireAbility3);
                case EAction.FireAbility4: return GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.FireAbility4);
                case EAction.FireAbility5: return GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.FireAbility5);
                case EAction.OpenGameMenu: return GameManager.InputSystem.IsGameplayActionPressed(EGameplayInputAction.OpenGameMenu);
                case EAction.Submit: return GameManager.InputSystem.IsUIActionPressed(EUIInputAction.Submit);
                case EAction.Cancel: return GameManager.InputSystem.IsUIActionPressed(EUIInputAction.Cancel);
            }

            return false;
        }

        /// <summary>
        /// 供按钮自身在配置变化后请求立即重刷图标和按下状态回调。
        /// </summary>
        public void ForceUpdateButton(UIControllerButton button)
        {
            if (button == null)
            {
                return;
            }

            UpdateButton(button);
        }

        public void SetButtonPromptsVisible(bool visible)
        {
            if (m_buttonPromptsVisible == visible)
            {
                return;
            }

            m_buttonPromptsVisible = visible;
            UpdateControllerButtons();
        }

        [ContextMenu("显示按键提示")]
        private void ShowButtonPrompts()
        {
            SetButtonPromptsVisible(true);
        }

        [ContextMenu("隐藏按键提示")]
        private void HideButtonPrompts()
        {
            SetButtonPromptsVisible(false);
        }

        private void UpdateButton(UIControllerButton button)
        {
            if (button == null || !button.isActiveAndEnabled)
            {
                return;
            }

            button.SetPromptVisible(m_buttonPromptsVisible);
            if (!m_buttonPromptsVisible)
            {
                button.Initialize(null, null);
                return;
            }

            if (m_controllerSpriteLibraries == null ||
                !m_controllerSpriteLibraries.TryGetValue(m_controllerType, out SpriteLibraryAsset currentLibrary) ||
                currentLibrary == null)
            {
                Debug.LogError($"UI 控制器按键提示缺少 {m_controllerType} 图标库配置。", this);
                button.Initialize(null, null);
                button.SetPromptVisible(false);
                return;
            }

            Dictionary<EButtonState, Sprite> sprites = new();

            foreach (EButtonState state in s_buttonStates)
            {
                Sprite sprite = currentLibrary.GetSprite(button.action.ToString(), state.ToString());
                sprites.Add(state, sprite);
            }

            // 按钮提示只读取 InputSystem 的正式动作状态，不再持有原始 InputAction 本体。
            button.Initialize(sprites, () => IsActionPressed(button.action));
        }
    }
}
