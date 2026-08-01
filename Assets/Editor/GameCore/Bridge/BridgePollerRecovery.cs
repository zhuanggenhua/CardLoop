#nullable enable

using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// AIBridge 域重载后的轮询恢复钩子。
    /// 这里只负责把包内正式轮询链重新挂回编辑器生命周期，不引入第二套桥接状态机。
    /// </summary>
    [InitializeOnLoad]
    public static class BridgePollerRecovery
    {
        private const double RetryDelaySeconds = 2.0;

        private static readonly string BridgeRoot =
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Temp", "UnityBridge");

        private static readonly string CommandsDir = Path.Combine(BridgeRoot, "commands");
        private static readonly string HeartbeatFile = Path.Combine(BridgeRoot, "heartbeat");

        private static readonly Type? FileBridgePollerType =
            Type.GetType("UnityAiBridge.Editor.FileBridgePoller, AiBridge.Unity.Editor");

        private static readonly Type? BridgePluginType =
            Type.GetType("UnityAiBridge.Editor.BridgePlugin, AiBridge.Unity.Editor");

        private static readonly MethodInfo? FileBridgePollerUpdateMethod =
            FileBridgePollerType?.GetMethod("OnEditorUpdate", BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo? BridgePluginEnsureInitializedMethod =
            BridgePluginType?.GetMethod("EnsureInitialized", BindingFlags.Public | BindingFlags.Static);

        private static readonly EditorApplication.CallbackFunction? FileBridgePollerUpdateCallback =
            FileBridgePollerUpdateMethod == null
                ? null
                : (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                    typeof(EditorApplication.CallbackFunction),
                    FileBridgePollerUpdateMethod);

        private static double s_nextRetryAt;
        private static bool s_retryHooked;

        static BridgePollerRecovery()
        {
            EditorApplication.delayCall += EnsureBridgePoller;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [InitializeOnLoadMethod]
        private static void EnsureBridgePoller()
        {
            if (!CanTouchBridge())
            {
                ScheduleRetry();
                return;
            }

            if (!TryRebindBridgePoller())
            {
                ScheduleRetry();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += EnsureBridgePoller;
            }
        }

        private static bool CanTouchBridge()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess())
            {
                return false;
            }

            return !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling
                && !EditorApplication.isUpdating;
        }

        private static void ScheduleRetry()
        {
            s_nextRetryAt = Math.Max(s_nextRetryAt, EditorApplication.timeSinceStartup + RetryDelaySeconds);

            if (s_retryHooked)
            {
                return;
            }

            s_retryHooked = true;
            EditorApplication.update += RetryEnsureBridgePoller;
        }

        private static void RetryEnsureBridgePoller()
        {
            if (EditorApplication.timeSinceStartup < s_nextRetryAt)
            {
                return;
            }

            EditorApplication.update -= RetryEnsureBridgePoller;
            s_retryHooked = false;
            EnsureBridgePoller();
        }

        private static bool TryRebindBridgePoller()
        {
            if (FileBridgePollerType == null || BridgePluginType == null || FileBridgePollerUpdateCallback == null)
            {
                Debug.LogWarning("[BridgePollerRecovery] AIBridge editor types are unavailable. Recovery skipped.");
                return false;
            }

            try
            {
                // 先确保包内静态构造链跑过，避免只补 update 回调却没初始化工具注册表。
                RuntimeHelpers.RunClassConstructor(BridgePluginType.TypeHandle);
                RuntimeHelpers.RunClassConstructor(FileBridgePollerType.TypeHandle);
                BridgePluginEnsureInitializedMethod?.Invoke(null, null);

                // EditorApplication.update 是 multicast delegate；先卸再挂，确保恢复幂等。
                EditorApplication.update -= FileBridgePollerUpdateCallback;
                EditorApplication.update += FileBridgePollerUpdateCallback;

                // 如果桥接心跳已经停掉或命令在积压，立刻补一次轮询，不等下一帧。
                if (HasPendingCommands() || IsHeartbeatStale())
                {
                    FileBridgePollerUpdateMethod!.Invoke(null, null);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[BridgePollerRecovery] Failed to rebind AIBridge poller: {exception.Message}");
                return false;
            }
        }

        private static bool HasPendingCommands()
        {
            return Directory.Exists(CommandsDir) && Directory.GetFiles(CommandsDir, "*.json").Length > 0;
        }

        private static bool IsHeartbeatStale()
        {
            if (!File.Exists(HeartbeatFile))
            {
                return true;
            }

            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(HeartbeatFile);
            return age.TotalSeconds > RetryDelaySeconds * 2.0;
        }
    }
}
