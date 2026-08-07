using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 可选角色属性开关。
    /// GameConfig 用它决定哪些属性参与显示和配置，不代表角色当前是否拥有该属性值。
    /// </summary>
    [System.Flags]
    public enum EOptionalCharacterStatistics
    {
        None = 0,
        Mana = 1 << 0,
        MagicalAttack = 1 << 1,
        MagicalDefense = 1 << 2,
        Agility = 1 << 3,
        Luck = 1 << 4,
    }

    /// <summary>
    /// 允许触发相机震动的来源。
    /// 具体震动强度仍由战斗和表现配置决定。
    /// </summary>
    [System.Flags]
    public enum ECameraShakeSources
    {
        None = 0,
        PlayerReceiveDamage = 1 << 0,
        AnyCharacterReceiveDamageFromPlayer = 1 << 1
    }

    /// <summary>
    /// 游戏内常用术语键。
    /// 用于 UI 文案统一，不直接作为存档数据。
    /// </summary>
    public enum EGameTerm
    {
        Currency,
        Level,
        Experience
    }

    /// <summary>
    /// 单个属性在 UI 和说明中的展示配置。
    /// </summary>
    [System.Serializable]
    public struct StatSettings
    {
        public string name;
        public string shortened;
        public string description;
        public Sprite icon;
        public bool hide;
    }

    /// <summary>
    /// 一个可本地化术语的显示定义。
    /// 当前保存全称、短名、描述和图标，供菜单与战斗说明复用。
    /// </summary>
    [System.Serializable]
    public struct TermDefinition
    {
        public string fullName;
        public string shortName;
        public string description;
        public Sprite icon;
    }
}
