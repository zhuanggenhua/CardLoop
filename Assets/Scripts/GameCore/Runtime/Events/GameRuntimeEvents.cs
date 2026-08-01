using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// GameCore 领域事件发送入口。
    /// 这里保持唯一正式发布入口，具体事件类型与分域入口拆到同目录并列 partial 文件中。
    /// </summary>
    public static partial class GameRuntimeEvents
    {
        private static void Publish<TEvent>(TEvent runtimeEvent) where TEvent : struct
        {
            EventKit.Type.Send(runtimeEvent);
        }
    }
}
