using GameCore;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 有独立名称、说明和图标的内容作者源。
	/// 纯规则参数不应继承本类型，避免被错误当成玩家可见内容。
	/// </summary>
	public abstract class DisplayableContentAsset : ContentAsset
	{
		[Header("展示")]
		[SerializeField]
		[LabelText("显示名")]
		[Tooltip("给玩家和作者看的名称。为空时由内容资产名兜底。")]
		private string m_displayName;

		[SerializeField]
		[TextArea]
		[LabelText("描述")]
		[Tooltip("内容摘要或规则说明，只用于展示，不承载结算逻辑。")]
		private string m_description;

		[SerializeField]
		[LabelText("图标")]
		[Tooltip("列表、提示和小尺寸界面使用的图标。资源加载统一由 ResourceSystem 负责。")]
		private SoftAssetReference<Sprite> m_icon;

		public string DisplayName => string.IsNullOrWhiteSpace(m_displayName) ? name : m_displayName;

		public string Description => m_description ?? string.Empty;

		public SoftAssetReference<Sprite> Icon => m_icon;
	}
}
