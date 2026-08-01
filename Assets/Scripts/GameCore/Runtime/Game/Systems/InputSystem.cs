using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YokiFrame;

namespace FantasyWord.GameCore
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
        private GameplayActions m_gameplayActions;
        private UIActions m_uiActions;
        private EActionMap m_currentActionMap = EActionMap.None;
        private event System.Action m_controlsChanged;

        public override void OnSystemInit()
        {
            m_playerInput = GetComponent<PlayerInput>();
            InputActionAsset actionAsset = m_playerInput.actions;
            m_gameplayActions = CreateGameplayActions(actionAsset.FindActionMap("Gameplay"));
            m_uiActions = CreateUiActions(actionAsset.FindActionMap("UI"));
            RegisterActionAssetForBindingTools(actionAsset, InputBindingsPersistenceKey);
        }

        public override void OnSystemStart()
        {
            EventKit.Type.Register<MapTransitionStartedEvent>(OnMapTransitionStarted);
            EventKit.Type.Register<MapTransitionCompletedEvent>(OnMapTransitionCompleted);
            m_playerInput.onControlsChanged += OnControlsChanged;

            RegisterGameplayInputCallbacks();
            RegisterSharedReleaseCallbacks();

            // 输入系统先验唯一节点检查，避免正式场景继续靠后续 UI 激活才暴露第二真相。
            FormalSceneSingletonConflictDiagnostics.ReportFormalSceneSingletonConflicts($"{nameof(InputSystem)}.{nameof(OnSystemStart)}");
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<MapTransitionStartedEvent>(OnMapTransitionStarted);
            EventKit.Type.UnRegister<MapTransitionCompletedEvent>(OnMapTransitionCompleted);
            m_playerInput.onControlsChanged -= OnControlsChanged;

            UnregisterSharedReleaseCallbacks();
            UnregisterGameplayInputCallbacks();
        }

        public bool IsPointerActive(EActionMap map)
        {
            return map switch
            {
                EActionMap.Gameplay => m_gameplayActions.point.activeControl != null,
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
            return map switch
            {
                EActionMap.Gameplay => m_gameplayActions.point.ReadValue<Vector2>(),
                EActionMap.UI => m_uiActions.point.ReadValue<Vector2>(),
                _ => Vector2.zero
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
        /// 向 Gameplay 动作注册监听。
        /// 运行时代码必须通过本系统订阅输入，避免把 InputAction 订阅逻辑散落成第二输入入口。
        /// </summary>
        public void AddGameplayActionListener(EGameplayInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            RegisterInputActionListener(GetGameplayAction(action), phase, listener);
        }

        public void RemoveGameplayActionListener(EGameplayInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            UnregisterInputActionListener(GetGameplayAction(action), phase, listener);
        }

        public void AddUIActionListener(EUIInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            RegisterInputActionListener(GetUIAction(action), phase, listener);
        }

        public void RemoveUIActionListener(EUIInputAction action, EInputActionPhase phase, System.Action<InputAction.CallbackContext> listener)
        {
            UnregisterInputActionListener(GetUIAction(action), phase, listener);
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
            return GetGameplayAction(action).IsInProgress();
        }

        public bool IsUIActionPressed(EUIInputAction action)
        {
            return GetUIAction(action).IsInProgress();
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

        private bool TryResolveLocalPlayerCommandContext(out GameCommandContext commandContext)
        {
            if (!GameManager.PlayerSystem.TryGetCurrentInputTarget(out IPlayerInputTarget inputTarget))
            {
                commandContext = GameCommandContext.LocalPlayer(null);
                return false;
            }

            inputTarget.TryGetControlledCharacter(out CharacterBase controlledCharacter);
            commandContext = GameCommandContext.LocalPlayer(controlledCharacter);
            return true;
        }

        private PlayerCommandResult ExecuteLocalPlayerCommand(
            EPlayerCommandKind kind,
            Vector2 direction = default,
            Vector2? worldPosition = null,
            int abilityIndex = -1,
            CharacterBase targetCharacter = null,
            GameObject interactionTarget = null)
        {
            if (!TryResolveLocalPlayerCommandContext(out GameCommandContext commandContext))
            {
                PlayerCommandResult missingTargetResult = PlayerCommandResult.Failed(
                    new PlayerCommandRequest(
                        GameCommandContext.LocalPlayer(null),
                        kind,
                        direction,
                        worldPosition,
                        abilityIndex,
                        targetCharacter,
                        interactionTarget),
                    EPlayerCommandFailureReason.MissingInputTarget);
                NotifyLocalPlayerCommandResult(missingTargetResult);
                return missingTargetResult;
            }

            PlayerCommandResult result = GameManager.PlayerSystem.SubmitPlayerCommand(
                new PlayerCommandRequest(
                    commandContext,
                    kind,
                    direction,
                    worldPosition,
                    abilityIndex,
                    targetCharacter,
                    interactionTarget));
            NotifyLocalPlayerCommandResult(result);
            return result;
        }

        private static void NotifyLocalPlayerCommandResult(PlayerCommandResult result)
        {
            if (!result.Succeeded)
            {
                GameRuntimeEvents.NotifyLocalPlayerCommandFailed(result);
            }
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

        private void RegisterGameplayInputCallbacks()
        {
            m_gameplayActions.interact.performed += OnInteractPerformed;
            m_gameplayActions.fireAbility1.performed += OnFireAbility1Performed;
            m_gameplayActions.fireAbility2.performed += OnFireAbility2Performed;
            m_gameplayActions.fireAbility3.performed += OnFireAbility3Performed;
            m_gameplayActions.fireAbility4.performed += OnFireAbility4Performed;
            m_gameplayActions.fireAbility5.performed += OnFireAbility5Performed;
            m_gameplayActions.fireAbility1.canceled += OnFireAbility1Canceled;
            m_gameplayActions.fireAbility2.canceled += OnFireAbility2Canceled;
            m_gameplayActions.fireAbility3.canceled += OnFireAbility3Canceled;
            m_gameplayActions.fireAbility4.canceled += OnFireAbility4Canceled;
            m_gameplayActions.fireAbility5.canceled += OnFireAbility5Canceled;
            m_gameplayActions.move.performed += OnMovePerformed;
            m_gameplayActions.move.canceled += OnMoveCanceled;
            m_gameplayActions.click.performed += OnClickPerformed;
            m_gameplayActions.toggleMovementControlMode.performed += OnToggleMovementControlModePerformed;
            m_gameplayActions.openGameMenu.performed += OnOpenGameMenuPerformed;
        }

        private void UnregisterGameplayInputCallbacks()
        {
            m_gameplayActions.interact.performed -= OnInteractPerformed;
            m_gameplayActions.fireAbility1.performed -= OnFireAbility1Performed;
            m_gameplayActions.fireAbility2.performed -= OnFireAbility2Performed;
            m_gameplayActions.fireAbility3.performed -= OnFireAbility3Performed;
            m_gameplayActions.fireAbility4.performed -= OnFireAbility4Performed;
            m_gameplayActions.fireAbility5.performed -= OnFireAbility5Performed;
            m_gameplayActions.fireAbility1.canceled -= OnFireAbility1Canceled;
            m_gameplayActions.fireAbility2.canceled -= OnFireAbility2Canceled;
            m_gameplayActions.fireAbility3.canceled -= OnFireAbility3Canceled;
            m_gameplayActions.fireAbility4.canceled -= OnFireAbility4Canceled;
            m_gameplayActions.fireAbility5.canceled -= OnFireAbility5Canceled;
            m_gameplayActions.move.performed -= OnMovePerformed;
            m_gameplayActions.move.canceled -= OnMoveCanceled;
            m_gameplayActions.click.performed -= OnClickPerformed;
            m_gameplayActions.toggleMovementControlMode.performed -= OnToggleMovementControlModePerformed;
            m_gameplayActions.openGameMenu.performed -= OnOpenGameMenuPerformed;
        }

        private void RegisterSharedReleaseCallbacks()
        {
            m_gameplayActions.move.canceled += OnSharedActionReleased;
            m_gameplayActions.interact.canceled += OnSharedActionReleased;
            m_gameplayActions.openGameMenu.canceled += OnSharedActionReleased;
            m_gameplayActions.click.canceled += OnSharedActionReleased;

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
                        m_gameplayActions.click);
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
            if (GameManager.EventSystem == null)
            {
                return;
            }

            BaseInputModule inputModule = GameManager.EventSystem.GetComponent<BaseInputModule>();
            if (inputModule == null)
            {
                return;
            }

            if (m_currentActionMap != EActionMap.UI)
            {
                GameManager.EventSystem.sendNavigationEvents = false;
                inputModule.enabled = false;
                return;
            }

            bool canProcessUiInputs = !m_actionMapReleaseGate.HasBlockedActions;
            GameManager.EventSystem.sendNavigationEvents = canProcessUiInputs;
            inputModule.enabled = canProcessUiInputs;
        }

        private void OnMapTransitionStarted(MapTransitionStartedEvent _)
        {
            m_playerInput.DeactivateInput();
        }

        private void OnMapTransitionCompleted(MapTransitionCompletedEvent _)
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

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (IsBlocked(context.action))
            {
                return;
            }

            ExecuteLocalPlayerCommand(EPlayerCommandKind.Interact);
        }

        private void OnOpenGameMenuPerformed(InputAction.CallbackContext context)
        {
            if (IsBlocked(context.action))
            {
                return;
            }

            ExecuteLocalPlayerCommand(EPlayerCommandKind.OpenGameMenu);
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            if (IsBlocked(context.action))
            {
                return;
            }

            ExecuteLocalPlayerCommand(EPlayerCommandKind.Move, direction: context.ReadValue<Vector2>());
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            ExecuteLocalPlayerCommand(EPlayerCommandKind.StopMove);
        }

        private void OnClickPerformed(InputAction.CallbackContext context)
        {
            if (IsBlocked(context.action))
            {
                return;
            }

            if (!TryResolveGameplayPointerWorldPosition(out Vector2 worldPosition))
            {
                return;
            }

            ExecuteLocalPlayerCommand(
                EPlayerCommandKind.ClickMove,
                worldPosition: worldPosition);
        }

        private void OnToggleMovementControlModePerformed(InputAction.CallbackContext context)
        {
            ExecuteLocalPlayerCommand(EPlayerCommandKind.ToggleMovementControlMode);
        }

        private void OnFireAbility1Performed(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.FireAbility, abilityIndex: 0);
        private void OnFireAbility2Performed(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.FireAbility, abilityIndex: 1);
        private void OnFireAbility3Performed(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.FireAbility, abilityIndex: 2);
        private void OnFireAbility4Performed(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.FireAbility, abilityIndex: 3);
        private void OnFireAbility5Performed(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.FireAbility, abilityIndex: 4);
        private void OnFireAbility1Canceled(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.StopFireAbility, abilityIndex: 0);
        private void OnFireAbility2Canceled(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.StopFireAbility, abilityIndex: 1);
        private void OnFireAbility3Canceled(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.StopFireAbility, abilityIndex: 2);
        private void OnFireAbility4Canceled(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.StopFireAbility, abilityIndex: 3);
        private void OnFireAbility5Canceled(InputAction.CallbackContext context) => ExecuteLocalPlayerCommand(EPlayerCommandKind.StopFireAbility, abilityIndex: 4);

        private bool TryResolveGameplayPointerWorldPosition(out Vector2 worldPosition)
        {
            Camera camera = GameManager.MainCamera;
            CharacterBase controlledCharacter = GameManager.PlayerSystem.GetCurrentControlledCharacterOrPlayerInstance();
            Vector2 screenPosition = ReadPointerScreenPosition(EActionMap.Gameplay);
            if (camera == null ||
                controlledCharacter == null ||
                UIPointerUtility.IsPositionOverUI(screenPosition))
            {
                worldPosition = default;
                return false;
            }

            float distanceToSubjectPlane = controlledCharacter.transform.position.z - camera.transform.position.z;
            Vector3 screenPoint = new(screenPosition.x, screenPosition.y, distanceToSubjectPlane);
            worldPosition = camera.ScreenToWorldPoint(screenPoint);
            return true;
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
