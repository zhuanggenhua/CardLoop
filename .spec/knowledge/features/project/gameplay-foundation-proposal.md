---
name: gameplay-foundation-proposal
description: GamePlay 地基提案：先建立内容定义与 Mod/作者源契约，再按 StackCraft 架构搬迁顺序吸收牌桌、任务、流程、UI、存档和联机约束。
metadata:
  type: feature
  status: 设计中
---

# GamePlay 地基提案

## 结论

- GamePlay 应保留本项目自己的 GameCore / YokiFrame / YooAsset / Input System / EX-GAS 方向，StackCraft 只作为参考样例和吸收对象。
- 当前优先目标是 **搬迁、理解和重构 StackCraft 架构**，不是先实现《卡牌生存：无限》的原创业务内容。
- 用户补充的《卡牌生存：无限》设计只用于说明最终游戏需要达到的扩展性：多世界、多规则、联机、Mod、关卡编辑器、角色经历、职业和混合交互。它是地基约束，不是当前开工清单。
- 正式地基的第一个模块必须是 **内容定义 / 加载 / 作者源校验**，不是牌桌拖拽；牌桌、行动进度、剧本目标、存档都要消费同一套内容契约。
- StackCraft 的牌桌手感、拆堆合堆、重叠解算、行动持续、编辑器校验等只先作为功能与问题证据；是否吸收由对应模块重新裁决，不能因为模板存在某个结构就默认保留。`Resources.LoadAll`、大一统 `CardDefinition`、枚举规则、单例上帝类、固定场景名、独立战斗规则和 JSON 扫档必须重构或排除。
- 后续实现采取激进重构口径：不为省事保留旧职责归属，不用长期 adapter 包住旧实现，不把参考样例能跑冒充 GamePlay 正式地基。
- 2026-08-04 纠偏：并行 `RuntimeContext` 小框架已删除。第二模块已完成审查并收口到 `GameCore.GameManager`、`AGameSystem`、YokiFrame `EventKit` / `SceneKit` 和 `ResourceSystem`，没有新增泛化上下文或 GamePlay 启动壳。
- 第一模块曾完成一轮收窄：不再预建七个空壳内容类型，不再把行动/配方 schema 提前塞进内容模块，不再让内容清单重复 Mod 包身份、版本、依赖和加载顺序；2026-08-05 回审又发现表现职责和作者源分类仍需订正，不能继续视为完全收口。

## 本轮硬决策

- **第一个正式模块**：内容定义 / 加载 / 作者源校验。
- **第二个正式模块已收口**：启动流程 / 系统协作 / 单局状态边界已复用并重构现有 `GameCore.GameManager`、`AGameSystem`、`EventKit`、`SceneKit`、`ResourceSystem`、`ModAPI` 和 GAS 初始化职责；没有新增 `RuntimeContext`、事件记录、系统注册表或生命周期事件。
- **第三个正式模块当前进度**：3.1-3.6 已跑通 StackCraft 可堆叠卡牌的状态、空间解算、卡牌视图、正式输入和测试场景链路；2026-08-05 回审确认这只能证明“可堆叠卡牌子系统”成立，不能把它扩张成固定工位、圆形节点、连通节点和所有牌桌对象的通用模型。业务行动规则仍未实现，通用命名和自动入堆假设需要先订正。
- **第四个正式模块已完成当前吸收切片**：4.1-4.11 已形成单一行动作者源、参与条件、显式候选选择、唯一请求启动入口、回合消耗进度、牌桌结果结算、权威随机、参与失效中断、发现过滤、作者校验和活动作业只读快照；统一测试场景已通过请求入口复现选中的 StackCraft 行动功能。完整存档恢复、网络传输、玩家授权、Mod API、库存、正式蓝图、地图旅行和 EX-GAS 结果仍属于后续真实模块，不能从当前切片越权推导。
- **第五个正式模块当前只完成 5.1**：已建立 `GamePlayWorldTurnSystem` 作为世界回合编号和确认事实的唯一写入口，并直接通过 YokiFrame `EventKit.Type` 发布 `GamePlayWorldTurnConfirmedEvent`；`TabletopCardActionSystem` 删除公开 `AdvanceTurn()`，只订阅该事实推进回合制普通行动。目标、遭遇、日结、天气、饥饿、危机和原创剧本均未实现。
- **当前阶段口径**：现在只是在打地基；具体业务设计尚未完成，不提前实现职业、技能树、荒岛剧本、联机玩法、跑团工具或原创数值。任何原创玩法信息只作为架构可扩展性约束。
- **加载**：正式内容加载走 YooAsset；核心运行时不得依赖 `Resources.LoadAll` 作为内容入口。
- **资源加载职责归属**：YooAsset 的项目级封装复用现有 YokiFrame / GameCore `ResourceSystem`、`SoftAssetReference` 与 Mod 包加载能力；不得在 GamePlay 保留第二套资源地址引用或第二套加载封装。第一模块只定义内容资产和派生索引，不提前建立内容包发现或加载器。
- **输入**：正式输入走 Unity 新 Input System；正式代码不得直接读 `UnityEngine.Input`。
- **UI 事件系统**：UGUI 场景使用 `InputSystemUIInputModule`；旧 `StandaloneInputModule` 只属于需要兼容的外部样例。
- **框架职责归属**：GamePlay 自己的框架优先；StackCraft 的脚本、场景、资源和 URP 配置保留为参考，不自动成为正式职责归属；现有 GameCore / DatabaseRegistry / ResourceSystem 若挡住正确架构，必须重构。
- **成熟框架校准**：GamePlay 地基不只参考 StackCraft；启动、系统注册、资源、场景、事件、存档和联机边界必须用 UE Gameplay Framework 或 Unity Game Framework 校准职责分层。校准只吸收职责边界，不照搬名称和结构。
- **SO 作者源**：ScriptableObject 是正式配置源之一，尤其适合 Unity 内作者、关卡编辑器和原型内容；不得因为未来支持 Mod 就默认排除 SO。
- **唯一 ID**：内容身份只能有一个唯一 ID。Unity GUID、YooAsset 地址、文件路径、资源名、包名和运行时实例号只负责定位、加载或实例引用，不得并列成第二套内容 ID。
- **内容 ID 职责纠偏**：现有 `DatabaseRegistry` 的注册、引用和旧 GUID 迁移思路可吸收；但正式玩法内容身份不能使用 Unity 资产 GUID 作为公开 ID。最终应由 `GamePlayContentId` 或等价公共内容 ID 接管，再让 registry / 索引围绕该 ID 工作。
- **禁止双重更新**：作者不能同时维护两处身份或引用关系；运行时查询缓存、生成代码、缓存文件和运行时目录必须由作者源生成或校验。
- **内容抽象口径**：`GamePlayContentAsset` 是狭窄的 ScriptableObject 技术基类，只统一稳定身份、最小展示信息和 EX-GAS 标签，不是所有玩法数据的万能业务父类，也不应提供牌桌专用 `PrimaryArt`。卡牌外观、世界外观和其它展示形态应由具体内容或表现配置组合提供；行动、配方、职业、技能、局外带出和 NPC 意图等留给真实模块。
- **内容类型口径**：正式玩法分类不得依赖可膨胀枚举。C# 类型只承载稳定作者源结构；跨剧本、跨 Mod、可组合的玩法语义使用 EX-GAS GameplayTag 的分组引用和查询表达；`GamePlayContentKind` 已删除，不作为规则判断入口。
- **标签 / 符号口径**：`wood`、`beast`、`fire-source`、`workstation` 这类“符号”本质是匹配标签，不建立独立 string 体系。每个内容作者源只保存一组 EX-GAS 标签码；身份、能力和交互语义由 GAS 标签路径区分。当前 EX-GAS 标签表是生成后一次性初始化，尚未提供 Mod 标签合并入口，因此正式层级查询延后，不能用整数相等匹配冒充 GAS 查询。
- **Mod 加载模型**：正式内容加载未来应支持“一个剧本 + 多个插件”的链式加载，插件可声明依赖、加载顺序、覆盖规则和冲突约束；当前只保留扩展约束，不在第一模块接入 Mod 包、命名空间、依赖排序或覆盖实现，也不得把默认包、固定路径或单包假设写进内容资产。
- **可编程 Mod API**：GamePlay 需要提供受控扩展点，而不是让 Mod 任意执行运行时代码。脚本或插件逻辑只能通过已登记的事件、查询、UI、行动解析、世界规则和联机同步 API 接入。
- **游戏内作者工具**：关卡编辑器 / Mod 编辑器是正式作者入口，应支持新建、打开、调试、构建、发布和创意工坊对接；外部 Unity 工程或表格工程只能作为专业作者路径之一。
- **GAS 职责边界**：未来职业、技能、状态、Buff/Debuff、战斗能力和持续效果默认走 EX-GAS / GamePlay 角色系统；本阶段只登记边界，不实现职业技能业务。
- **内容职责边界**：第一模块先管“能被稳定引用和校验”的地基，不接管所有玩法系统。不得再把“卡牌”和“可交互”当成互斥业务分类；角色、地点、道具、事件、工位、剧本和世界规则等作者源只有在真实独立字段与生命周期出现时才建立，表现形态和可交互能力优先采用组合关系。
- **命名口径**：`ContentCatalog` 不作为正式模块名；若后续需要类似能力，只能是由作者源生成的运行时查询缓存 / 索引。`CraftingTask` 归入桌面行动进度，`Quest` 归入剧本目标，`CombatTask` 归入战斗结算参考，三者不再统称“任务系统”。
- **配方职责口径**：行动与配方条件是冲突域，只能有一个 GamePlay 正式职责归属。旧 GameCore 的 `Recipe` / `CraftingStation` 背包制作站和 StackCraft 的 `RecipeDefinition` / `CraftingTask` 都只能作为参考来源，不能并行进入正式链路。
- **时间推进真相**：普通牌桌行动默认回合制，行动作者只配置消耗回合数；切换即时制时读取当前回合时间规则的“每回合秒数”，把游戏秒数换算为同一份回合进度。战斗始终即时并继续由战斗 / EX-GAS 实时链负责，不读取普通行动换算规则。禁止同时配置行动回合数和行动持续秒数。

