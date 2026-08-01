using System.Threading.Tasks;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 请求打开一个通用游戏菜单。
    /// menuClosedTask 允许命令等待菜单关闭，但事件本身不负责创建任务。
    /// </summary>
    public readonly struct MenuRequestedEvent
    {
        public MenuRequestedEvent(EMenu menu, TaskCompletionSource<bool> menuClosedTask)
        {
            Menu = menu;
            MenuClosedTask = menuClosedTask;
        }

        public EMenu Menu { get; }

        public TaskCompletionSource<bool> MenuClosedTask { get; }
    }

    /// <summary>
    /// 请求打开商店菜单。
    /// CommandContext 保留来源命令，便于菜单关闭后回到原命令链。
    /// </summary>
    public readonly struct ShopRequestedEvent
    {
        public ShopRequestedEvent(Shop shop, TaskCompletionSource<bool> menuClosedTask)
            : this(shop, GameCommandContext.Unknown(), menuClosedTask)
        {
        }

        public ShopRequestedEvent(Shop shop, GameCommandContext commandContext, TaskCompletionSource<bool> menuClosedTask)
        {
            Shop = shop;
            CommandContext = commandContext;
            MenuClosedTask = menuClosedTask;
        }

        public Shop Shop { get; }

        public GameCommandContext CommandContext { get; }

        public TaskCompletionSource<bool> MenuClosedTask { get; }
    }

    /// <summary>
    /// 请求打开背包菜单。
    /// 上下文负责说明打开的是谁的背包、与哪个目标交互以及允许的转移方式。
    /// </summary>
    public readonly struct InventoryMenuRequestedEvent
    {
        public InventoryMenuRequestedEvent(InventoryMenuContext context, TaskCompletionSource<bool> menuClosedTask)
        {
            Context = context;
            MenuClosedTask = menuClosedTask;
        }

        public InventoryMenuContext Context { get; }

        public TaskCompletionSource<bool> MenuClosedTask { get; }
    }

    /// <summary>
    /// 请求打开角色信息菜单。
    /// UI 只读 CharacterMenuContext，不反向修改角色内部状态。
    /// </summary>
    public readonly struct CharacterMenuRequestedEvent
    {
        public CharacterMenuRequestedEvent(CharacterMenuContext context, TaskCompletionSource<bool> menuClosedTask)
        {
            Context = context;
            MenuClosedTask = menuClosedTask;
        }

        public CharacterMenuContext Context { get; }

        public TaskCompletionSource<bool> MenuClosedTask { get; }
    }

    /// <summary>
    /// 请求打开能力菜单。
    /// 与角色菜单共用上下文，但入口语义独立，便于按钮和快捷键分别路由。
    /// </summary>
    public readonly struct AbilitiesMenuRequestedEvent
    {
        public AbilitiesMenuRequestedEvent(CharacterMenuContext context, TaskCompletionSource<bool> menuClosedTask)
        {
            Context = context;
            MenuClosedTask = menuClosedTask;
        }

        public CharacterMenuContext Context { get; }

        public TaskCompletionSource<bool> MenuClosedTask { get; }
    }

    /// <summary>
    /// 请求打开制作菜单。
    /// CraftingStation 是制作规则真相源，事件只负责把请求送到 UI 层。
    /// </summary>
    public readonly struct CraftRequestedEvent
    {
        public CraftRequestedEvent(CraftingStation craftingStation, TaskCompletionSource<bool> menuClosedTask)
            : this(craftingStation, GameCommandContext.Unknown(), menuClosedTask)
        {
        }

        public CraftRequestedEvent(CraftingStation craftingStation, GameCommandContext commandContext, TaskCompletionSource<bool> menuClosedTask)
        {
            CraftingStation = craftingStation;
            CommandContext = commandContext;
            MenuClosedTask = menuClosedTask;
        }

        public CraftingStation CraftingStation { get; }

        public GameCommandContext CommandContext { get; }

        public TaskCompletionSource<bool> MenuClosedTask { get; }
    }

    /// <summary>
    /// 请求关闭所有菜单的广播事件。
    /// 不携带目标菜单，避免某个 UI 面板成为全局关闭入口的真相源。
    /// </summary>
    public readonly struct CloseAllMenusRequestedEvent
    {
    }

    /// <summary>
    /// 物品详情被打开时的通知。
    /// 其它 UI 可以据此暂停快捷提示或切换焦点。
    /// </summary>
    public readonly struct ItemDetailsOpenedEvent
    {
        public ItemDetailsOpenedEvent(Item item)
        {
            Item = item;
        }

        public Item Item { get; }
    }

    /// <summary>
    /// 物品详情关闭通知。
    /// 空事件代表只关心“详情层已退出”这个事实，不关心之前是哪件物品。
    /// </summary>
    public readonly struct ItemDetailsClosedEvent
    {
    }

    /// <summary>
    /// UI 相关运行时事件门面。
    /// 调用方通过这里发布菜单请求，避免直接依赖具体 UIManager 或菜单实例。
    /// </summary>
    public static partial class GameRuntimeEvents
    {
        public static void RequestMenu(EMenu menu, TaskCompletionSource<bool> menuClosedTask = null)
        {
            Publish(new MenuRequestedEvent(menu, menuClosedTask));
        }

        public static void RequestShop(Shop shop, TaskCompletionSource<bool> menuClosedTask = null)
        {
            RequestShop(shop, GameCommandContext.Unknown(), menuClosedTask);
        }

        public static void RequestShop(Shop shop, GameCommandContext commandContext, TaskCompletionSource<bool> menuClosedTask = null)
        {
            if (!shop)
            {
                return;
            }

            Publish(new ShopRequestedEvent(shop, commandContext, menuClosedTask));
        }

        public static void RequestInventory(InventoryMenuContext context, TaskCompletionSource<bool> menuClosedTask = null)
        {
            Publish(new InventoryMenuRequestedEvent(context, menuClosedTask));
        }

        public static void RequestCharacterMenu(CharacterMenuContext context, TaskCompletionSource<bool> menuClosedTask = null)
        {
            Publish(new CharacterMenuRequestedEvent(context, menuClosedTask));
        }

        public static void RequestAbilitiesMenu(CharacterMenuContext context, TaskCompletionSource<bool> menuClosedTask = null)
        {
            Publish(new AbilitiesMenuRequestedEvent(context, menuClosedTask));
        }

        public static void RequestCraft(CraftingStation craftingStation, TaskCompletionSource<bool> menuClosedTask = null)
        {
            RequestCraft(craftingStation, GameCommandContext.Unknown(), menuClosedTask);
        }

        public static void RequestCraft(CraftingStation craftingStation, GameCommandContext commandContext, TaskCompletionSource<bool> menuClosedTask = null)
        {
            if (!craftingStation)
            {
                return;
            }

            Publish(new CraftRequestedEvent(craftingStation, commandContext, menuClosedTask));
        }

        public static void RequestCloseAllMenus()
        {
            Publish(new CloseAllMenusRequestedEvent());
        }

        public static void NotifyItemDetailsOpened(Item item)
        {
            if (!item)
            {
                NotifyItemDetailsClosed();
                return;
            }

            Publish(new ItemDetailsOpenedEvent(item));
        }

        public static void NotifyItemDetailsClosed()
        {
            Publish(new ItemDetailsClosedEvent());
        }
    }
}
