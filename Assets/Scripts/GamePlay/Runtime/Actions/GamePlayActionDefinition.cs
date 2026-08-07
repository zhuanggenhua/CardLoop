using System;
using System.Collections.Generic;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 可被牌桌、世界节点或后续行动解析器独立引用的行动作者源。
    /// 它声明内容身份、展示、EX-GAS 标签、参与条件、回合消耗和结果声明；结果由正式状态 owner 结算。
    /// EX-GAS Ability 继续负责角色技能生命周期，不继承或执行本类型。
    /// </summary>
    [CreateAssetMenu(menuName = "GamePlay/内容/行动", fileName = "行动_")]
    public class GamePlayActionDefinition : GamePlayContentAsset
    {
        [Header("回合消耗")]
        [SerializeField, Min(0), InspectorName("消耗回合数"), Tooltip("普通行动完成所需的回合数；0 表示选择后立即完成。切换即时制时由当前回合规则统一换算秒数，不能在行动上另配持续秒数。战斗技能不使用本字段。")]
        private int m_turnCost = 1;

        [Header("参与槽位")]
        [SerializeField, InspectorName("参与槽位"), Tooltip("声明行动需要哪些参与对象以及各自的匹配条件。槽位只负责判断能否参与，不扣除材料、不启动计时，也不执行结果。")]
        private GamePlayActionSlotDefinition[] m_participationSlots = Array.Empty<GamePlayActionSlotDefinition>();

        [Header("结果意图")]
        [SerializeReference, InspectorName("结果意图"), Tooltip("只声明行动完成后的结果意图；真正提交由对应状态 owner 在行动完成时统一校验和执行。")]
        private GamePlayActionResultIntent[] m_resultIntents = Array.Empty<GamePlayActionResultIntent>();

        [SerializeField, InspectorName("随机结果分支"), Tooltip("行动开始时由权威随机流选择一个分支；分支只保存相对权重和结果意图，不直接执行副作用。")]
        private GamePlayActionResultBranch[] m_resultBranches = Array.Empty<GamePlayActionResultBranch>();

        /// <summary>
        /// 行动的参与槽位作者数据。运行时只能查询这些声明，不能把匹配成功当作行动已经开始或对象已经消耗。
        /// </summary>
        public IReadOnlyList<GamePlayActionSlotDefinition> ParticipationSlots =>
            m_participationSlots ?? Array.Empty<GamePlayActionSlotDefinition>();

        /// <summary>
        /// 行动完成后提交给正式状态 owner 的结果声明；作者源不直接执行这些意图。
        /// </summary>
        public IReadOnlyList<GamePlayActionResultIntent> ResultIntents =>
            m_resultIntents ?? Array.Empty<GamePlayActionResultIntent>();

        /// <summary>
        /// 行动开始时交给权威随机流选择的结果分支；分支内结果只在完成时交给状态 owner 结算。
        /// </summary>
        public IReadOnlyList<GamePlayActionResultBranch> ResultBranches =>
            m_resultBranches ?? Array.Empty<GamePlayActionResultBranch>();

        /// <summary>当前行动是否声明了需要正式状态 owner 提交的任何结果意图。</summary>
        public bool HasResultIntents
        {
            get
            {
                if (ResultIntents.Count > 0)
                {
                    return true;
                }

                for (int i = 0; i < ResultBranches.Count; i++)
                {
                    GamePlayActionResultBranch branch = ResultBranches[i];
                    if (branch != null && branch.ResultIntents.Count > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 普通行动完成所需的回合数；0 表示立即完成。
        /// 即时制只换算这份数据，不维护第二份行动耗时。
        /// </summary>
        public int TurnCost => m_turnCost;
    }
}
