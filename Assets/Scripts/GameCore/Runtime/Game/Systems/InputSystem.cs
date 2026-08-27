using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using YokiFrame;

namespace GameCore
{
    [RequireComponent(typeof(PlayerInput))]
    public class InputSystem : AGameSystem
    {
        /// <summary>
        /// 玩家输入绑定保存键。
        /// 绑定数据属于本项目玩家配置，不进入 RPG 世界存档，也不由 TopDown InputManager 管理。
        /// </summary>
        public const string InputBindingsPersistenceKey = "GameCore_InputBindings";

        private PlayerInput m_playerInput = null;
        private readonly InputActionReleaseGate m_actionMapReleaseGate = new();
        private readonly HashSet<object> m_gameplayInputLocks = new();
        private GameplayActions m_gameplayActions;
        private UIActions m_uiActions;
        private readonly List<InputActionSubscription> m_externalActionSubscriptions = new();
        private EActionMap m_currentActionMap = EActionMap.None;
        private bool m_isInitialized;
        private event System.Action m_controlsChanged;

        public override void OnSystemInit()
        {
            m_playerInput = GetComponent<PlayerInput>();
            InputActionAsset actionAsset = m_playerInput.actions;
            m_gameplayActions = CreateGameplayActions(actionAsset.FindActionMap("Gameplay"));
            m_uiActions = CreateUiActions(actionAsset.FindActionMap("UI"));
            RegisterActionAssetForBindingTools(actionAsset, InputBindingsPersistenceKey);
            m_isInitialized = true;
        }

        public override void OnSystemStart()
        {
            EventKit.Type.Register<SceneTransitionStartedEvent>(OnSceneTransitionStarted);
            EventKit.Type.Register<SceneTransitionEndedEvent>(OnSceneTransitionEnded);
            m_playerInput.onControlsChanged += OnControlsChanged;

            RegisterSharedReleaseCallbacks();
        }

        public override void OnSystemStop()
        {
            m_gameplayInputLocks.Clear();
            ClearExternalInputActionListeners();
            EventKit.Type.UnRegister<SceneTransitionStartedEvent>(OnSceneTransitionStarted);
            EventKit.Type.UnRegister<SceneTransitionEndedEvent>(OnSceneTransitionEnded);
            m_playerInput.onControlsChanged -= OnControlsChanged;

            UnregisterSharedReleaseCallbacks();
        }

        public override void OnSystemShutdown()
        {
            // 初始化中途失败时可能不会进入正常 Stop；记录持有真实 InputAction，可在清空动作字段前兜底解绑。
            m_gameplayInputLocks.Clear();
            ClearExternalInputActionListeners();
            m_isInitialized = false;
            m_currentActionMap = EActionMap.None;
            m_gameplayActions = default;
            m_uiActions = default;
            m_playerInput = null;
        }

        public bool IsPointerActive(EActionMap map)
        {
            // MonoBehaviour.Update 可能早于 GameManager 的异步系统初始化；未就绪时必须保持无输入，
            // 不能把 Unity 组件启用时序误当成正式输入系统已经可用。
            if (!m_isInitialized)
            {
                return false;
            }

            return map switch
            {
                EActionMap.Gameplay => !IsGameplayInputLocked &&
                    m_gameplayActions.point.activeControl != null,
                EActionMap.UI => m_uiActions.point.activeControl != null,
                _ => false
            };
        }

        /// <summary>
        /// 读取当前 action map 下的屏幕指针位置。
        /// 指针坐标真相只由 InputSystem 持有，调用方不再直接抓原始 Point Action。
        /// </summary>
        public Vector2 ReadPointerScreenPosition(EActionMap map)
        {
            if (!m_isInitialized)
            {
                return Vector2.zero;
            }

            return map switch
            {
                EActionMap.Gameplay => m_gameplayActions.point.ReadValue<Vector2>(),
                EActionMap.UI => m_uiActions.point.ReadValue<Vector2>(),
                _ => Vector2.zero
            };
        }

