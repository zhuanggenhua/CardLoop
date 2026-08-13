using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 进程级技术场景切换入口。
    /// 它只编排淡出、SceneKit 加载、淡入和场景生命周期事件，不保存剧本、地图、检查点或角色位置。
    /// </summary>
    public sealed class SceneSystem : AGameSystem
    {
        private static readonly IReadOnlyCollection<Type> SystemStartupDependencies =
            new[] { typeof(TransitionSystem) };

        private bool m_transitionInProgress;

        public override IReadOnlyCollection<Type> StartupDependencies => SystemStartupDependencies;

        /// <summary>当前由 SceneKit 管理的活动场景地址；编辑器直接打开而未交给 SceneKit 的场景不伪装成已跟踪地址。</summary>
        public string CurrentSceneAddress => GetCurrentSceneHandler()?.SceneName ?? string.Empty;

        public bool HasCurrentScene => GetCurrentSceneHandler() != null;

        public bool IsTransitioning => m_transitionInProgress;

        public override void OnSystemShutdown()
        {
            m_transitionInProgress = false;
        }

        /// <summary>
        /// 发起一次完整的技术场景切换。场景业务由调用方在成功加载回调中继续完成，
        /// 例如后续的 ScenarioRun 组合场景，而不是由本系统保存业务状态。
        /// </summary>
        public void TransitionTo(
            string sceneAddress,
            Action onSceneLoaded = null,
            Action onCompletion = null)
        {
            TransitionToAsync(sceneAddress, onSceneLoaded, onCompletion).Forget(
                exception => Debug.LogException(
                    new InvalidOperationException($"场景切换失败：{sceneAddress}", exception),
                    this));
        }

        /// <summary>
        /// 异步执行淡出、场景替换和淡入。
        /// `SceneTransitionCompletedEvent` 只在成功完成后发送；无论成功、失败或取消，
        /// 都会在 finally 中发送 `SceneTransitionEndedEvent`，用于解除输入锁定等清理职责。
        /// </summary>
        public async UniTask TransitionToAsync(
            string sceneAddress,
            Action onSceneLoaded = null,
            Action onCompletion = null)
        {
            if (m_transitionInProgress)
            {
                throw new InvalidOperationException("已有场景切换正在执行，不能并发开始第二次切换。");
            }

            string targetSceneAddress = sceneAddress ?? string.Empty;
            if (string.IsNullOrEmpty(targetSceneAddress))
            {
                EventKit.Type.Send(new SceneLoadedEvent());
                onSceneLoaded?.Invoke();
                onCompletion?.Invoke();
                return;
            }

            TransitionSystem transitionSystem = GetRequiredTransitionSystem();
            SceneHandler currentSceneHandler = GetCurrentSceneHandler();
            bool hasCurrentScene = currentSceneHandler != null;
            bool isSameScene = hasCurrentScene &&
                               string.Equals(currentSceneHandler.SceneName, targetSceneAddress, StringComparison.Ordinal);

            m_transitionInProgress = true;
            EventKit.Type.Send(new SceneTransitionStartedEvent());
            try
            {
                await transitionSystem.FadeOutUniTaskAsync(destroyCancellationToken);

                if (!isSameScene)
                {
                    if (hasCurrentScene)
                    {
                        EventKit.Type.Send(new SceneUnloadingEvent());
                    }

                    EventKit.Type.Send(new SceneLoadingEvent());
                    // SceneKit 的取消令牌只能中断等待，不能停止底层 YooAsset 场景加载。
                    // 这里必须等待真实加载完成，避免调用方已取消但资源句柄仍无 owner 收口。
                    SceneHandler loadedScene = await SceneKit.LoadSceneUniTaskAsync(
                        targetSceneAddress,
                        SceneLoadMode.Single);
                    EnsureActiveScene(targetSceneAddress, loadedScene);

                    if (hasCurrentScene)
                    {
                        EventKit.Type.Send(new SceneUnloadedEvent());
                    }
                }

                EventKit.Type.Send(new SceneLoadedEvent());
                onSceneLoaded?.Invoke();
                await transitionSystem.FadeInUniTaskAsync(destroyCancellationToken);
                EventKit.Type.Send(new SceneTransitionCompletedEvent());
                onCompletion?.Invoke();
            }
            finally
            {
                try
                {
                    if (transitionSystem.IsTransitioning)
                    {
                        await transitionSystem.FadeInUniTaskAsync(destroyCancellationToken);
                    }
                }
                finally
                {
                    m_transitionInProgress = false;
                    EventKit.Type.Send(new SceneTransitionEndedEvent());
                }
            }
        }

        private static void EnsureActiveScene(string sceneAddress, SceneHandler sceneHandler)
        {
            if (sceneHandler == null ||
                !sceneHandler.Scene.IsValid() ||
                !sceneHandler.Scene.isLoaded ||
                SceneManager.GetActiveScene() != sceneHandler.Scene ||
                !string.Equals(sceneHandler.SceneName, sceneAddress, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"SceneKit 没有返回有效的活动场景：{sceneAddress}");
            }
        }

        private static SceneHandler GetCurrentSceneHandler()
        {
            SceneHandler handler = SceneKit.GetActiveSceneHandler();
            return handler != null &&
                   handler.State == SceneState.Loaded &&
                   handler.Scene.IsValid() &&
                   handler.Scene.isLoaded
                ? handler
                : null;
        }

        private static TransitionSystem GetRequiredTransitionSystem()
        {
            TransitionSystem transitionSystem = GameManager.TransitionSystem;
            if (transitionSystem == null || !transitionSystem.isActiveAndEnabled)
            {
                throw new InvalidOperationException(
                    $"场景切换需要一个启用的 {nameof(TransitionSystem)}。");
            }

            return transitionSystem;
        }
    }
}
