#nullable enable

using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace UnityAiBridge.Editor
{
    /// <summary>
    /// 共享的文件工具方法。
    /// </summary>
    internal static class BridgeFileUtils
    {
        private const int AtomicWriteRetryCount = 6;
        private const int AtomicWriteRetryDelayMilliseconds = 20;
        private const int VolatileWriteRetryCount = 6;
        private const int VolatileWriteRetryDelayMilliseconds = 10;

        /// <summary>
        /// 原子写入文件：先写 .tmp 再 rename，避免读到半写文件。
        /// </summary>
        internal static void WriteAtomically(string targetPath, string content)
        {
            var tmpPath = targetPath + ".tmp";
            try
            {
                File.WriteAllText(tmpPath, content);
                TryCommitAtomicallyWithRetry(tmpPath, targetPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BridgeFileUtils] Atomic write failed, falling back to direct write: {ex.Message}");
                // 兜底：直接写入
                try { File.WriteAllText(targetPath, content); }
                catch (Exception ex2) { Debug.LogWarning($"[BridgeFileUtils] Fallback write also failed: {ex2.Message}"); }
                TryDeleteFileQuietly(tmpPath);
            }
        }

        /// <summary>
        /// 高频快照文件直接覆盖写，避免 Windows 下 Replace/Delete 竞争持续打告警。
        /// 用于 heartbeat 这类允许短暂非原子窗口的状态文件。
        /// </summary>
        internal static void WriteVolatileSnapshot(string targetPath, string content)
        {
            Exception? lastException = null;
            for (int attempt = 0; attempt < VolatileWriteRetryCount; attempt++)
            {
                try
                {
                    using var stream = new FileStream(
                        targetPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var writer = new StreamWriter(stream);
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(flushToDisk: false);
                    return;
                }
                catch (Exception ex) when (IsRetryableAtomicWriteException(ex) && attempt + 1 < VolatileWriteRetryCount)
                {
                    lastException = ex;
                    Thread.Sleep(VolatileWriteRetryDelayMilliseconds * (attempt + 1));
                }
            }

            if (lastException != null)
            {
                Debug.LogWarning($"[BridgeFileUtils] Volatile snapshot write failed after retries: {lastException.Message}");
                return;
            }

            try
            {
                using var stream = new FileStream(
                    targetPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                using var writer = new StreamWriter(stream);
                writer.Write(content);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BridgeFileUtils] Volatile snapshot write failed: {ex.Message}");
            }
        }

        private static void TryCommitAtomicallyWithRetry(string tmpPath, string targetPath)
        {
            Exception? lastException = null;
            for (int attempt = 0; attempt < AtomicWriteRetryCount; attempt++)
            {
                try
                {
                    CommitAtomically(tmpPath, targetPath);
                    return;
                }
                catch (Exception ex) when (IsRetryableAtomicWriteException(ex) && attempt + 1 < AtomicWriteRetryCount)
                {
                    lastException = ex;
                    Thread.Sleep(AtomicWriteRetryDelayMilliseconds * (attempt + 1));
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }

            CommitAtomically(tmpPath, targetPath);
        }

        private static void CommitAtomically(string tmpPath, string targetPath)
        {
#if UNITY_EDITOR_WIN
            // Windows: File.Move 不能覆盖，用 File.Replace；目标不存在时退回 Move。
            if (File.Exists(targetPath))
            {
                File.Replace(tmpPath, targetPath, null);
                return;
            }

            File.Move(tmpPath, targetPath);
#else
            // Unix: 先删再 Move（rename(2) 本身支持覆盖，但 Mono 的 File.Move 不支持）
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            File.Move(tmpPath, targetPath);
#endif
        }

        private static bool IsRetryableAtomicWriteException(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException;
        }

        private static void TryDeleteFileQuietly(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BridgeFileUtils] Failed to cleanup tmp file: {ex.Message}");
            }
        }
    }
}