## 2026-08-05 模块 1-3 回审结论

- **回审口径**：检查的不是“有没有引用 `CryingSnow.StackCraft`”这么窄，而是当前代码是否仍把模板的卡牌表现、自动入堆、单一视图或全量内容扫描误当成本游戏的普遍真相。
- **模块 1 已完成回审订正**：保留唯一内容 ID、SO 作者源、`ResourceSystem`、EX-GAS 标签和派生索引；已删除 `GamePlayContentAsset.PrimaryArt`，并删除职责重叠的 `GamePlayInteractableDefinition`。`GamePlayCardDefinition` 现在是可堆叠卡牌的具体作者源，卡牌专用 `CardArt` / `Artwork` 只存在于该类型，Mod 类型可以直接继承它；可交互能力、世界表现和其它非卡牌形态不再被迫继承卡牌定义。按单一 YooAsset 标签加载全部内容和重复 ID 直接失败仍只是基础包验证路径，尚未宣称已解决剧本按需加载或 Mod 覆盖顺序。
- **模块 2 没有发现 StackCraft 结构照搬**：正式入口没有 `GameDirector`、`RuntimeContext`、StackCraft Manager 单例链、固定场景名或 `Resources.LoadAll`。当前 `GameManager` 静态系统访问和 `MapSystem` 职责较宽属于现有 GameCore 架构债，不应伪装成“已经是最终最佳实践”，但它们不是本轮从 StackCraft 生搬出来的问题；只有真实新局、读档、联机或剧本流程证明阻塞时再按正式 owner 重构。
- **模块 3 已完成职责收窄**：运行时类型、状态、空间解算、表现、输入意图和测试都已改为 `TabletopCard*` 卡牌专用命名，不保留旧通用别名。`TabletopCardState.CreateCard` 为新卡牌建立独立单卡堆栈是卡牌子系统不变量；固定工位、可移动圆形工位、连通节点和其它非卡牌形态不进入该状态，跨形态目标等真实行动消费者出现后另行设计。
- **进入 4.2 的边界**：模块 1 和模块 3 的代码订正已经完成。第四模块可以消费卡牌拖拽事实，但不能把“目标一定是另一张卡、目标一定属于堆栈、图片一定来自通用内容基类”写进行动作者源和条件模型；非卡牌工位、圆形节点和连通节点仍需等真实行动消费者出现后单独建立作者源。

## 2026-08-02 最新设计对第一模块的修正

- **第一模块需要更新，但只更新地基边界**：最新附件把流程明确为“局外准备 -> 局内生存 -> 成长带出”，因此第一模块不能只服务局内卡牌原型；它还要能表达局外可购置/可带出内容、剧本初始投放、对局内状态和跨局成长引用之间的身份边界。
- **卡牌不是数据根**：卡牌是常见交互表面。第一模块保留 `GamePlayCardDefinition` 作为卡牌作者源，但不把它冒充所有内容的业务根；同一角色或地点未来可以同时拥有卡牌表现、世界表现和可交互入口，交互能力不再通过已删除的 `GamePlayInteractableDefinition` 二选一表达。
- **第一模块只分三类东西**：一是内容共用元信息，二是真正有独立生命周期的内容定义，三是未来系统会用到的引用关系。技能、职业、局外带出、NPC 意图、行动计划都先归入引用关系，不放进对象定义列表里。
- **作者源类型先收窄**：第一模块只保留确有公共技术合同的内容基类。只有出现必须独立配置、实例化、存档或覆盖的真实字段和生命周期，后续模块才增加新的作者源类型；表现形态和可交互能力不为了方便投影提前固化成继承树。
- **角色和意图只定引用边界**：最新教程流程确认有剧本原生 NPC 意图、普通玩家角色拖拽行动、真实玩家手动控制、领袖/好感/拒绝指令等概念；第一模块只预留角色身份、控制关系、意图声明、可见性和行动计划引用，不实现 AI、投票、好感、联机或教程业务。
- **工位需要正式作者源，但不强塞进第一模块**：固定工位、可移动圆形工位、连通节点和卡牌工位是后续牌桌与行动模块的核心输入；它们的槽位、允许对象、默认行动和展示资源应在真实工位 / 行动职责出现时建立唯一作者源，不能散落在场景脚本里，也不能为了前置而塞进通用内容基类。
- **行动计划不等于行动执行**：附件里的回合确认、统一结算、即时陷阱、拖拽弹出行动选项说明，第一模块应只声明行动/配方/事件的条件与结果意图；真正执行仍由后续行动解析、规则 pipeline、库存/牌桌状态变更和 EX-GAS 效果结算承担。
- **UI 展示字段只保留最小契约**：角色卡基础信息、职业徽记、攻击/血量/护盾/资源条、Buff 位置、右侧详情 Tab 等，只要求第一模块给出显示元信息和标签/资源引用边界，不提前实现 UI 框架或职业详情页。
- **职业、技能、状态、数值仍不在第一模块实现**：它们只作为可引用定义、EX-GAS 标签、资源引用、显示摘要和校验压力进入内容契约；不得因为最新附件补了职业系统，就在第一模块里实现技能树、DND/COC 数值或战斗规则。