        /// <summary>
        /// 读取 Gameplay 动作图中 Vector2 类型动作的当前值。
        /// 牌桌镜头等玩法表现只能通过本入口消费滚轮和方向值，不能直接读取 Unity 原始输入。
        /// </summary>
        public Vector2 ReadGameplayVector2(EGameplayInputAction action)
        {
            if (!m_isInitialized || IsGameplayInputLocked)
            {
                return Vector2.zero;
            }

            return action switch
            {
                EGameplayInputAction.Move or
                EGameplayInputAction.Point or
                EGameplayInputAction.ScrollWheel => GetGameplayAction(action).ReadValue<Vector2>(),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(action),
                    action,
                    "该 Gameplay 动作不是二维数值，不能按 Vector2 读取。")
            };
        }

        public void AddControlsChangedListener(System.Action listener)
        {
            m_controlsChanged += listener;
        }

        public void RemoveControlsChangedListener(System.Action listener)
        {
            m_controlsChanged -= listener;
        }

        /// <summary>
        /// Gameplay 输入被正式流程临时接管时为真；UI 输入不受影响。
        /// </summary>
        public bool IsGameplayInputLocked => m_gameplayInputLocks.Count > 0;

        /// <summary>
        /// 临时锁住 Gameplay 输入。只允许真实流程 owner 申请，重复申请说明调用生命周期错误。
        /// </summary>
        public void AddGameplayInputLock(object requester)
        {
            if (requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }
            if (!m_gameplayInputLocks.Add(requester))
            {
                throw new InvalidOperationException("同一个请求方重复锁定 Gameplay 输入。");
            }
        }

        /// <summary>
        /// 释放 Gameplay 输入锁。释放不存在的锁属于内部生命周期错误，必须直接暴露。
        /// </summary>
        public void RemoveGameplayInputLock(object requester)
        {
            if (requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }
            if (!m_gameplayInputLocks.Remove(requester))
            {
                throw new InvalidOperationException("请求方释放了并未持有的 Gameplay 输入锁。");
            }
        }

        /// <summary>
        /// 向 Gameplay 动作注册监听。
        /// 运行时代码必须通过本系统订阅输入，避免把 InputAction 订阅逻辑散落成第二输入入口。
        /// </summary>
        public void AddGameplayActionListener(EGameplayInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            AddExternalInputActionListener(GetGameplayAction(action), phase, listener);
        }

        public void RemoveGameplayActionListener(EGameplayInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            RemoveExternalInputActionListener(GetGameplayAction(action), phase, listener);
        }

        public void AddUIActionListener(EUIInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            AddExternalInputActionListener(GetUIAction(action), phase, listener);
        }

        public void RemoveUIActionListener(EUIInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            RemoveExternalInputActionListener(GetUIAction(action), phase, listener);
        }

        /// <summary>
        /// 为 UI 输入释放门禁准备当前按住的动作。
        /// 这样 UI 不需要再越过 InputSystem 直接接触原始 Submit/Cancel/Click Action。
        /// </summary>
        internal void PrepareUIReleaseGate(InputActionReleaseGate releaseGate, params EUIInputAction[] actions)
        {
            releaseGate.Clear();

            foreach (EUIInputAction action in actions)
            {
                releaseGate.ArmIfPressed(GetUIAction(action));
            }
        }

        public bool IsGameplayActionPressed(EGameplayInputAction action)
        {
            if (!m_isInitialized || IsGameplayInputLocked)
            {
                return false;
            }

            return GetGameplayAction(action).IsPressed();
        }

        /// <summary>
        /// 查询 Gameplay 动作是否仍被切换动作图时的释放门禁阻挡。
        /// 外部玩法监听必须在处理 started/performed 前检查，避免同一次按压穿透 UI 与 Gameplay。
        /// </summary>
        public bool IsGameplayActionBlocked(EGameplayInputAction action)
        {
            if (!m_isInitialized)
            {
                return IsGameplayInputLocked;
            }

            return IsGameplayInputLocked || IsBlocked(GetGameplayAction(action));
        }

        public bool IsUIActionPressed(EUIInputAction action)
        {
            if (!m_isInitialized)
            {
                return false;
            }

            return GetUIAction(action).IsPressed();
        }

        public string GetCurrentControlDevicesSignature()
        {
            string devices = string.Empty;

            foreach (InputDevice device in m_playerInput.devices)
            {
                if (device.enabled)
                {
                    devices += device.name + ";";
                }
            }

            return devices.ToLower();
        }

