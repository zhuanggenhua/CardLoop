using System;
using System.Collections.Generic;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 多个条件的组合方式。
    /// </summary>
    public enum EGameConditionOperation
    {
        All,
        Any
    }

    /// <summary>
    /// 组合条件，按“全部满足”或“任一满足”的方式聚合多个子条件。
    /// </summary>
    [Serializable]
    public class AreConditionMet : ABaseCondition
    {
        [InspectorName("组合方式")]
        [Tooltip("All 表示所有子条件都满足才成立；Any 表示任意一个条件满足即可成立。")]
        [SerializeField]
        private EGameConditionOperation m_operator = EGameConditionOperation.All;

        [InspectorName("子条件")]
        [Tooltip("需要聚合判断的条件列表。空列表在 All 下视为满足，在 Any 下视为不满足。")]
        [SerializeReference, SubclassSelector]
        private ICondition[] m_conditions = null;

        public override bool Evaluate()
        {
            switch (m_operator)
            {
                case EGameConditionOperation.All: return CheckAnd();
                case EGameConditionOperation.Any: return CheckOr();
            }

            return false;
        }

        private bool CheckAnd()
        {
            foreach (ICondition condition in GetConditions())
            {
                if (!(condition?.Evaluate() ?? true))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CheckOr()
        {
            foreach (ICondition condition in GetConditions())
            {
                if (condition?.Evaluate() ?? true)
                {
                    return true;
                }
            }

            return false;
        }

        public override void StartListening(Action action)
        {
            base.StartListening(action);

            foreach (ICondition condition in GetConditions())
            {
                condition?.StartListening(action);
            }
        }

        public override void StopListening()
        {
            foreach (ICondition condition in GetConditions())
            {
                condition?.StopListening();
            }

            base.StopListening();
        }

        private IEnumerable<ICondition> GetConditions()
        {
            return m_conditions ?? Array.Empty<ICondition>();
        }
    }
}