## 为什么先做数据定义

- **依赖方向决定顺序**：牌桌要显示什么、拖什么、实例化什么、保存什么，都依赖卡牌/角色/地点/事件等对象定义；没有内容契约，牌桌会被迫临时发明数据模型。
- **StackCraft 的旧根在数据层**：`CardDefinition`、`CardCategory`、`Resources.LoadAll` 是旧实现的根。如果先做牌桌，后面所有投放、配方、战斗、存档都会继续引用这些旧职责。
- **Mod 和关卡编辑器不能后补**：如果第一版内容 ID、GAS 标签引用/查询、资源引用、包依赖和校验规则没定，后续每个剧本都会留下迁移债。
- **激进重构要先改源头**：正确路径是先建立 GamePlay 作者源和运行时查询缓存，再让 StackCraft 的牌桌手感接到这套源头上。

## 推荐 StackCraft 架构搬迁顺序

| 顺序 | 模块 | 职责 |
|------|------|------|
| 1 | 内容作者源 / 内容发现边界 / 校验 | YooAsset 包由资源与 Mod 系统负责；GamePlay 建立作者源 schema、GAS 标签引用、资源引用、运行时索引和基础校验，替换 StackCraft 的 `Resources` 和大一统数据根。 |
| 2 | 启动流程 / 系统协作 / 单局状态边界 | 先审 `GameCore.GameManager`、`AGameSystem` 生命周期、`EventKit` 事件、资源/Mod/GAS 初始化与 StackCraft `GameDirector` 的职责关系，再裁决是否重构现有职责或建立新的正式职责入口。 |
| 3 | Stackable Card Runtime / Card View | 可堆叠卡牌实例、卡牌视图、拖拽、拆堆、合堆、卡牌放置区域、重叠解算和选中高亮；不冒充固定工位、圆形节点和连通节点的通用运行时。 |
| 4 | 行动选择 / 配方条件 / 桌面行动进度 | 对象交互、可行动作列表、行动进度、消耗模式、符号配方、蓝图门槛和结果事件。 |
| 5 | Scenario / Objective / World Rules | 目标、遭遇、时间、日结/回合阶段、危机、胜负条件和世界规则模块；先吸收架构节奏，不先堆原创剧本内容。 |
| 6 | Economy / Pack / Trading | 卡包、交易、市场和卖卡闭环作为可选世界规则参考，不作为核心内容加载职责。 |
| 7 | Combat / Stats / Equipment Boundary | 审查 StackCraft 战斗、装备、属性和职业变化的边界；规则不吸收，冲突区表现可参考。 |
| 8 | UI Framework / Authoring Tools | 信息面板、任务进度、目标/配方列表、弹窗、菜单、编辑器冲突检查和作者体验；UI 框架属于架构。 |
| 9 | Save / Runtime Restore | 局内/局外存档、Mod 依赖快照、场景/剧本状态、运行时恢复和版本迁移。 |
| 10 | Multiplayer Constraints | 联机不是 StackCraft 现有模块，但必须从第一阶段起约束控制权、同步、可见性、随机和秘密目标边界。 |

## 数据归属

- **内容共用元信息**：唯一内容 ID、名称、描述、图标和一组 EX-GAS GameplayTag 引用。
- **作者源类型**：当前不把卡牌表现与可交互能力当成互斥作者源。角色、地点、道具、事件、工位等语义不通过空壳子类提前冻结；真实类型由独立字段和生命周期证明，外观与交互能力通过组合关系表达。
- **引用关系声明**：技能/职业引用、局外可购买/可带出引用、剧本初始投放、NPC 意图、行动计划和行动/配方条件都延后到对应模块，第一模块不猜字段。
- **内容表现**：卡面、世界图片 / 预制体、圆形节点、悬浮信息和右侧详情入口属于可组合的表现资源；资源引用不替代内容身份，也不反向决定内容业务类型。
- **运行时查询缓存**：运行前由内容加载层从作者源构建，例如按 EX-GAS Tag 查配方条件、按地点/工位查可行动作、按剧本查规则模块；它不是 `ContentCatalog`，也不是作者手动维护的第二目录。
- **运行状态**：单局牌桌、卡牌实例、行动进度、角色当前生命/饥饿/状态、目标进度、已解锁蓝图。
- **局外状态**：玩家购买、职业解锁、角色经历、跨世界物品和长期成长。
- **显示状态**：选中对象、拖拽位置、行动面板、提示、动画、音效和可视反馈。

## 交互规则

- 系统之间优先使用明确职责入口、请求/结果和成熟框架校准后的生命周期边界；不得用泛化上下文、中转层或隐藏全局单例串联正式核心逻辑。
- MonoBehaviour 只做场景组合和表现驱动；可测试规则放在纯 C# 类。
- 输入层只输出意图：点选、拖拽、取消、确认、镜头平移、缩放、快捷键；牌桌层再解释这些意图。
- UI 只展示可用行动和结果，不硬编码规则判定。
- Mod 内容不能直接执行任意运行时代码；先通过数据、标签、符号、规则模块、GAS 配置和受控扩展点表达。
- 联机 Mod 的运行时改动必须通过命令 / 事件 / 效果结果进入系统，并能记录发起者、授权、可见性、随机种子、同步和回放；本地脚本不能直接修改权威状态。
- 临时适配层必须有删除条件；不能把 StackCraft 旧类包一层继续当正式能力用。

## 阶段计划

### 阶段 0：参考样例可操作

- 目标：让 StackCraft 能在新 Input System 下作为参考样例操作。
- 修复范围：替换模板场景/预制体中的旧 UI 输入模块，并把模板中少量 `UnityEngine.Input` 读取改成参考样例输入适配。
- 边界：这只是样例兼容修，不代表 StackCraft 被正式接管。
- 验收：进入 StackCraft Title/Main 样例时不再因为旧输入 API 抛出 InvalidOperationException，能点击 UI、拖卡、平移和缩放。

### 阶段 1：内容作者源 / 加载 / 校验

- 目标：建立 GamePlay 第一个正式模块，替换 StackCraft 数据层根依赖。
- 内容：唯一内容 ID、内容资产技术基类、资源引用、一组 EX-GAS 标签码、运行时 ID 索引和基础校验；具体作者源类型必须由真实字段与生命周期证明，卡牌 / 世界表现和可交互能力不作为互斥继承分类。
- 加载：内容清单通过项目正式 `ResourceSystem` 进入，不走 `Resources`；Mod 包身份、版本、依赖和顺序继续由 `ModAPI` / `ModInfo` 负责。
- 排除：不沿用 StackCraft 大一统 `CardDefinition`、`CardCategory`、`Resources.LoadAll` 和旧卡牌 ID 作为正式事实。
- 验收：给一组已加载的内容作者源可以生成运行时索引；新增一种真实内容类型或标签语义不需要修改索引代码。只有选择卡牌作者源的内容才提供卡牌专用 `Artwork`，其它内容不会为了进入索引被迫提供卡面。正式资源包发现、加载会话不在本模块验收内。
- 小步顺序：唯一 ID -> 内容资产技术基类 -> 真实作者源 / 表现组合边界 -> 运行时派生索引 -> 内容校验。正式资源发现、加载会话、Mod 包上下文和 GAS Mod 标签合并另开专项。

