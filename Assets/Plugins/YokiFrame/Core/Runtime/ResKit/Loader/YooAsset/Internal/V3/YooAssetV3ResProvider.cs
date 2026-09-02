#if YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// YooAsset 3.x 资源提供者。
    /// 使用 ResourcePackage 实例 API。
    /// 文件内零条件编译 — 纯 V3 代码。
    /// </summary>
    internal sealed class YooAssetV3ResProvider : IYooAssetResProvider
#if YOKIFRAME_UNITASK_SUPPORT
        , IYooAssetResUniTaskProvider
#endif
    {
        private readonly ResourcePackage mPackage;
        private AssetHandle mHandle;
        private AllAssetsHandle mAllHandle;
        private SubAssetsHandle mSubHandle;

        public YooAssetV3ResProvider(ResourcePackage package)
            => mPackage = package ?? throw new ArgumentNullException(nameof(package));

        // ──────────── Sync ────────────

        public T LoadAsset<T>(string path) where T : Object
        {
            mHandle = mPackage.LoadAssetSync<T>(path);
            return mHandle.GetAssetObject<T>();
        }

        public T[] LoadAllAssets<T>(string path) where T : Object
        {
            mAllHandle = mPackage.LoadAllAssetsSync<T>(path);
            if (mAllHandle.Status != EOperationStatus.Succeeded)
                return Array.Empty<T>();
            return ConvertAll<T>(mAllHandle.AllAssetObjects);
        }

        public SubAssetsResult<T> LoadSubAssets<T>(string path) where T : Object
        {
            mSubHandle = mPackage.LoadSubAssetsSync<T>(path);
            if (mSubHandle.Status != EOperationStatus.Succeeded)
                return default;
            return ConvertSub<T>(mSubHandle.SubAssetObjects);
        }

        // ──────────── Async Callback ────────────

        public void LoadAssetAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            mHandle = mPackage.LoadAssetAsync<T>(path);
            mHandle.Completed += h => onComplete?.Invoke(h.GetAssetObject<T>());
        }

        public void LoadAllAssetsAsync<T>(string path, Action<T[]> onComplete) where T : Object
        {
            mAllHandle = mPackage.LoadAllAssetsAsync<T>(path);
            mAllHandle.Completed += h =>
            {
                if (h.Status != EOperationStatus.Succeeded) { onComplete?.Invoke(Array.Empty<T>()); return; }
                onComplete?.Invoke(ConvertAll<T>(h.AllAssetObjects));
            };
        }

        public void LoadSubAssetsAsync<T>(string path, Action<SubAssetsResult<T>> onComplete) where T : Object
        {
            mSubHandle = mPackage.LoadSubAssetsAsync<T>(path);
            mSubHandle.Completed += h =>
            {
                if (h.Status != EOperationStatus.Succeeded) { onComplete?.Invoke(default); return; }
                onComplete?.Invoke(ConvertSub<T>(h.SubAssetObjects));
            };
        }

        // ──────────── Cleanup ────────────

        public void ReleaseHandles()
        {
            mHandle?.Release();
            mAllHandle?.Release();
            mSubHandle?.Release();
            mHandle = null;
            mAllHandle = null;
            mSubHandle = null;
        }

        // ──────────── Converters ────────────

        private static T[] ConvertAll<T>(System.Collections.Generic.IReadOnlyList<Object> objects) where T : Object
        {
            var result = new T[objects.Count];
            for (int i = 0; i < objects.Count; i++)
                result[i] = objects[i] as T;
            return result;
        }

        private static SubAssetsResult<T> ConvertSub<T>(System.Collections.Generic.IReadOnlyList<Object> objects) where T : Object
        {
            T main = null;
            var subList = new System.Collections.Generic.List<T>(objects.Count);
            foreach (var obj in objects)
            {
                if (obj is T typed) { main ??= typed; subList.Add(typed); }
            }
            return new SubAssetsResult<T>(main, subList.ToArray());
        }

#if YOKIFRAME_UNITASK_SUPPORT
        // ──────────── UniTask ────────────

        public async UniTask<T> LoadAssetUniTaskAsync<T>(string path, CancellationToken ct) where T : Object
        {
            AssetHandle handle = mPackage.LoadAssetAsync<T>(path);
            mHandle = handle;
            await handle.ToUniTask(cancellationToken: ct);
            EnsureHandleReadable(handle, path, ct);
            if (handle.Status != EOperationStatus.Succeeded)
                return null;
            return handle.GetAssetObject<T>();
        }

        public async UniTask<T[]> LoadAllAssetsUniTaskAsync<T>(string path, CancellationToken ct) where T : Object
        {
            AllAssetsHandle handle = mPackage.LoadAllAssetsAsync<T>(path);
            mAllHandle = handle;
            await handle.ToUniTask(cancellationToken: ct);
            EnsureHandleReadable(handle, path, ct);
            if (handle.Status != EOperationStatus.Succeeded)
                return Array.Empty<T>();
            return ConvertAll<T>(handle.AllAssetObjects);
        }

        public async UniTask<SubAssetsResult<T>> LoadSubAssetsUniTaskAsync<T>(string path, CancellationToken ct) where T : Object
        {
            SubAssetsHandle handle = mPackage.LoadSubAssetsAsync<T>(path);
            mSubHandle = handle;
            await handle.ToUniTask(cancellationToken: ct);
            EnsureHandleReadable(handle, path, ct);
            if (handle.Status != EOperationStatus.Succeeded)
                return default;
            return ConvertSub<T>(handle.SubAssetObjects);
        }

        private static void EnsureHandleReadable(HandleBase handle, string path, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (handle == null || !handle.IsValid)
            {
                throw new InvalidOperationException($"YooAsset 资源句柄已失效，不能读取加载结果：{path}。");
            }
        }
#endif
    }
}
#endif
