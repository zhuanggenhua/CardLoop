using UnityEngine.InputSystem;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// Gameplay 动作引用集合。
    /// 这里只描述正式输入根对外公开的动作形状，不承载运行时路由、生命周期或绑定工具逻辑。
    /// </summary>
    public struct GameplayActions
    {
        public InputAction move;
        public InputAction interact;
        public InputAction fireAbility1;
        public InputAction fireAbility2;
        public InputAction fireAbility3;
        public InputAction fireAbility4;
        public InputAction fireAbility5;
        public InputAction openGameMenu;
        public InputAction point;
        public InputAction click;
        public InputAction toggleMovementControlMode;
    }

    /// <summary>
    /// UI 动作引用集合。
    /// 正式 UI 输入语义仍归 InputSystem 所有，这里只定义动作容器形状。
    /// </summary>
    public struct UIActions
    {
        public InputAction submit;
        public InputAction cancel;
        public InputAction click;
        public InputAction navigate;
        public InputAction point;
    }

    /// <summary>
    /// 正式输入动作图。
    /// 用于在 Gameplay、UI 和 None 三种输入上下文之间切换。
    /// </summary>
    public enum EActionMap
    {
        Gameplay,
        UI,
        None
    }

    /// <summary>
    /// Gameplay 动作图中的动作键。
    /// 这里保持与 InputSystem_Actions.inputactions 的正式动作名对齐。
    /// </summary>
    public enum EGameplayInputAction
    {
        Move,
        Interact,
        FireAbility1,
        FireAbility2,
        FireAbility3,
        FireAbility4,
        FireAbility5,
        OpenGameMenu,
        Point,
        Click,
        ToggleMovementControlMode
    }

    /// <summary>
    /// UI 动作图中的动作键。
    /// </summary>
    public enum EUIInputAction
    {
        Submit,
        Cancel,
        Click,
        Navigate,
        Point
    }

    /// <summary>
    /// UI 按键提示需要展示的输入设备图标族。
    /// 设备识别归正式输入系统所有，UI 只消费这个归类结果。
    /// </summary>
    public enum EInputControlDisplayType
    {
        Keyboard,
        XBOX,
        Playstation
    }

    /// <summary>
    /// 输入动作阶段。
    /// 统一封装 Unity Input System 的 started/performed/canceled 三态。
    /// </summary>
    public enum EInputActionPhase
    {
        Started,
        Performed,
        Canceled
    }
}
