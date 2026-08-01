using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 玩法装备对表现资源的类型安全引用基类。
    /// 具体渲染数据由表现程序集实现，GameCore 不反向依赖渲染实现。
    /// </summary>
    public abstract class EquipmentVisualAsset : ScriptableObject
    {
    }
}
