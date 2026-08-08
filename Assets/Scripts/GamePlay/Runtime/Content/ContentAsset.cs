using System;
using System.Collections.Generic;
using GameCore;
using UnityEngine;

namespace Gameplay.Content
{
    /// <summary>
    /// 可进入 Gameplay 内容索引的 ScriptableObject 技术基类。
    /// 它只统一稳定身份、最小展示信息和 EX-GAS 标签，不承载具体玩法或牌桌表现。
    /// </summary>
    public abstract class ContentAsset : ScriptableObject
    {
        [Header("身份")]
        [SerializeField, InspectorName("内容 ID"), Tooltip("供存档、联机、Mod 和编辑器引用的唯一内容身份。Unity GUID、资源地址和文件名都不能替代它。")]
        private ContentId m_contentId;

        [Header("展示")]
        [SerializeField, InspectorName("显示名"), Tooltip("给玩家和作者看的名称。为空时由内容资产名兜底。")]
        private string m_displayName;

        [SerializeField, TextArea, InspectorName("描述"), Tooltip("内容摘要或规则说明，只用于展示，不承载结算逻辑。")]
        private string m_description;

        [SerializeField, InspectorName("图标"), Tooltip("列表、提示和小尺寸界面使用的图标。资源加载统一由 ResourceSystem 负责。")]
        private SoftAssetReference<Sprite> m_icon;

        [Header("EX-GAS 标签")]
        [SerializeField, InspectorName("标签码"), Tooltip("引用 EX-GAS GameplayTag 的正式整数码。标签层级和查询语义由 GAS 负责。")]
        private int[] m_tagCodes = Array.Empty<int>();

        /// <summary>
        /// 作者维护的唯一内容身份。存档、联机和 Mod 引用只能依赖该值。
        /// </summary>
        public ContentId ContentId => m_contentId;

        /// <summary>
        /// 面向玩家和内容作者的显示名称；作者未填写时使用 Unity 资产名。
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(m_displayName) ? name : m_displayName;

        /// <summary>
        /// 仅用于展示的内容说明，不参与条件判断或效果结算。
        /// </summary>
        public string Description => m_description ?? string.Empty;

        /// <summary>
        /// 小尺寸界面使用的资源地址引用。返回对象始终非空，但地址仍可能未配置。
        /// </summary>
        public SoftAssetReference<Sprite> Icon => m_icon ??= new SoftAssetReference<Sprite>();

        /// <summary>
        /// 作者声明的 EX-GAS GameplayTag 精确码集合。
        /// 整数只用于序列化和索引，父子标签与匹配语义必须交给 EX-GAS 处理。
        /// </summary>
        public IReadOnlyList<int> TagCodes => m_tagCodes ?? Array.Empty<int>();

    }
}
