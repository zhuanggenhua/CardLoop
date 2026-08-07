using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 普通行动从回合制切换到即时制时使用的唯一时间换算规则。
    /// 行动仍只配置回合消耗；战斗始终使用自己的实时战斗时钟，不读取本资产。
    /// </summary>
    [CreateAssetMenu(menuName = "GamePlay/规则/普通行动时间换算", fileName = "普通行动时间换算_")]
    public sealed class GamePlayTurnTimingDefinition : GamePlayContentAsset
    {
        [Header("即时制换算")]
        [SerializeField, Min(0.001f), InspectorName("每回合秒数"), Tooltip("普通行动处于即时制时，一个回合单位对应的游戏秒数。必须大于 0；战斗攻击间隔、技能时间轴和冷却不使用这个值。")]
        private float m_secondsPerTurn = 1f;

        /// <summary>普通行动即时推进时，一个回合单位对应的游戏秒数。</summary>
        public float SecondsPerTurn => m_secondsPerTurn;
    }
}