#### 当前实现入口（2026-08-04）

- Runtime 入口：`Assets/Scripts/GamePlay/Runtime/Content/`，程序集为 `GamePlay.Runtime`。
- Editor 入口：`Assets/Editor/GamePlay/Content/`，菜单入口为 `GamePlay/内容/校验内容资产`。
- 已落地范围：`GamePlayContentId`、`GamePlayContentAsset`、`GamePlayCardDefinition`、`GamePlayContentIndex` 和内容校验器。`PrimaryArt` 与 `GamePlayInteractableDefinition` 已删除；卡牌专用 `CardArt` / `Artwork` 已收口到 `GamePlayCardDefinition`，不再作为所有内容的共同字段。
- 已删除：`GamePlayContentPackage` 的包 ID/版本/依赖/加载顺序、`GamePlayContentDefinition` 万能父类、七个空壳内容类型、`GamePlayContentQuery`，以及第一模块当时提前猜测条件/结果字段的旧行动定义。这些职责分别回到 Mod 系统、真实作者源和后续行动模块；当前 `GamePlayActionDefinition` 是 4.2 根据独立作者生命周期重新建立的最小类型，不恢复旧字段。
- GAS 现状：内容只保存一组 EX-GAS 官方整数标签码，第一模块不提供任何本地标签查询或标签索引。正式查询必须使用 EX-GAS；Mod 标签合并尚未实现，本阶段不声称已解决。
- 排除状态：正式链路不依赖 StackCraft `CardDefinition` / `CardCategory` / `Resources.LoadAll`，不依赖 GameCore 旧配方或 StackCraft 配方作为并行职责。
- 旧 `GamePlay.EditModeTests` 已删除：它通过反射写私有字段并锁定尚未裁决的结构，只能自证旧实现。第一模块当前使用编辑器校验、静态引用检查和 Unity 编译验收；出现真实公开行为后再按 TDD 规范补行为测试。
- 当前验证：2026-08-04 使用 Unity `6000.5.4f1` 完成删除本地标签索引后的新鲜 batchmode 脚本编译，返回码为 `0`；随后实际执行 `GamePlay/内容/校验内容资产` 对应方法，当前扫描到 `0` 个内容资产并正常退出。日志分别为 `Temp/codex-gameplay-module1-gas-index-removal-20260804.log`、`Temp/codex-gameplay-module1-final-validation-smoke-20260804.log`；`node .spec/tools/spec-lint.mjs` 通过。

#### 第一模块收口约束（2026-08-04）

- 不建立所有玩法数据共同继承的万能业务父类；只有需要进入内容索引的 SO 才继承 `GamePlayContentAsset`。`GamePlayCardDefinition` 是有独立卡面字段的卡牌作者源，项目内容与 Mod 内容都可以直接继承它；世界外观和可交互能力不再用平行空壳父类表达，等真实字段与生命周期出现后再组合到对应作者源。
- 不建立手工 `GamePlayContentSet`。内容资产只维护自身一次；默认包、剧本包和 Mod 包如何发现并形成加载会话，等正式资源 / Mod 模块裁决后由其生成输入，不要求作者再登记一份清单。
- 内容 ID 的包命名空间格式暂不在第一模块写死；所有格式检查集中在 `GamePlayContentId`，未来 Mod 包身份裁决时只允许修改这一正式入口，不得散落字符串拼接或从路径推断身份。
- 每个内容作者源只保存一组 EX-GAS 标签码；不得恢复本地 string 符号表、平行标签类型或身份/交互/匹配三份重复字段。
- 新作者源类型必须由真实独立字段和生命周期证明；不得再创建只有名称不同、没有数据职责的空壳类型。
- 行动/配方条件、结果意图和 GAS 层级查询不属于本轮已完成范围；后续必须从 StackCraft 真实字段、牌桌投放关系和 EX-GAS 正式入口重新设计。
- 最新附件新增的领袖、NPC 意图、投票、职业、主神空间、成长带出、前后排、连通节点和教程流程，只作为第一模块边界校验；第一模块不实现这些业务，只保证将来不会被 `CardDefinition` 或本地枚举卡死。
- 以上是第一模块地基修正，不代表开始实现荒岛剧本、职业系统、战斗前后排、DND/COC 数值或联机业务。

### 阶段 2：启动流程 / 系统协作 / 单局状态边界

- 目标：拆解 StackCraft 的 `GameDirector` 和各类 Manager 单例链，明确 `GameCore.GameManager` 只承接进程级基础设施启动，场景、事件、资源和未来单局业务分别回到正式 owner。
- 内容：当前项目启动入口、内容索引接入位置、系统协作边界、基础事件流、运行时实例身份、生命周期和场景切换边界；命令归因复用 `GameCore.GameCommandContext`，普通强类型事件直接走 YokiFrame `EventKit`，只有需要校验、权限、可见性、回放记录、生命周期分发或跨模块稳定 API 时才允许新增语义包装。
- 吸收：参考 `GameDirector` 的新局/读档/存档/切场景流程，`CardManager` / `CraftingManager` / `QuestManager` / `TimeManager` / `DayCycleManager` 的事件订阅、阶段推进和保存前汇总经验。
- 排除：不吸收 `public static Instance` 单例链，不依赖 `Awake` / `Start` 偶然顺序，不让 Manager 直接互相改状态，不使用固定 `Title` / `Main` / `Island` 场景名，不用 `Resources.LoadAll` 或 StackCraft 命名空间作为正式运行链路。
- 验收：形成可执行裁决表，明确 `GameCore.GameManager` / `AGameSystem` / `EventKit` / `ResourceSystem` / `ModAPI` / GAS 初始化 / StackCraft `GameDirector` 分别保留、重构、迁移或删除哪些职责；正式 GamePlay 代码不依赖 StackCraft Manager、固定场景名或旧资源扫描。
- 小步顺序：已依次完成当前启动职责、StackCraft `GameDirector` 对照、成熟框架校准、内容索引接入、事件/命令、运行时实例身份延后裁决和场景生命周期收口；并行 `RuntimeContext` 已删除。

#### 当前实现状态（2026-08-04）

