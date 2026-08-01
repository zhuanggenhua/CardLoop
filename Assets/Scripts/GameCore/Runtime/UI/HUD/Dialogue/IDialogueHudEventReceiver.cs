namespace FantasyWord.GameCore
{
    /// <summary>
    /// 对话 HUD 内部组件与父级界面之间的正式回调合同。
    /// 只服务 `UIDialogue` 闭包，用来替代消息框与选项按钮对父级的字符串上行消息。
    /// </summary>
    public interface IDialogueHudEventReceiver
    {
        void HandleMessageBoxTextAnimationFinished();
        void HandleDialogueOptionClicked(int option);
    }
}
