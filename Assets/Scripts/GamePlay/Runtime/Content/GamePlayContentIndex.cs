using System.Collections.Generic;

namespace GamePlay
{
    /// <summary>
    /// 从已加载内容资产派生的运行时查询索引。它不拥有作者数据，也不保存第二套内容身份。
    /// </summary>
    public sealed class GamePlayContentIndex
    {
        private readonly Dictionary<GamePlayContentId, GamePlayContentAsset> m_byId;

        private GamePlayContentIndex(
            IReadOnlyList<GamePlayContentAsset> allAssets,
            Dictionary<GamePlayContentId, GamePlayContentAsset> byId)
        {
            AllAssets = allAssets;
            m_byId = byId;
        }

        /// <summary>
        /// 通过校验的全部内容资产，顺序与构建索引时的输入顺序一致。
        /// </summary>
        public IReadOnlyList<GamePlayContentAsset> AllAssets { get; }

        /// <summary>
        /// 当前可通过唯一内容 ID 查询的资产数量。
        /// </summary>
        public int Count => m_byId.Count;

        /// <summary>
        /// 复制并校验已加载的作者资产，然后建立精确内容 ID 索引。
        /// 发现空引用、无效 ID 或重复 ID 时抛出异常；本索引不接管资源句柄生命周期。
        /// </summary>
        public static GamePlayContentIndex Build(IEnumerable<GamePlayContentAsset> contentAssets)
        {
            var assets = new List<GamePlayContentAsset>();
            if (contentAssets != null)
            {
                foreach (GamePlayContentAsset contentAsset in contentAssets)
                {
                    assets.Add(contentAsset);
                }
            }

            GamePlayContentValidationReport report = GamePlayContentValidator.ValidateContentAssets(assets);
            report.ThrowIfHasErrors();

            var byId = new Dictionary<GamePlayContentId, GamePlayContentAsset>();

            for (int i = 0; i < assets.Count; i++)
            {
                GamePlayContentAsset asset = assets[i];
                if (asset == null)
                {
                    continue;
                }

                byId.Add(asset.ContentId, asset);
            }

            return new GamePlayContentIndex(assets, byId);
        }

        /// <summary>
        /// 按唯一内容 ID 精确查询资产。无效或不存在的 ID 返回 false，不执行标签或资源地址匹配。
        /// </summary>
        public bool TryGet(GamePlayContentId contentId, out GamePlayContentAsset contentAsset)
        {
            return m_byId.TryGetValue(contentId, out contentAsset);
        }

        /// <summary>
        /// 按唯一内容 ID 精确查询指定作者源类型；ID 存在但类型不匹配时同样返回 false。
        /// </summary>
        public bool TryGet<TAsset>(GamePlayContentId contentId, out TAsset contentAsset)
            where TAsset : GamePlayContentAsset
        {
            if (TryGet(contentId, out GamePlayContentAsset found) && found is TAsset typed)
            {
                contentAsset = typed;
                return true;
            }

            contentAsset = null;
            return false;
        }
    }
}
