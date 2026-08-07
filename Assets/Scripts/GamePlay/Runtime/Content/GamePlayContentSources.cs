using GameCore;
using UnityEngine;

namespace GamePlay
{
    /// <summary>
    /// 可堆叠卡牌的作者源基类。角色、道具、资源等业务内容可以派生它获得卡牌表现，
    /// 但可交互能力、世界节点表现和规则职责不通过本继承关系表达。
    /// </summary>
    [CreateAssetMenu(menuName = "GamePlay/内容/卡牌", fileName = "卡牌_")]
    public class GamePlayCardDefinition : GamePlayContentAsset
    {
        [Header("卡牌表现")]
        [SerializeField, InspectorName("卡面美术"), Tooltip("卡牌正面使用的图片地址。它只负责表现，不替代内容 ID。")]
        private SoftAssetReference<Sprite> m_cardArt;

        /// <summary>
        /// 作者配置的卡牌正面图片地址。资源生命周期仍由牌桌卡牌投影器负责。
        /// </summary>
        public SoftAssetReference<Sprite> CardArt => m_cardArt ??= new SoftAssetReference<Sprite>();

        /// <summary>
        /// 卡牌视图使用的图片地址；没有独立卡面时回退到内容共用图标。
        /// </summary>
        public SoftAssetReference<Sprite> Artwork => CardArt.IsValid() ? CardArt : Icon;
    }
}