        /// <summary>
        /// 返回 UI 按键提示应使用的图标族。设备名称和布局判断只在输入系统内集中维护。
        /// </summary>
        public EInputControlDisplayType GetCurrentControlDisplayType()
        {
            foreach (InputDevice device in m_playerInput.devices)
            {
                if (!device.enabled)
                {
                    continue;
                }

                if (MatchesDeviceFamily(device, "xinput", "xbox"))
                {
                    return EInputControlDisplayType.XBOX;
                }

                if (MatchesDeviceFamily(device, "dualsense", "dualshock", "playstation"))
                {
                    return EInputControlDisplayType.Playstation;
                }
            }

            return EInputControlDisplayType.Keyboard;
        }

        public void SetActionMap(EActionMap actionMap)
        {
            m_currentActionMap = actionMap;
            m_playerInput.SwitchCurrentActionMap(actionMap.ToString());
            ArmActionMapReleaseGate(actionMap);
            UpdateEventSystemUiModuleGate();
        }

        /// <summary>
        /// 导出当前输入绑定覆盖。
        /// 用于设置菜单、云同步或调试工具读取玩家自定义按键；输入语义仍以本系统的 Gameplay/UI Action 为准。
        /// </summary>
        public string ExportBindingOverridesJson()
        {
            return InputKit.ExportBindingsJson();
        }

        /// <summary>
        /// 导入输入绑定覆盖并保存。
        /// 调用者传入的 JSON 必须来自 Unity Input System 的绑定覆盖格式。
        /// </summary>
        public void ImportBindingOverridesJson(string json)
        {
            InputKit.ImportBindingsJson(json);
        }

        /// <summary>
        /// 保存当前绑定覆盖到玩家本地配置。
        /// 这里只保存按键设置，不触碰地图、背包、任务和角色等 RPG 世界状态。
        /// </summary>
        public void SaveBindingOverrides()
        {
            InputKit.SaveBindings();
        }

        /// <summary>
        /// 从玩家本地配置加载绑定覆盖。
        /// 通常由系统初始化调用；设置菜单也可以在撤销改动时显式调用。
        /// </summary>
        public void LoadBindingOverrides()
        {
            InputKit.LoadBindings();
        }

        /// <summary>
        /// 删除玩家本地保存的绑定覆盖，并清空当前 ActionAsset 上的覆盖。
        /// </summary>
        public void ClearSavedBindingOverrides()
        {
            InputKit.ResetAllBindings();
            InputKit.ClearSavedBindings();
        }

        /// <summary>
        /// 重置指定 Action 的某一个绑定覆盖。
        /// </summary>
        public void ResetBinding(InputAction action, int bindingIndex = 0)
        {
            InputKit.ResetBinding(action, bindingIndex);
        }

        /// <summary>
        /// 重置指定 Action 的全部绑定覆盖。
        /// </summary>
        public void ResetActionBindings(InputAction action)
        {
            InputKit.ResetActionBindings(action);
        }

        /// <summary>
        /// 重置当前输入资产内所有绑定覆盖。
        /// </summary>
        public void ResetAllBindingOverrides()
        {
            InputKit.ResetAllBindings();
        }

        /// <summary>
        /// 获取指定绑定在 UI 中可显示的按键名称。
        /// </summary>
        public string GetBindingDisplayString(InputAction action, int bindingIndex = 0)
        {
            return InputKit.GetBindingDisplayString(action, bindingIndex);
        }

        /// <summary>
        /// 查询指定绑定是否与其他 Action 使用同一实际按键。
        /// 这里只做工具层检测，不自动修改玩家配置，避免菜单层替玩家做不可见决策。
        /// </summary>
        public InputAction[] GetConflictingActions(InputAction action, int bindingIndex = 0)
        {
            return InputKit.GetConflictingActions(action, bindingIndex).ToArray();
        }

        private void NotifyControlsChanged()
        {
            m_controlsChanged?.Invoke();
        }

        private GameplayActions CreateGameplayActions(InputActionMap actions)
        {
            return new GameplayActions
            {
                interact = actions.FindAction("Interact"),
                fireAbility1 = actions.FindAction("FireAbility1"),
                fireAbility2 = actions.FindAction("FireAbility2"),
                fireAbility3 = actions.FindAction("FireAbility3"),
                fireAbility4 = actions.FindAction("FireAbility4"),
                fireAbility5 = actions.FindAction("FireAbility5"),
                move = actions.FindAction("Move"),
                openGameMenu = actions.FindAction("OpenGameMenu"),
                point = actions.FindAction("Point"),
                click = actions.FindAction("Click"),
                middleClick = actions.FindAction("MiddleClick"),
                scrollWheel = actions.FindAction("ScrollWheel"),
                toggleMovementControlMode = actions.FindAction("ToggleMovementControlMode")
            };
        }