- `Assets/Scripts/GamePlay/Runtime/RuntimeContext/` 下的并行上下文、系统注册、事件记录、会话/实例 ID 和随机源已删除。
- 对应 `GamePlayRuntimeContextEditModeTests` 已删除，因为它只能证明自建小框架内部自洽，不能证明真实新局、读档、场景和保存流程成立。
- 第二模块已经完成 `GameCore.GameManager` / `AGameSystem` / `EventKit` / `SceneKit` / `ResourceSystem` / `ModAPI` / GAS 初始化与 StackCraft `GameDirector` 的职责对照和重构收口。
- 2.1 初审证据曾显示 `GameManager` 和 `GameConfig` 没有装配进任何场景、Prefab 或资产；当前已通过专用 `GamePlayFoundationTest` 场景完成真实装配和运行验收。详细裁决以 `stackcraft-system-reference-matrix.md` 的“2.1 启动入口与生命周期裁决”为唯一真相源。
- 2.1 当前结论：未新增 GamePlay 启动壳；`GameCore.GameManager` 已缩窄为进程级基础设施入口，单局 NewGame / LoadGame / Travel 流程延后到真实职责出现后再决定是否建立 `GamePlayDirector`。
- 2.4 已删除 `AGameSystem` 的地图 / 读档空回调和 `GameManager.LifecycleRuntime` 双重分发。`MapSystem` / `SaveSystem` 直接发送 YokiFrame `EventKit` 事件，`PersistenceSystem` / `PlayerSystem` / `UISystem` 显式订阅；系统启动顺序、依赖校验和失败逆序回收已完成收口。
- 2.7 已把正式场景生命周期收口到 YokiFrame `SceneKit`；`ResourceSystemSceneLoaderPool` 只通过 ResKit 官方扩展点选择默认包 / Mod 包，`MapSystem` 只持有当前场景地址，`TransitionSystem` 只实现视觉过渡。重复的 `SceneResourceHandle` 和 `ResourceSystem.LoadSceneAsync` 已删除。
- 2.7 地基 PlayMode 使用两张纯地图测试场景验证了 SceneKit A -> B 加载、活动场景切换、旧场景卸载和事件顺序；旧 `M2DEngine` / `Main Menu` 固定场景名与正式代码中的直接 `SceneManager.LoadScene*` / `UnloadSceneAsync` 入口已清除。
- 2.8 已完成第二模块整体验收：PlayMode `2/2` 通过，EditMode `304` 通过、`1` 条条件跳过、`0` 失败，`.spec` lint 和正式源码残留扫描通过。
- 3.1 已完成 `CardInstance`、`CardStack`、`CardController`、`Board`、`CardPhysicsSolver`、`CardManager`、`StackingRulesMatrix`、高亮与进度 UI 的替换清单；详细裁决以 `stackcraft-system-reference-matrix.md` 为唯一真相源。
- 3.2 已新增 `TabletopCardId`、`TabletopCard`、`TabletopCardStack` 和 `TabletopCardState`；同一内容可生成多张局内卡牌，所有成员关系和位置变化只通过卡牌状态提交。每张新卡牌先形成独立单卡堆栈，这是卡牌子系统的不变量，不代表所有牌桌形态都必须入堆。定向 EditMode `5/5` 通过；全量 EditMode `309` 通过、`1` 条既有条件跳过、`0` 失败。
- 3.3 已新增 `TabletopCardPlacementArea`、`TabletopCardSpatialBody`、`TabletopCardSpatialResult` 和 `TabletopCardOverlapSolver`；完整占地夹取、禁放区、稳定同中心分离、锁定冲突和未收敛报告定向 EditMode `5/5` 通过。
- 3.4 已新增 `TabletopCardLayout`、`TabletopCardPresentationSettings`、`TabletopCardView` 和 `TabletopCardViewProjector`；视图只读牌桌状态与内容索引，预制体和图片统一通过现有 `SoftAssetReference` / `ResourceSystem` 入口定位、创建和释放。`GamePlay.Tests` 定向 EditMode `14/14` 通过，其中 3.4 新增 `4/4`。
- 3.5 已订正正式输入作者源为 `Gameplay` / `UI` / `None`，删除左键自动点击移动的旧业务假设，并新增 `TabletopCardDragSession` / `TabletopCardDragInput`。输入层只通过 `GameCore.InputSystem` 读取 `Point` / `Click`，拖拽只修改视图预览并产出 `TabletopCardPointerReleaseIntent`；不拆堆、不合堆、不执行行动。GamePlay EditMode `18/18`、输入作者源合同 `1/1` 通过。
- 3.6 已新增牌桌测试 Prefab、表现配置和最小测试装配器，把 4 个真实视图通过 YooAsset / `ResourceSystem` 创建并写入内容图片。PlayMode `2/2` 验证从中间卡牌拖拽时的首牌即时跟随、顶部尾牌阻尼、独立目标高亮和释放意图；释放后 `TabletopCardState` 仍为两个堆栈。输入测试使用 Unity Input System 官方 `InputTestFixture`，没有保留测试专用输入实现。注释审计后的新鲜验证为 GamePlay EditMode `18/18`、输入与生命周期定向 EditMode `9/9`、全量 EditMode `323` 通过加 `1` 条既有条件跳过、牌桌 PlayMode `2/2`。
- 2026-08-05 模块 1/3 回审订正后，正式生成器重新构建并回读 `GamePlayFoundationTest` 成功，Unity 退出码 `0`；GamePlay EditMode `18/18`、牌桌 PlayMode `2/2`、全量 EditMode `323` 通过加 `1` 条既有条件跳过、`0` 失败，`.spec` lint 通过。PlayMode 退出时仍有已登记的 EX-GAS 标签原生容器未释放提示，本轮没有用测试兜底掩盖，也不把它计作已修复。

#### 2.1 测试场景与真实运行验收（2026-08-04）

- 新增统一测试入口：`Assets/Scenes/GamePlayFoundationTest.unity`，配置资产为 `Assets/Scenes/GamePlayFoundationTestConfig.asset`；场景只包含 `GameManager`、`GamePlayFoundationTest` 根对象和主相机，不承载原创玩法。
- 新增编辑器菜单 `GamePlay/地基/重建测试场景`，生成器会把 `GameConfig` 写入场景并保存后回读校验，场景自动加入 Build Settings。后续吸收 StackCraft 模块时，验证对象追加到这张场景，不把 `Title` 当正式入口。
- 删除 `Assets/StackCraft/Scripts/Editor/PlayModeStartScene.cs` 及其 `.meta`：它会在每次进入 Play Mode 时强制打开 `Assets/StackCraft/Scenes/Title.unity`，与项目测试场景和正常 Unity 场景入口冲突，属于参考模板残留的全局编辑器行为。
- YooAsset `Assets/BundleCollectorSetting.asset` 保留唯一 `DefaultPackage`，删除 FantasyWord 遗留的两个不存在目录收集规则；当前增加 `CollectGamePlayContentAssets` 构建期规则，自动把所有 `GamePlayContentAsset` 作者资产标记为 `gameplay-content`，内容定义使用 `AddressDisable`，不把 YooAsset 地址变成第二内容 ID。
- `GameCore.ResourceSystem.InitializeAsync` 不再强制加载旧地址 `localization` 并创建本地化 Provider。该文件来自未迁入的 FantasyWord 业务资源，不属于 YooAsset 进程初始化；本地化保留给后续明确的 YokiFrame/正式本地化模块，不在本轮新增空文件或第二套入口。
- 新鲜 Unity `6000.5.4f1` batchmode 编译通过：`Temp/codex-gameplay-foundation-fresh-compile-20260804.log`，退出码 `0`。2.3 新鲜 PlayMode 验收见 `Temp/codex-gameplay-module23-playmode-r2-20260804.log` 和 `Temp/codex-gameplay-module23-playmode-results-r2.xml`。
- Play Mode 新实例验收结果：活动场景为 `GamePlayFoundationTest`，`GameManager.StartupState = Ready`，异常为空，场景中 `GameManager` 数量为 `1`；`ResourceSystem`、YooAsset、`ModAPI`、EX-GAS 均已初始化，GAS 正在运行，包名为 `DefaultPackage`。
- 退出验收结果：`ResourceSystem`、YooAsset、`ModAPI` 均回到未初始化，GAS 按插件官方 `Stop()` 语义停止运行；EX-GAS 当前仍保留 `IsInitialized` 世界状态，这是插件现有停止契约，不修改第三方插件源码。
- 2.3 已把 YooAsset 默认包 / Mod 包内容接入 `GamePlayContentIndex`：`GameCore.ResourceSystem` 负责跨包按资源标签加载，`GamePlayContentSystem` 持有句柄并建索引；真实测试资产 `test.foundation.card` 已从模拟清单加载并查询成功。本节仍不代表 StackCraft 的 `GameDirector` 新局、读档、存档、场景旅行和单局状态流程已经吸收完成。

