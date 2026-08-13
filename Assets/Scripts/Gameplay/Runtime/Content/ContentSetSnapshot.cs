using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 单局存档实际依赖的内容身份集合。它只保存唯一内容 ID，
	/// 不把 Unity GUID、资源地址、文件路径或尚未建立归属关系的 Mod 包信息变成第二套身份。
	/// </summary>
	[Serializable]
	public sealed class ContentSetSnapshot
	{
		[SerializeField]
		private ContentId[] m_contentIds;

		public IReadOnlyList<ContentId> ContentIds => m_contentIds;

		internal ContentSetSnapshot(IReadOnlyList<ContentId> contentIds)
		{
			if (contentIds == null)
			{
				throw new ArgumentNullException(nameof(contentIds));
			}

			m_contentIds = new ContentId[contentIds.Count];
			for (int i = 0; i < contentIds.Count; i++)
			{
				if (!contentIds[i].IsValid)
				{
					throw new ArgumentException(
						$"内容集合快照的第 {i + 1} 项不是有效内容 ID。",
						nameof(contentIds));
				}
				m_contentIds[i] = contentIds[i];
			}
		}
	}
}
