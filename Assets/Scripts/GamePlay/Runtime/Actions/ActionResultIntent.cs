using System;
using System.Collections.Generic;
using Gameplay.Content;
using UnityEngine;

namespace Gameplay.Actions
{
    /// <summary>
    /// 行动完成后提交给正式状态 owner 的结果声明。
    /// 该类型只描述意图，不读取或修改牌桌、角色、地图和库存状态。
    /// 当前只开放内置牌桌结果；后续必须通过正式 Mod API 登记新的结果类型及其唯一结算职责。
    /// </summary>
    [Serializable]
    public abstract class ActionResultIntent
    {
    }

    /// <summary>
    /// 行动开始时可被权威随机选择的一组结果意图。
    /// 分支键只在所属行动内稳定，用于结算、回放和结果解释，不是第二套内容 ID。
    /// </summary>
    [Serializable]
    public sealed class ActionResultBranchDefinition
    {
        [SerializeField, InspectorName("分支键"), Tooltip("所属行动内的稳定结果分支键；重复或为空会在行动开始时直接报错。")]
        private string m_key;

        [SerializeField, Min(1), InspectorName("权重"), Tooltip("正整数相对权重。权重越大，被权威随机选中的相对概率越高；0 或负数属于作者配置错误。")]
        private int m_weight = 1;

        [SerializeReference, InspectorName("分支结果"), Tooltip("该分支被选中后提交给正式状态 owner 的结果意图；这里只声明，不直接执行副作用。")]
        private ActionResultIntent[] m_resultIntents = Array.Empty<ActionResultIntent>();

        public string Key => m_key ?? string.Empty;
        public int Weight => m_weight;
        public IReadOnlyList<ActionResultIntent> ResultIntents =>
            m_resultIntents ?? Array.Empty<ActionResultIntent>();
    }

    /// <summary>
    /// 声明移除行动参与槽位中已经绑定的全部牌桌卡牌。
    /// 卡牌是否仍存在、是否重复绑定和实际移除由牌桌结果结算统一校验。
    /// </summary>
    [Serializable]
    public sealed class TabletopCardRemoveResultIntent : ActionResultIntent
    {
        [SerializeField, InspectorName("参与槽位键"), Tooltip("完成行动时移除该槽位本次绑定的全部牌桌卡牌。槽位必须存在于本次行动。")]
        private string m_slotKey;

        public string SlotKey => m_slotKey ?? string.Empty;
    }

    /// <summary>
    /// 声明在指定参与槽位的当前牌桌位置生成若干张内容卡牌。
    /// 生成只在行动完成且所有结果前置验证通过后提交，不在作者资产或行动作业中直接创建实例。
    /// </summary>
    [Serializable]
    public sealed class TabletopCardCreateResultIntent : ActionResultIntent
    {
        [SerializeField, InspectorName("产物内容 ID"), Tooltip("生成卡牌引用的唯一 Gameplay 内容身份；必须已进入当前内容索引。")]
        private ContentId m_contentId;

        [SerializeField, Min(1), InspectorName("生成数量"), Tooltip("行动完成后生成的卡牌数量，必须大于 0。")]
        private int m_count = 1;

        [SerializeField, InspectorName("位置来源槽位键"), Tooltip("使用该参与槽位当前所在堆栈的位置生成产物；槽位必须绑定至少一张牌。")]
        private string m_anchorSlotKey;

        public ContentId ContentId => m_contentId;
        public int Count => m_count;
        public string AnchorSlotKey => m_anchorSlotKey ?? string.Empty;
    }
}
