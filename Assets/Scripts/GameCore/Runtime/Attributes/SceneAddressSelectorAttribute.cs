using UnityEngine;

namespace GameCore
{
	/// <summary>
	/// 在 Inspector 中从场景资产选择项目 YooAsset 场景地址。
	/// 该字符串只负责场景加载定位，不是地图内容 ID。
	/// </summary>
	public sealed class SceneAddressSelectorAttribute : PropertyAttribute
	{
	}
}
