namespace FantasyWord.GameCore
{
    /// <summary>
    /// 框架级菜单反馈文案模板。
    /// 这里只保留代表性 Shop/Craft 面板运行所需的最小提示，不再依赖项目侧 DialogueSequence 资产。
    /// </summary>
    public static class MenuFeedbackPrompts
    {
        public const string CraftCannotUseItem = "It's not the time to use that now!";
        public const string CraftMissingIngredients = "You don't have the necessary ingredients to craft {0}!";
        public const string CraftMissingMoney = "You don't have enough funds to craft {0}!";
        public const string CraftSucceeded = "You successfully crafted {0}!";
        public const string InventoryTransferFailed = "I can't move {0} right now.";
        public const string InventoryTransferActorNotParticipant = "I can't move {0} from there.";
        public const string InventoryTransferActionLocked = "I can't handle {0} right now.";
        public const string InventoryUseActionLocked = "I can't use {0} right now.";
        public const string InventoryUseMissingItem = "I don't have {0} anymore.";
        public const string ShopCannotBuy = "This {0} is too expensive for me at the moment!";
        public const string ShopCannotSell = "{0} could be really useful, I shouldn't sell it!";
    }
}
