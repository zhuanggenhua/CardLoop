using System.Collections.Generic;
using GameCore;

namespace Gameplay.Content
{
    /// <summary>
    /// 持有进程级 Gameplay 内容加载句柄，并从已加载作者资产建立唯一内容 ID 索引。
    /// 资源发现归 ResourceSystem，索引构建归 ContentIndex，本系统只负责二者的生命周期接入。
    /// </summary>
    public sealed class ContentRegistrySystem : AGameSystem
    {
        /// <summary>
        /// YooAsset Collector 给正式 Gameplay 内容资产使用的收集标签。
        /// 它只是资源发现条件，不是内容类型、内容 ID 或 EX-GAS 标签。
        /// </summary>
        public const string YooAssetContentTag = "gameplay-content";

        private ResourceHandle<IList<ContentAsset>> m_contentHandle;

        /// <summary>
        /// 当前进程已加载内容的只读运行时索引；系统关闭后恢复为空。
        /// </summary>
        public ContentIndex Index { get; private set; }

        /// <summary>内容资源句柄和唯一 ID 索引是否已成功建立。</summary>
        public bool IsInitialized => Index != null;

        /// <summary>
        /// 通过 ResourceSystem 按 YooAsset 标签加载全部作者资产，并在 GameManager 初始化阶段同步建立索引。
        /// 加载或校验失败时释放临时句柄并让启动失败显式向上传播。
        /// </summary>
        public override void OnSystemInit()
        {
            if (IsInitialized)
            {
                return;
            }

            ResourceHandle<IList<ContentAsset>> handle =
                ResourceSystem.LoadAssetsByAssetTagAsync<ContentAsset>(YooAssetContentTag);
            try
            {
                IList<ContentAsset> contentAssets = handle.WaitForCompletion();
                Index = ContentIndex.Build(contentAssets);
                m_contentHandle = handle;
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 清空派生索引并释放本系统持有的批量资源句柄，使场景或进程重启不会保留旧内容引用。
        /// </summary>
        public override void OnSystemShutdown()
        {
            Index = null;
            m_contentHandle.Dispose();
            m_contentHandle = default;
        }
    }
}
