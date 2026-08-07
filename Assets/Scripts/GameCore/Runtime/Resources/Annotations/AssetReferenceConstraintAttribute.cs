using System;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 迁自 Chris 的 SoftAssetReference 编辑器约束标记。
    /// 当前只保留运行时可编译的属性数据，后续若要做拖拽分组和地址格式化，再补对应 PropertyDrawer。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AssetReferenceConstraintAttribute : PropertyAttribute
    {
        public Type AssetType { get; }
        public string Formatter { get; }
        public string Group { get; }
        public bool ForceGroup { get; }

        public AssetReferenceConstraintAttribute(Type assetType = null, string formatter = null, string group = null, bool forceGroup = false)
        {
            AssetType = assetType;
            Formatter = formatter;
            Group = group;
            ForceGroup = forceGroup;
        }
    }
}
