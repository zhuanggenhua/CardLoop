using System;
using System.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 增加或移除队伍金钱。
    /// </summary>
    [Serializable]
    public class AddOrRemoveMoney : IContextualCommand
    {
        [InspectorName("动作")]
        [Tooltip("Add 表示增加金钱，Remove 表示扣除金钱。")]
        [SerializeField] private EAction m_action = EAction.Add;

        [InspectorName("金额")]
        [Tooltip("要增加或扣除的金钱数量。")]
        [SerializeField][Min(0)] private int m_amount = 0;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            Debug.Assert(m_amount != 0, "Invalid quantity! Expected != 0");

            switch (m_action)
            {
                case EAction.Add:
                    GameManager.InventorySystem.AddMoney(m_amount);
                    break;

                case EAction.Remove:
                    GameManager.InventorySystem.RemoveMoney(m_amount);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}