### 阶段 3：可堆叠卡牌运行时 / 卡牌表现

- 目标：用阶段 1 的内容定义实例化可拖拽、可选中、可堆叠的桌面卡牌；本阶段不承担所有固定工位、圆形节点和连通节点的通用运行时。
- 内容：卡牌视图、牌桌实例、拖拽、拆堆、合堆、区域判定、重叠解算、选中高亮和投放空间意图。
- 吸收：参考 `CardInstance` / `CardStack` / `Board` / `CardPhysicsSolver` 的手感和算法。
- 排除：不让牌桌决定配方、交易、装备、战斗或职业变化；它只提交空间意图，真实事件或命令等消费者出现后再建立合同。
- 验收：EditMode 验证拆堆/合堆、边界夹取和重叠解算；统一 PlayMode 场景验证真实卡牌视图、YooAsset 图片、正式输入、拖拽预览、高亮和释放意图。卡牌子系统只提交空间意图，不提前解释森林、地点、配方或战斗业务，也不宣称覆盖非卡牌节点。

### 阶段 4：交互行动 / 配方 / 桌面行动进度

- 目标：把投放关系解释成可行动作，创建任务并产出结果。
- 内容：行动菜单、探索/采集、行动进度、消耗模式、产出、蓝图门槛和符号配方。
- 参考证据：`RecipeDefinition` / `CraftingTask` 证明模板存在延迟完成、暂停/恢复/取消、材料处理和研究/探索等玩家可见功能，但它们的 SO 继承、秒数进度、状态字段和 Manager 调度结构全部不预设保留。
- 时间实现顺序：先以回合消耗建立普通行动唯一进度，再验证默认回合推进和切换即时制后的换算；两种推进方式必须操作同一份回合进度。世界当前回合、回合确认和日结编排留给第 5 模块的正式 owner，4.6 不保存第二份全局回合计数。
- 参考 GameCore：旧 `Recipe` / `CraftingStation` 可参考背包材料扣除、费用、产物入包和失败原因提示；不能作为正式配方职责。
- 排除：不照搬按固定 `CardDefinition` 完全匹配，不照搬旧 `Recipe` 只按 `Item` / 背包交易匹配，不让配方配置直接执行副作用。
- 整体门禁：4.2-4.10 完成裁决和实现后，必须在统一测试场景用新框架复现第四模块最终选择吸收的 StackCraft 玩家可见功能，并证明旧 `CraftingManager`、自动配方扫描、直接副作用和固定场景旅行没有进入正式链路；验收通过后才能进入第五模块。
- 4.1 已完成订正：StackCraft 的 `RecipeDefinition`、四种特殊 Recipe、`CraftingManager`、`CraftingTask`、牌桌触发链、`ProgressUI`、`RecipesView`、`RecipeDefinitionEditor` 和 `CraftingData` 都只作为需求、问题和体验证据；这些结构全部不直接保留。当前只吸收延迟进度、暂停/恢复/取消和完成反馈；StackCraft 的行动秒数配置明确排除，连续执行与材料处理仍按后续小步单独裁决。
- 4.2 已完成：新增唯一正式行动作者源 `GamePlayActionDefinition`，复用 `GamePlayContentId`、显示信息、图标和 EX-GAS 标签，并通过真实 YooAsset 测试资产进入内容索引。配方不建立第二 SO、第二 ID 或特殊子类；它留作后续条件与结果组合形成的行动语义。4.2 当时没有预填后续字段；4.3 已加入参与条件，4.6 已加入唯一回合消耗，消耗、随机、结果和执行代码仍不属于行动 SO。
- 4.3 已完成：行动可声明多个开放参与槽位，每个槽位按行动内稳定键、数量范围、唯一内容 ID 白名单、内容静态 EX-GAS 标签和角色动态 GAS 标签进行无副作用查询；“符号”直接使用内容标签，不建立枚举或第二标签系统。蓝图、世界事实、属性和空间关系分别留给后续正式职责，不用万能条件或字符串 GameFlag 抢占职责。
- 4.4 已完成：消费牌桌释放回调的交互组合入口显式提供当前可用行动集合，解析器把来源/目标卡确定性分配到 4.3 槽位并返回零个、一个或多个完整/待填充候选；玩家只能按行动唯一内容 ID 显式选择。没有全局行动扫描、候选 ID、Source/Target 内容枚举、随机替选、单候选自动执行或牌桌状态写入。
- 4.5 已订正：删除牌桌参与对象预留表、共享/独占声明、候选来源牌桌耦合和对应测试。工位没有参与人数上限；多个角色属于同一工位的一次行动，固定工位数量只表示可并行的独立行动数。角色唯一工位归属必须由未来正式工位状态的唯一写入口保证，内部重复归属直接报错，不能用预留表或冲突结果掩盖错误结构。2026-08-06 最新设计把多人默认规则改为“参与者增加缩短耗时，成功判定只取相关等级最高者”，旧的逐人判定直到成功口径已撤销；具体进度倍率和判定入口仍需由真实工位/属性系统承担。当前没有耐久、删卡或库存正式修改入口，因此未照搬 `Keep/Consume/Destroy`；卡牌材料变化留给 4.7 与正式状态结算共同裁决，角色技能 Cost 继续归 EX-GAS。
- 4.6 已订正：普通行动默认回合制，行动只配置 `TurnCost`；`GamePlayTurnTimingDefinition.SecondsPerTurn` 是切换即时制时唯一换算规则，作业始终累计 `ProgressedTurns`。战斗始终即时且不接入普通行动模式开关。后续进入 4.7 结果结算、4.8 权威随机、4.9 连续/中断、4.10 发现/校验、4.11 存档/联机/Mod/统一测试场景收口。
- 4.7 当前牌桌切片已完成：行动作者源使用可序列化的具体结果意图声明“移除参与槽位卡牌”和“在槽位位置生成产物卡牌”；`TabletopCardActionResultSettlement` 先完整验证槽位、卡牌、内容 ID、数量、重复移除和牌桌容量，再由唯一 `TabletopCardState` 提交。行动 SO、作业和结果意图都不直接改状态；库存、蓝图、旅行、随机与 EX-GAS GameplayEffect 尚未出现真实 owner 接入，不建立空结算接口。
- 4.7 验收：`Logs/TestResults-GamePlay-4.7-EditMode-Final.xml` 为 `5/5`，覆盖立即结算、耗时完成后结算、非法产物与重复移除时牌桌完全不变，以及结果提交后旧候选必须重新查询；统一测试场景经正式生成器写入产物资产和 `SerializeReference` 结果，`Logs/TestResults-GamePlay-4.7-AllPlayMode-Final.xml` 为 `6/6`，验证 YooAsset、拖拽候选、回合 / 即时同一进度、参与卡移除、产物创建和视图刷新。下一步仍在 4.7 内逐个审真实 owner，不越权进入 4.8。
- 4.7 最终回归：`Logs/TestResults-GamePlay-4.7-AllEditMode-Final.xml` 为 335 通过、1 条条件不适用跳过、0 失败；`Logs/TestResults-GamePlay-4.7-AllPlayMode-Final.xml` 为 `6/6`。EditMode 与 PlayMode 均在 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2` 下正常退出，`Logs/GamePlay-4.7-AllEditMode-Final.log` 与 `Logs/GamePlay-4.7-AllPlayMode-Final.log` 未出现 `Leak Detected` 或未释放原生集合。
- 4.7 地图旅行裁决为延后而非吸收：StackCraft `TravelRecipe` 保存固定场景名并在同步结果完成后异步切场景，还把参与卡复制成跨场景数据；当前 `MapSystem` 只接受资源场景地址，场景地址不是地图内容 ID，行动完成与异步切换也没有共同事务，参与角色/牌桌状态跨地区保留规则尚未成立。现在新增旅行意图只会制造第二套地图真相或假完成，因此没有改地图代码。
- 4.8 已完成权威随机切片：行动可以声明共同结果和若干正整数权重的随机结果分支；分支键只在所属行动内稳定，不是第二套内容 ID。`TabletopCardActionSystem` 由单局权威 owner 一次性注入非零种子，使用 Unity.Mathematics 的 xor-shift 随机流在行动开始时选择分支，并把分支键写入 `TabletopCardActionJob`；完成时只结算作业已经记录的分支，不在 UI、SO 或结算阶段重新掷骰。
- 4.8 排除：不恢复 StackCraft “多个行动候选按权重随机替玩家选择”的行为，不使用 `UnityEngine.Random`，不接受浮点权重、零权重随机回退或未初始化时临时取种子。当前只建立单机权威与未来服务器权威共用的确定性入口；随机状态存档、网络同步、隐藏随机可见性和 Mod API 仍归 4.11，不能把固定测试种子当正式单局 owner。
- 4.8 验收：定向 `Logs/TestResults-GamePlay-4.8-EditMode-First.xml` 为 `8/8`，覆盖加权分支、固定种子、缺失权威随机、非法权重和原子牌桌结果；正式生成器已把统一测试行动改为“共同移除参与卡 + 权威随机生成 1 或 2 个产物”。最终 `Logs/TestResults-GamePlay-4.8-AllEditMode-Final-R2.xml` 为 338 通过、1 条条件不适用跳过、0 失败，`Logs/TestResults-GamePlay-4.8-AllPlayMode-Final.xml` 为 `6/6`；对应日志未出现 `Leak Detected` 或未释放原生集合。下一步进入 4.9 连续执行与中断策略。
- 4.9 已完成当前真实中断切片：所有 `TabletopCardActionSystem` 作业在开始前都必须绑定当前 `TabletopCardState` 与 `GamePlayContentIndex`，候选作者源、槽位、数量、卡牌内容和可选 GAS 动态标签会重新复核；运行或暂停作业在每次正式推进前再次复核，参与卡被移除或不再满足槽位时以 `ParticipantInvalidated` 原因取消，进度和结果都不会继续提交。
- 4.9 取消原因只包含真实生命周期结果：玩家/规则显式取消为 `Requested`，参与者失效为 `ParticipantInvalidated`，系统关闭为 `SystemStopped`；拒绝开始、作者配置错误和联机过期命令不伪装成取消。纯静态牌桌绑定不暴露 GAS 依赖，只有角色动态标签行动使用显式 GAS 绑定入口。
- 4.9 明确排除 StackCraft `isContinuous`、剩余材料自动重扫配方、Manager `Update` 自动新建任务和旧候选续作。当前游戏要求玩家显式确认行动计划，且尚无正式工位/行动计划 owner 提供“此刻仍可用的行动集合”；重复执行必须重新查询候选并新建作业，完成或取消作业不能继承旧进度。
- 4.9 延后项：2026-08-06 最新设计规定多人默认缩短行动耗时、成功判定只取相关等级最高者；当前尚无正式工位归属、角色属性查询和多人倍率规则 owner，不能从牌桌绑定数量直接推导进度倍率。本步只保证参与身份中断，不冒充工位位置、世界规则或属性判定已经接入。
- 4.9 定向验收：`Logs/TestResults-GamePlay-4.9-EditMode-R3.xml` 为 `34/34`，覆盖手动取消原因、参与卡移除后零进度取消、系统关闭取消及既有随机/原子结算；`Logs/TestResults-GamePlay-4.9-PlayMode-R3.xml` 为 `3/3`，统一场景验证真实 YooAsset 行动在参与卡移除后不生成产物并刷新视图。
- 4.9 最终回归：`Logs/TestResults-GamePlay-4.9-AllEditMode-Final.xml` 共 `341` 条，其中 `340` 通过、`1` 条条件不适用跳过、`0` 失败；`Logs/TestResults-GamePlay-4.9-AllPlayMode-Final.xml` 为 `7/7`。两次运行都启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，对应日志未出现 `Leak Detected` 或未释放原生集合。
- 4.10 已完成当前发现 / 蓝图边界切片：新增 `GamePlayContentDiscoveryState` 只记录当前局内已发现的唯一内容 ID；新增 `GamePlayActionDiscoveryFilter` 只按发现状态过滤调用方已经提供的可用行动集合；统一测试场景先把测试行动标记为已发现，再进入候选解析。没有新增研究随机、开包抽配方、蓝图 UI、自动全局扫描、存档格式或 Mod API。
- 4.10 作者源校验已接入现有 `GamePlayContentValidator`：行动槽位键、槽位数量范围、允许内容 ID 引用、结果槽位引用、产物内容引用、产物数量、随机分支键和权重会在建立内容索引前校验；同参与条件的多行动只给 `ACTION_CONDITION_SIGNATURE_SHARED` 警告，不阻止多选项交互。
- 4.10 验收：定向发现 / 校验 EditMode `Logs/TestResults-GamePlay-4.10-EditMode-First.xml` 为 `4/4`；GamePlay EditMode `Logs/TestResults-GamePlay-4.10-EditMode-R3.xml` 为 `38/38`；牌桌 PlayMode `Logs/TestResults-GamePlay-4.10-PlayMode-R1.xml` 为 `3/3`。最终全量回归 `Logs/TestResults-GamePlay-4.10-AllEditMode-Final-R2.xml` 为 `344` 通过、`1` 条条件不适用跳过、`0` 失败，`Logs/TestResults-GamePlay-4.10-AllPlayMode-Final.xml` 为 `7/7`；两次最终运行均启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，日志未出现 `Leak Detected` 或未释放原生集合。4.11 随后完成当前请求复核与统一场景收口。
- 4.11 已完成：新增请求复核、活动作业只读快照和统一测试场景的唯一请求启动链；完整存档恢复、网络传输、玩家授权、Mod API 和断线恢复仍未实现。定向 GamePlay EditMode `Logs/TestResults-GamePlay-4.11-EditMode-Final.xml` 为 `42/42`；全量 EditMode `Logs/TestResults-GamePlay-4.11-AllEditMode-Final.xml` 为 `348` 通过、`1` 条条件不适用跳过、`0` 失败；全量 PlayMode `Logs/TestResults-GamePlay-4.11-AllPlayMode-Final.xml` 为 `7/7`。

### 阶段 5：目标 / 遭遇 / 世界流程架构吸收

- 目标：吸收 StackCraft 的目标监听、遭遇筛选、日结阶段和场景流程组织方式，但不在本阶段堆原创生存内容。
- 内容：目标激活/完成/解锁链、事件流、世界规则 pipeline、日结/回合阶段、危机倒计时和遭遇候选筛选。
- 吸收：参考 `QuestManager`、`EncounterManager`、`DayCycleManager`、`TimeManager` 和 `GameDirector` 的流程片段。
- 排除：不吸收固定 `QuestType`、固定场景名旅行、英文模板文案和写死的饥饿/卖卡流程。
- 验收：用最小测试内容跑通目标推进、日结阶段和规则模块调用；原创剧本数值可以暂不进入。

#### 当前实现状态：5.1 世界回合事实与确认（2026-08-07）

- StackCraft 的 `TimeManager` 同时持有实时秒数、时间倍率、当前天数和日开始/结束事件；`DayCycleManager` 又把日结固定编排为通知、喂食、卖卡、遭遇和新一天。GamePlay 只吸收“世界流程有一个回合确认事实，具体系统订阅它”的职责，不吸收这些固定字段、`Time.timeScale` 写入或日结顺序。
- `GamePlayWorldTurnSystem` 持有 `ConfirmedTurnIndex`，唯一公开写入口是 `ConfirmTurn()`。确认后直接调用 YokiFrame `EventKit.Type.Send(new GamePlayWorldTurnConfirmedEvent(...))`；没有新增事件总线、事件包装层、回合缓存或第二个事件编号。
- `TabletopCardActionSystem` 通过 `AGameSystem` 生命周期注册 / 注销 `EventKit.Type` 监听，并删除公开 `AdvanceTurn()`。回合制时消费一次世界回合事实并推进普通行动；即时制时仍使用同一份 `TurnCost` 和 `GamePlayTurnTimingDefinition.SecondsPerTurn` 换算进度，不重复消费世界事件。战斗实时链不接入该系统。
- 统一测试场景生成器已把 `GamePlayWorldTurnSystem` 装配到 `GameManager`，场景运行测试改为通过世界回合系统确认回合；没有把 `Quest`、`Encounter`、`DayCycle`、天气、饥饿或原创剧本塞入测试场景。
- 验收证据：`Logs/TestResults-GamePlay-5.1-EditMode-Final.xml` 为 GamePlay EditMode `44/44`；`Logs/TestResults-GamePlay-5.1-AllEditMode-Final.xml` 共 `351` 条，其中 `349` 通过、`2` 条既有 UnitySkills Package Manager 条件跳过、`0` 失败；`Logs/TestResults-GamePlay-5.1-AllPlayMode-Final.xml` 为 `7/7`。全量运行启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，未发现 `Leak Detected`、未释放 Native Collection 或编译错误。
- 当前未吸收：目标激活/完成、遭遇候选、日结阶段、世界规则 pipeline、天数、存档和联机同步。它们要等下一小步分别锁定真实输入和唯一职责，不由 5.1 的回合编号提前代行。

### 阶段 6：战斗 / Stats / 装备 / 职业变化边界

- 目标：审查 StackCraft 的战斗、属性、装备和职业变化系统，决定哪些表现可参考、哪些规则必须排除。
- 内容：冲突区表现、战斗对象分组、装备属性修改、单位状态显示和 GAS / 角色系统职责边界。
- 吸收：参考 `CombatRect` 的工作区视觉组织，以及战斗对象被集中到独立桌面区域的表现方式。
- 排除：不吸收 RPS 战斗规则、`CombatStats`、`CombatType`、命中/暴击职责、投射物规则或 `classChangeResult` 职业变化。
- 验收：形成替换清单，明确 StackCraft 战斗规则不进入正式链路；未来职业技能只通过 EX-GAS / 角色系统接入。

### 阶段 7：UI 框架 / 存档 / 作者工具收口

- 目标：在核心状态边界稳定后建立局内/局外存档、UI 框架和作者工具。
- 内容：RunSave、MetaSave、ScenarioState、ModDependencySnapshot、信息面板、进度 UI、目标/配方列表、配方冲突检查和剧本校验报告。
- 吸收：参考 StackCraft 保存范围、`InfoPanel` 信息优先级、`ProgressUI` 桌面贴附、任务/配方列表和配方冲突提示。
- 排除：不吸收 JSON 全目录扫档、不用场景名当存档主键、不把 StackCraft Inspector 当正式关卡编辑器，不让 UI 拥有规则真相。
- 验收：新增一个地点、一条符号配方或一个世界规则时，不需要改核心运行时代码，并能被校验器发现引用缺失或冲突。

### 阶段 8：联机约束回查

- 目标：不实现完整联机玩法，但回查前面所有架构模块是否阻碍未来联机、叛徒和秘密目标。
- 内容：行动发起者、控制者、授权、可见性、种子随机、同步事件、运行时快照和断线恢复边界。
- 排除：不在本阶段实现房间、匹配、服务器、叛徒完整规则或网络传输层。
- 验收：每条正式状态变化都能说明由谁发起、谁可见、如何记录、是否可同步或回放。

## 对 StackCraft 的具体吸收方式

- 先拆数据层：只参考 `CardDefinition` / `RecipeDefinition` / `Quest` / `EncounterDefinition` 的字段范围，不保留其正式职责。
- 再吸收牌桌：参考 `CardInstance` / `CardStack` / `Board` / `CardPhysicsSolver` 的桌面表现，但 GamePlay 的运行时实例必须和内容定义分离。
- 再重建行动与配方：StackCraft `RecipeDefinition` / `CraftingTask` 和旧 GameCore `Recipe` / `CraftingStation` 只证明模板分别存在桌面延迟行为与库存事务问题；正式架构必须从 GamePlay 需求重新推导，不能把两边的进度字段、消耗枚举和交易结构拼接起来。
- 再吸收节奏：参考 Pack / Quest / DayCycle / Save 的演示闭环，但正式局内、局外和存档层要重新划分职责。

## 当前风险

- 如果先做牌桌再补数据定义，会把 StackCraft 的 `CardDefinition`、`CardCategory` 和 `Resources` 带进正式运行链路。
- YooAsset 内容地基如果太晚接入，后续 Mod 和关卡编辑器会被迫迁移资源路径和 ID。
- 如果为了“先能玩”长期保留 adapter，后续每个模块都会双职责并存，重构成本会翻倍。
- 如果旧 GameCore 配方和 StackCraft 配方并行进入正式链路，会出现两个材料判断、两个结果结算、两个 UI 反馈和两套存档语义，后续 Mod / 关卡编辑器会无法判断哪套才是真相。
- 如果把完整技能树、联机房间、叛徒规则和多世界内容提前当业务实现，会拖慢架构吸收；正确做法是先定数据契约和命令/可见性边界，再按依赖顺序逐块落地。
- 如果把《卡牌生存：无限》的半成品策划当成当前实现清单，会让地基被未定业务牵着走；当前只能把它作为可扩展性验收压力测试。

## 近期执行建议

1. 第二模块保持当前“未照搬 StackCraft”的收口，不恢复已删除的 `RuntimeContext`、第二事件层、第二资源入口或 StackCraft 单例 Manager 链；现有 GameCore 债务只在真实流程证明阻塞时重构。
2. 第一模块的通用卡面泄漏和可交互空壳类型已经删除；进入 4.2 后继续复用唯一 ID、内容索引、EX-GAS 标签和 `ResourceSystem`，不恢复第二套内容真相。
3. 第三模块已经收窄为可堆叠卡牌子系统；现有自动建立单卡堆栈只对 `TabletopCardState` 成立，固定工位、圆形节点和连通节点不得塞进当前堆栈模型。
4. 4.11 已完成行动请求、活动作业快照和统一测试场景收口；其它结果只有出现正式状态职责和本阶段明确选择吸收的真实功能时才继续接入，不建立万能效果总线。
5. 第五模块当前只推进 5.1 世界回合事实与确认；目标、遭遇、日结和世界规则尚未进入正式代码，不能由回合系统代行。
6. 每个模块开工前继续写旧实现替换清单：参考来源、重构范围、删除/隔离对象、临时适配删除条件和验收方式。
7. 在 StackCraft 架构吸收和替换清单稳定前，不开始堆原创生存内容，避免系统边界被具体数值和临时卡牌路径带偏。
