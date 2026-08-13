using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using System.IO;
#endif
using GameCore;
using Sirenix.OdinInspector;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// Gameplay 内容资产的技术基类，只承载唯一内容身份、EX-GAS 静态标签和作者校验入口。
	/// </summary>
	public abstract class ContentAsset : ScriptableObject
	{
		[Header("身份")]
		[SerializeField]
		[LabelText("内容 ID")]
		[Tooltip("供存档、联机、Mod 和编辑器引用的唯一内容身份。首次为空时由资产文件名和 Unity GUID 短 hash 自动生成；生成后不随文件名漂移。")]
		private ContentId m_contentId;

		[Header("EX-GAS 标签")]
		[SerializeField]
		[ListDrawerSettings]
		[ValueDropdown("@GAS.General.GeneralGasChoiceHelper.Tags()", IsUniqueList = true, HideChildProperties = true)]
		[LabelText("标签码")]
		[Tooltip("引用 EX-GAS GameplayTag 的正式整数码。标签层级和查询语义由 GAS 负责。")]
		private int[] m_tagCodes = Array.Empty<int>();

		public ContentId ContentId => m_contentId;

		public IReadOnlyList<int> TagCodes => m_tagCodes ?? Array.Empty<int>();

		internal void ValidateContentAsset(ContentValidationContext context)
		{
			ValidateContent(context ?? throw new ArgumentNullException(nameof(context)));
		}

		/// <summary>
		/// 校验当前派生内容的作者数据和跨内容引用；Mod 派生类型可覆盖该入口。
		/// </summary>
		protected virtual void ValidateContent(ContentValidationContext context)
		{
		}

		#if UNITY_EDITOR
		/// <summary>
		/// 首次保存作者资产时生成稳定内容 ID；Unity GUID 只作为生成种子，不进入运行时身份协议。
		/// </summary>
		public bool EnsureGeneratedContentIdForEditor()
		{
			if (ContentIdRules.IsValidKey(m_contentId.Value))
			{
				return false;
			}
			string assetPath = AssetDatabase.GetAssetPath(this);
			if (string.IsNullOrWhiteSpace(assetPath))
			{
				return false;
			}
			string guid = AssetDatabase.AssetPathToGUID(assetPath);
			if (string.IsNullOrWhiteSpace(guid))
			{
				return false;
			}
			string fileName = Path.GetFileNameWithoutExtension(assetPath);
			m_contentId = new ContentId(ContentIdRules.CreateGeneratedContentId(fileName, guid));
			EditorUtility.SetDirty(this);
			return true;
		}

		protected virtual void OnValidate()
		{
			EnsureGeneratedContentIdForEditor();
		}
		#endif
	}
}