        private UIActions CreateUiActions(InputActionMap actions)
        {
            return new UIActions
            {
                submit = actions.FindAction("Submit"),
                cancel = actions.FindAction("Cancel"),
                click = actions.FindAction("Click"),
                navigate = actions.FindAction("Navigate"),
                point = actions.FindAction("Point")
            };
        }

        private static void RegisterActionAssetForBindingTools(InputActionAsset actionAsset, string persistenceKey)
        {
            InputKit.SetPersistence(new PlayerPrefsPersistence());
            InputKit.SetPersistenceKey(persistenceKey);
            InputKit.SetActionAsset(actionAsset);
            InputKit.LoadBindings();
        }

        private static bool MatchesDeviceFamily(InputDevice device, params string[] tokens)
        {
            string identity = $"{device.name};{device.layout};{device.displayName};{device.description.product};{device.description.manufacturer}".ToLowerInvariant();
            return tokens.Any(identity.Contains);
        }

        private InputAction GetGameplayAction(EGameplayInputAction action)
        {
            return action switch
            {
                EGameplayInputAction.Move => m_gameplayActions.move,
                EGameplayInputAction.Interact => m_gameplayActions.interact,
                EGameplayInputAction.FireAbility1 => m_gameplayActions.fireAbility1,
                EGameplayInputAction.FireAbility2 => m_gameplayActions.fireAbility2,
                EGameplayInputAction.FireAbility3 => m_gameplayActions.fireAbility3,
                EGameplayInputAction.FireAbility4 => m_gameplayActions.fireAbility4,
                EGameplayInputAction.FireAbility5 => m_gameplayActions.fireAbility5,
                EGameplayInputAction.OpenGameMenu => m_gameplayActions.openGameMenu,
                EGameplayInputAction.Point => m_gameplayActions.point,
                EGameplayInputAction.Click => m_gameplayActions.click,
                EGameplayInputAction.MiddleClick => m_gameplayActions.middleClick,
                EGameplayInputAction.ScrollWheel => m_gameplayActions.scrollWheel,
                EGameplayInputAction.ToggleMovementControlMode => m_gameplayActions.toggleMovementControlMode,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private InputAction GetUIAction(EUIInputAction action)
        {
            return action switch
            {
                EUIInputAction.Submit => m_uiActions.submit,
                EUIInputAction.Cancel => m_uiActions.cancel,
                EUIInputAction.Click => m_uiActions.click,
                EUIInputAction.Navigate => m_uiActions.navigate,
                EUIInputAction.Point => m_uiActions.point,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private static void RegisterInputActionListener(InputAction action, EInputActionPhase phase, Action<InputAction.CallbackContext> listener)
        {
            switch (phase)
            {
                case EInputActionPhase.Started:
                    action.started += listener;
                    break;
                case EInputActionPhase.Performed:
                    action.performed += listener;
                    break;
                case EInputActionPhase.Canceled:
                    action.canceled += listener;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private static void UnregisterInputActionListener(InputAction action, EInputActionPhase phase, Action<InputAction.CallbackContext> listener)
        {
            switch (phase)
            {
                case EInputActionPhase.Started:
                    action.started -= listener;
                    break;
                case EInputActionPhase.Performed:
                    action.performed -= listener;
                    break;
                case EInputActionPhase.Canceled:
                    action.canceled -= listener;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private void AddExternalInputActionListener(
            InputAction action,
            EInputActionPhase phase,
            Action<InputAction.CallbackContext> listener)
        {
            RegisterInputActionListener(action, phase, listener);
            m_externalActionSubscriptions.Add(new InputActionSubscription(action, phase, listener));
        }

        private void RemoveExternalInputActionListener(
            InputAction action,
            EInputActionPhase phase,
            Action<InputAction.CallbackContext> listener)
        {
            UnregisterInputActionListener(action, phase, listener);
            for (int i = m_externalActionSubscriptions.Count - 1; i >= 0; i--)
            {
                InputActionSubscription subscription = m_externalActionSubscriptions[i];
                if (ReferenceEquals(subscription.Action, action) &&
                    subscription.Phase == phase &&
                    subscription.Listener == listener)
                {
                    m_externalActionSubscriptions.RemoveAt(i);
                    return;
                }
            }
        }

        private void ClearExternalInputActionListeners()
        {
            for (int i = m_externalActionSubscriptions.Count - 1; i >= 0; i--)
            {
                InputActionSubscription subscription = m_externalActionSubscriptions[i];
                UnregisterInputActionListener(
                    subscription.Action,
                    subscription.Phase,
                    subscription.Listener);
            }

            m_externalActionSubscriptions.Clear();
        }

        /// <summary>
        /// 保存外部监听实际订阅的 InputAction，使系统关闭时不依赖场景组件的销毁顺序也能完整解绑。
        /// </summary>
        private readonly struct InputActionSubscription
        {
            public InputActionSubscription(
                InputAction action,
                EInputActionPhase phase,
                Action<InputAction.CallbackContext> listener)
            {
                Action = action;
                Phase = phase;
                Listener = listener;
            }

            public InputAction Action { get; }

            public EInputActionPhase Phase { get; }

            public Action<InputAction.CallbackContext> Listener { get; }
        }

        private void RegisterSharedReleaseCallbacks()
        {
            m_gameplayActions.move.canceled += OnSharedActionReleased;
            m_gameplayActions.interact.canceled += OnSharedActionReleased;
            m_gameplayActions.openGameMenu.canceled += OnSharedActionReleased;
            m_gameplayActions.click.canceled += OnSharedActionReleased;
            m_gameplayActions.middleClick.canceled += OnSharedActionReleased;

            m_uiActions.navigate.canceled += OnSharedActionReleased;
            m_uiActions.submit.canceled += OnSharedActionReleased;
            m_uiActions.cancel.canceled += OnSharedActionReleased;
            m_uiActions.click.canceled += OnSharedActionReleased;
        }

        private void UnregisterSharedReleaseCallbacks()
        {
            m_gameplayActions.move.canceled -= OnSharedActionReleased;
            m_gameplayActions.interact.canceled -= OnSharedActionReleased;
            m_gameplayActions.openGameMenu.canceled -= OnSharedActionReleased;
            m_gameplayActions.click.canceled -= OnSharedActionReleased;
            m_gameplayActions.middleClick.canceled -= OnSharedActionReleased;

            m_uiActions.navigate.canceled -= OnSharedActionReleased;
            m_uiActions.submit.canceled -= OnSharedActionReleased;
            m_uiActions.cancel.canceled -= OnSharedActionReleased;
            m_uiActions.click.canceled -= OnSharedActionReleased;
        }

        private bool IsBlocked(InputAction action)
        {
            return m_actionMapReleaseGate.IsBlocked(action);
        }

        private void ArmActionMapReleaseGate(EActionMap actionMap)
        {
            m_actionMapReleaseGate.Clear();

            switch (actionMap)
            {
                case EActionMap.Gameplay:
                    // Gameplay/UI 共用方向、确认、取消与点击输入；切图层时先等按键松开，避免同一按压穿透到新 action map。
                    m_actionMapReleaseGate.ArmIfPressed(
                        m_gameplayActions.move,
                        m_gameplayActions.interact,
                        m_gameplayActions.openGameMenu,
                        m_gameplayActions.click,
                        m_gameplayActions.middleClick);
                    break;
                case EActionMap.UI:
                    m_actionMapReleaseGate.ArmIfPressed(
                        m_uiActions.navigate,
                        m_uiActions.submit,
                        m_uiActions.cancel,
                        m_uiActions.click);
                    break;
            }
        }

        private void UpdateEventSystemUiModuleGate()
        {
            EventSystem eventSystem = GameManager.EventSystem;
            if (eventSystem == null)
            {
                return;
            }

            InputSystemUIInputModule inputModule = GetRequiredEventSystemUiInputModule(eventSystem);

            if (m_currentActionMap is not (EActionMap.Gameplay or EActionMap.UI))
            {
                eventSystem.sendNavigationEvents = false;
                inputModule.enabled = false;
                return;
            }

            bool canProcessUiNavigation = !m_actionMapReleaseGate.HasBlockedActions;
            eventSystem.sendNavigationEvents =
                m_currentActionMap == EActionMap.UI && canProcessUiNavigation;
            inputModule.enabled = true;
            EnsureUiPointerActionsEnabled(inputModule);
        }

        private static void EnsureUiPointerActionsEnabled(InputSystemUIInputModule inputModule)
        {
            // Gameplay 状态也有常驻 HUD；PlayerInput 切到 Gameplay 会禁用 UI map，
            // 这里仅保留指针点击链，导航/Submit 仍由 EventSystem.sendNavigationEvents 限制。
            EnableRequiredInputAction(inputModule.point, "UI Point");
            EnableRequiredInputAction(inputModule.leftClick, "UI Click");
            EnableOptionalInputAction(inputModule.rightClick);
            EnableOptionalInputAction(inputModule.middleClick);
            EnableOptionalInputAction(inputModule.scrollWheel);
        }

        private static void EnableRequiredInputAction(InputActionReference actionReference, string displayName)
        {
            if (actionReference == null || actionReference.action == null)
            {
                throw new InvalidOperationException(
                    $"正式 UI 输入模块缺少 {displayName} 动作引用，常驻 HUD 无法接收鼠标输入。");
            }

            actionReference.action.Enable();
        }

        private static void EnableOptionalInputAction(InputActionReference actionReference)
        {
            actionReference?.action?.Enable();
        }

        /// <summary>
        /// 把 UIKit 创建的唯一 UI 输入模块接回当前玩家的正式动作资产。
        /// UIKit 负责创建 EventSystem，本系统负责保证它不会留下第二份默认输入资产。
        /// </summary>
        private InputSystemUIInputModule GetRequiredEventSystemUiInputModule(EventSystem eventSystem)
        {
            if (m_playerInput == null || m_playerInput.actions == null)
            {
                throw new InvalidOperationException("正式输入系统尚未拥有 PlayerInput 动作资产，不能启用 UI 输入。");
            }

            InputSystemUIInputModule inputModule =
                eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                throw new InvalidOperationException(
                    "正式 EventSystem 缺少 InputSystemUIInputModule；新输入系统项目不能回退到旧输入模块。");
            }

            if (m_playerInput.uiInputModule != inputModule)
            {
                m_playerInput.uiInputModule = inputModule;
            }
            if (inputModule.actionsAsset != m_playerInput.actions)
            {
                inputModule.actionsAsset = m_playerInput.actions;
            }

            return inputModule;
        }

        private void OnSceneTransitionStarted(SceneTransitionStartedEvent _)
        {
            m_playerInput.DeactivateInput();
        }

        private void OnSceneTransitionEnded(SceneTransitionEndedEvent _)
        {
            m_playerInput.ActivateInput();
        }

        private void OnControlsChanged(PlayerInput _)
        {
            NotifyControlsChanged();
        }

        private void OnSharedActionReleased(InputAction.CallbackContext context)
        {
            m_actionMapReleaseGate.NotifyReleased(context.action);
            UpdateEventSystemUiModuleGate();
        }

        private void Update()
        {
            if (!IsPointerActive(EActionMap.UI))
            {
                return;
            }

            EventSystem eventSystem = GameManager.EventSystem;
            if (!(eventSystem?.IsPointerOverGameObject() ?? false))
            {
                return;
            }

            PointerEventData pointerEventData = new(eventSystem)
            {
                position = ReadPointerScreenPosition(EActionMap.UI)
            };

            List<RaycastResult> results = new();
            eventSystem.RaycastAll(pointerEventData, results);

            foreach (RaycastResult result in results)
            {
                // 鼠标/触屏指到可选 UI 时同步当前选中项，让手柄和键鼠导航状态保持一致。
                Selectable selectable = result.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null &&
                    selectable.isActiveAndEnabled &&
                    (!selectable.targetGraphic || selectable.targetGraphic.raycastTarget))
                {
                    if (selectable.gameObject != eventSystem.currentSelectedGameObject)
                    {
                        eventSystem.SetSelectedGameObject(selectable.gameObject);
                    }

                    return;
                }

                // 已命中会吃射线的图形时停止向下穿透，避免选择到被遮挡的控件。
                Graphic graphic = result.gameObject.GetComponent<Graphic>();
                if (graphic && graphic.raycastTarget)
                {
                    break;
                }
            }
        }
    }
}
