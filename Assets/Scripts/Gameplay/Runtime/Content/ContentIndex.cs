using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Gameplay.Content
{
	/// <summary>
	/// 由当前活动内容集合构建的只读索引，按唯一内容 ID 提供类型安全查询。
	/// </summary>
	public sealed class ContentIndex
	{
		private readonly Dictionary<ContentId, ContentAsset> m_byId;

		public IReadOnlyList<ContentAsset> AllAssets { get; }

		public int Count => m_byId.Count;

		private ContentIndex(ReadOnlyCollection<ContentAsset> allAssets, Dictionary<ContentId, ContentAsset> byId)
		{
			AllAssets = allAssets;
			m_byId = byId;
		}

		public static ContentIndex Build(IEnumerable<ContentAsset> contentAssets)
		{
			List<ContentAsset> assets = new List<ContentAsset>();
			if (contentAssets != null)
			{
				foreach (ContentAsset contentAsset in contentAssets)
				{
					assets.Add(contentAsset);
				}
			}
			ContentValidationReport report = ContentValidator.ValidateContentAssets(assets);
			report.ThrowIfHasErrors();
			Dictionary<ContentId, ContentAsset> byId = new Dictionary<ContentId, ContentAsset>();
			for (int i = 0; i < assets.Count; i++)
			{
				ContentAsset asset = assets[i];
				if (!(asset == null))
				{
					byId.Add(asset.ContentId, asset);
				}
			}
			return new ContentIndex(assets.AsReadOnly(), byId);
		}

		public bool TryGet(ContentId contentId, out ContentAsset contentAsset)
		{
			return m_byId.TryGetValue(contentId, out contentAsset);
		}

		public bool TryGet<TAsset>(ContentId contentId, out TAsset contentAsset) where TAsset : ContentAsset
		{
			if (TryGet(contentId, out var found) && found is TAsset typed)
			{
				contentAsset = typed;
				return true;
			}
			contentAsset = null;
			return false;
		}

		/// <summary>
		/// 创建当前单局冻结内容集合的稳定身份快照；序列化结果不依赖资源或 Mod 的加载顺序。
		/// </summary>
		public ContentSetSnapshot CreateSnapshot()
		{
			List<ContentId> contentIds = new List<ContentId>(m_byId.Keys);
			contentIds.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
			return new ContentSetSnapshot(contentIds);
		}

		/// <summary>
		/// 验证当前已加载内容能够完整解释存档。新增且不冲突的内容不影响旧存档，
		/// 但存档依赖的任意内容缺失都会一次列出并拒绝恢复。
		/// </summary>
		public void RequireContentSet(ContentSetSnapshot snapshot)
		{
			if (snapshot == null)
			{
				throw new ArgumentNullException(nameof(snapshot));
			}
			IReadOnlyList<ContentId> requiredContentIds = snapshot.ContentIds;
			if (requiredContentIds == null)
			{
				throw new InvalidOperationException("内容集合快照缺少内容 ID 清单。");
			}

			List<string> missingContentIds = new List<string>();
			HashSet<ContentId> seenContentIds = new HashSet<ContentId>();
			for (int i = 0; i < requiredContentIds.Count; i++)
			{
				ContentId contentId = requiredContentIds[i];
				if (!contentId.IsValid)
				{
					throw new InvalidOperationException(
						$"内容集合快照的第 {i + 1} 项不是有效内容 ID。");
				}
				if (!seenContentIds.Add(contentId))
				{
					throw new InvalidOperationException($"内容集合快照重复声明内容 {contentId}。");
				}
				if (!m_byId.ContainsKey(contentId))
				{
					missingContentIds.Add(contentId.Value);
				}
			}

			if (missingContentIds.Count > 0)
			{
				throw new InvalidOperationException(
					"当前启用的基础内容或 Mod 无法解释该存档，缺少内容：" +
					string.Join("，", missingContentIds) + "。");
			}
		}
	}
}
