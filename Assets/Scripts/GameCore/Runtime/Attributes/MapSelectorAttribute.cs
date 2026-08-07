using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 在 Inspector 中选择 MapSystem 使用的 YooAsset 场景地址。
    /// 该字符串只负责场景加载定位，不是地图内容 ID。
    /// </summary>
    public class MapSelectorAttribute : PropertyAttribute
    {
    }
}

