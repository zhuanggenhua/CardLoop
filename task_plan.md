# CardLoop Gameplay 地基吸收计划

## 目标

以 StackCraft 的玩家可见能力和成熟框架职责为证据，统合 CardLoop 现有 GameCore、EX-GAS、YooAsset、UIKit 与新输入系统，逐模块建立自己的正式地基；不复制模板结构，也不提前制作原创剧本业务。

## 当前阶段

2026-08-18 纠偏：此前把“机制 / 代表性业务一致”越权说成“整体一致”，这是错误口径。当前只能证明 Gameplay 自有框架已经接管一批 StackCraft 机制效果，并能承接 Starter / Beginning 代表性业务竖切；不能证明玩家第一眼看到的卡牌表面、材质分类、卡图比例、进度条样式、命中弹窗、拖拽 / 移动 / 受击 / 投射物 / 粒子动画已经与模板一致。新增专项表面 / 动画审计见 `.spec/knowledge/features/project/stackcraft-visual-animation-parity.md`，该表未收口前，不得宣称“和模板一致”“完整复刻”或“可以删除模板”。本轮已完成第一轮静态表面订正：卡牌视图 Prefab 改为标题 / 价格 / 营养 / 当前生命分区、候选外轮廓和受击本体反馈入口；`node .spec/tools/gameplay-static-preflight.mjs` 通过。下一步仍要做真实画面落点 / 比例核验和动画逐项补齐。

2026-08-17 新鲜复核：当前只能说“自动化覆盖范围内与 StackCraft 已选机制效果一致，并且代表性业务竖切已通过审计”，不能说“StackCraft 全部业务内容已经完整迁移”。本轮静态预检通过，额外 GUID 扫描确认 `Assets/StackCraft` 下 708 个旧模板 GUID 没有被参考目录外部正式文本资源引用；`FoundationTestScenePlayModeTests` `26/26`、全量 PlayMode `59/59` 通过，Unity 编译 `0` 错误，清理预期负向测试日志后 Console `0` 错误。代表性业务审计 `node .spec/tools/stackcraft-business-representative-audit.mjs` 已通过，证明 Starter / Beginning 卡包、Beginning 商贩、权重和配方候选等代表性业务参数已经映射到 CardLoop 作者源。按当前用户口径，业务不用完整实现，剩余 StackCraft 原始业务 `.asset` 是后续可选迁移范围，不阻塞当前阶段；真正删除 `Assets/StackCraft` 仍需用户当轮授权，删除后还要重新跑 Unity 编译 / PlayMode 验证。

2026-08-16 补充：Unity 恢复可用后已完成阶段 C 当前等待补跑项的运行验收。修复 Gameplay 状态常驻 HUD 鼠标点击链、`ScenarioContentPlayModeTests` 存档隔离和标题入口旧 prefab 缺少日长滑条后，`FoundationTestScenePlayModeTests` `31/31`、`ScenarioContentPlayModeTests` `8/8`、`ScenarioTitleScreenPlayModeTests` `5/5`、全量 PlayMode `58/58` 通过。该结果只证明当前统一测试覆盖项通过，不代表 StackCraft 完整效果清单已全部审计完成。

2026-08-17 当前执行口径：下方 2026-08-16 分项记录里仍出现的“等待 Unity 独占 / 待补跑”属于当时状态，已经被全量 PlayMode `59/59`、静态预检和 GUID 扫描覆盖；不得再把它当作当前阻塞。阶段 C 机制清单当前版见 `.spec/knowledge/features/project/stackcraft-system-reference-matrix.md`。业务数据全量一致必须单独按 StackCraft 原 `.asset` 到 CardLoop 作者源逐项对账，但它不是当前代表性验收完成条件。

模块 0-9 已形成可运行地基，阶段 A-B 已通过。阶段 C 在读取 StackCraft 完整 41 页文档后重新打开玩家效果审计：当前目标是先用新框架完整复现模板玩家效果供实际试玩，临时策划只约束扩展性，不能作为删除模板效果的产品真相。已补齐“标题入口四命令、友好模式开局和日长滑条开局”“既有战斗增援”“参战卡拖出战斗区离战并回桌放置”“战斗区域重叠自动合并”“实时自动战斗”“材料使用次数”“普通行动与即时战斗不能同时占用同一角色”、Research 随机解锁、按槽位权威随机打开卡包、“卡包商贩任务解锁 -> 一次性解锁提示 -> 分批付款 -> 满价生成卡包 -> 付款归零 / 收藏反馈”、“购买指定或任意卡包推进 Buy 任务”、“出售指定或任意卡牌推进 Sell 任务”、“箱子存币 -> 取币 -> 用存币付款”、“装备卡离桌 -> 占用角色槽位 -> 同槽替换 / 卸下回桌 -> EX-GAS 装备效果”、“装备成功后推进指定装备任务”、“行动成功或日终遭遇生成指定卡牌后推进 Obtain / Craft 对应任务”、“战斗击败指定卡牌后推进 Defeat 对应任务”、“当前牌桌状态推进 Have / Food / Coins / Capacity 对应任务”、“指定区域 / 地点探索推进 Explore 对应任务”、“研究发现推进 Discover 对应任务”、“当前天数达到目标推进 Day 对应任务”、“玩家切换普通行动推进模式推进 Time 对应任务”，以及“进食 -> 超限售卡 -> 遭遇 -> 新日确认 -> 自动保存”的完整日终闭环；日终无角色 Game Over 已确认由 `ScenarioRun` / `ScenarioDirector` 现有链路承担并补回归合同。模板旧结构和第二套写死数值系统不复制；模板表面和动画也不按旧 UI 代码照搬，但其中承载玩家效果的卡面、材质分类、进度条、命中反馈、粒子、投射物、拖拽 / 移动 / 受击动画必须进入表面 / 动画审计。战斗数值正式归 GNS/EX-GAS 数值链，StackCraft 的命中、闪避、暴击、RPS 克制、攻防、攻速、投射物前摇、战斗音效和 HitUI 式命中反馈已按当前模板机制映射到正式链路；玩家可见表面与动画仍待专项审计验证。玩家可见触发条件、过程结果、状态变化、操作结果和必要反馈必须进入统一 `FoundationTest` 验证。下一项继续按 StackCraft 实际效果清单审计尚未复现的通用能力，不转入原创剧本业务。模块 10.1 内容包依赖与版本已完成，10.3 已确认复用现有单局 / 牌桌 / 战斗权威随机链；FishNet 暂不接入，等待玩家席位、控制权、可见性与连接策略形成正式事实。
2026-08-16 补充：StackCraft `EquipmentPanel` 的装备可读反馈已完成裁决。模板装备面板结构、漂浮装备卡和 `InfoPanel` 不恢复；玩家需要知道“角色当前装备了什么”的效果由现有 `TabletopCardInfoPanel` 承接，直接读取 `CharacterCard` 的只读装备事实并显示“已装备”列表。装备状态仍由角色卡和 EX-GAS 结算链唯一拥有，UI 不保存第二份装备状态。
2026-08-16 补充：StackCraft `WorldCanvas` / `Highlight` 已完成裁决。世界空间表现不恢复模板全局 Canvas 单例，继续由当前绑定的 `TabletopView` 作为牌桌视图根；候选高亮不恢复临时 `Highlight` 类，继续由 `TabletopCardView` 的中文“候选高亮”子节点承接。现有 PlayMode 已覆盖拖拽候选高亮打开 / 释放关闭，后续只需在 Unity 独占后随 FoundationTest 统一补跑。
2026-08-16 补充：StackCraft 暂停灰阶与日终暗角后处理反馈已按当前 URP 链接管：`ScenarioScreenEffectView` 只读 `GameStateSystem.Menu` 和 `ScenarioRun.DayCyclePhase`，通过全局 `Volume` 的 `ColorAdjustments` / `Vignette` 投影灰阶与暗角；不恢复模板 `TimeManager`、DOTween、`OnRenderImage`、RendererFeature 或模板 Shader。测试场景生成器已补中文对象“剧本屏幕效果”和中文 Profile 资产 `Assets/Gameplay/Tests/牌桌/剧本屏幕效果配置.asset`，PlayMode 合同已覆盖暂停与日终两条反馈的进入 / 恢复；仍需等待 Unity 独占环境后重建场景并补跑。

2026-08-16 补充：StackCraft `GameOptionsUI` / `GraphicsManager` / `AudioManager` 的设置面板玩家效果已按当前框架接管：显示设置新增进程级 `DisplaySettingsSystem`，音量继续走既有 `AudioSystem`，面板继续用 UIKit `UISettings`。不恢复模板设置单例、模板 `PlayerPrefs` 键、`AudioId`、旧文本按钮体系或 `PlayerPrefs.DeleteAll()`；Reset 只清理显示和音频系统拥有的键，避免误删存档、Mod 配置和其它系统偏好。现有测试运行根和设置面板生成器已补 `DisplaySettingsSystem`、图形按钮、三类音频通道与 Reset 确认弹窗；仍需等待 Unity 独占环境后重建 Prefab / 场景并补跑 PlayMode。

2026-08-16 补充：StackCraft 非战斗通用反馈音效已完成源码接入，包括拿起 / 放下卡牌、日终进食滑动 / 进食、生成完成、单枚 / 多枚货币和购买成交。StackCraft 原 `Puff` 粒子 VFX 已按“卡牌烟雾”空间粒子 + 同步音效接入 `TabletopView` 表现链；正式表现提示符号已收口为 `CardSmoke`；图片、材质、音频和粒子预制体已收进 `Assets/Art/Sprites`、`Assets/Art/Textures`、`Assets/Art/Materials`、`Assets/Art/Prefabs`、`Assets/Audio/SFX` 标准资源目录；粒子预制体路径为 `Assets/Art/Prefabs/卡牌烟雾粒子.prefab`，运行时地址按 YooAsset 文件名规则为 `卡牌烟雾粒子`。测试场景生成器、现有测试内容资产、牌桌测试 Prefab 和 YooAsset 测试收集项已改用中文项目素材，不再直接读取 StackCraft 的 Sprites / Sounds 路径；正式场景菜单和场景地址快照也已收窄到项目场景根，不再把 StackCraft 参考场景或插件 Demo 当正式入口。`CardAI` 的非敌对卡周期产出已改造为 `CardDefinition` 作者源 + `Tabletop.AdvanceRealTime` 牌桌推进，不恢复旧 `CardAI` 协程或 `CardManager`；自动移动前置的“只抽出自己一张卡”已落到 `Tabletop.TryPlaceSingleCard` 正式牌桌入口，玩家拖拽尾段语义不被复用；非敌对随机巡逻已由 `CardDefinition` 作者源和 `Tabletop.AdvanceRealTime` 权威随机推进接管，并在提交前按桌面边界 / 禁放区校验候选点；敌对追击、进入攻击半径开战、靠近玩家战斗后增援加入已由角色唯一 EX-GAS 阵营标签 + `CharacterCardDefinition` 半径配置 + 牌桌自动推进接入；围栏容量已改造为 `CardDefinition` 自动移动留存容量 + 牌堆顺序判断；拖拽中暂停自动行为已由 `TabletopCardDragInput` 本地持有入口 + `Tabletop.AdvanceRealTime` 跳过被持有卡牌计时接入；`Board` / `LimitBooster` 的“容量卡扩大可摆放桌面”效果已由 `Tabletop` 当前放置规则派生接管，收缩时复用正式放置解算重排。该切片仍需等待 Unity 独占环境后重建 `FoundationTest` 测试场景并补跑 PlayMode。

2026-08-16 补充：StackCraft 日终遭遇的提示文本已吸收为 `ScenarioDayEncounterRule` 作者字段，由 `ScenarioRun` 结算到 `ScenarioDayEncounterResult` 并在现有回合 HUD 展示，不恢复模板 `EncounterManager` 或 `InfoPanel`。自有粒子资源继续使用中文项目素材路径；确认无旧 `m_puff*` 序列化存量后，已删除 `TabletopViewSettings` 上的旧来源兼容标记，并把“无真实存量时删除旧来源兼容名”写入命名规范。

2026-08-16 补充：StackCraft `CameraController` 的牌桌相机能力已按当前框架接管：中键平移和滚轮缩放走正式 `GameCore.InputSystem`，遭遇等空间反馈聚焦走 `TabletopPresentationCueKind.CameraFocus`，执行者是主相机上的 `TabletopCameraController`。不恢复模板旧输入、`CameraController` 单例语义、`Board` 依赖或 DOTween；`CameraShake` 已避免震动时重置相机 XY。中文项目素材规则已同步到测试断言，命中图标不再期待旧 StackCraft 英文 Sprite 名。该切片仍需等待 Unity 独占环境后重建 `FoundationTest` 并补跑 PlayMode。

2026-08-16 补充：StackCraft `QuestsView` / `RecipesView` 的玩家效果已由现有 `ScenarioJournalPanel` 承接：任务页读取当前 `QuestLog`，配方 / 行动页读取本局发现集合并随任务和发现事件刷新。不恢复模板 `QuestManager`、`CraftingManager`、配方分类 UI 或旧菜单基类；日志文案统一为“已发现配方 / 行动”，明确此处对应模板配方视图。该切片仍需等待 Unity 独占环境后重建 `FoundationTest` 并补跑日志面板 PlayMode。
2026-08-16 补充：StackCraft `DayTimeUI` 的“点击日程 HUD 切换时间推进”入口已由 `ScenarioTurnPanel` 承接为普通行动推进模式按钮；按钮只切换现有 `ScenarioRun` / `Tabletop.ProgressionMode` 的回合制与即时制，不恢复模板速度枚举或全局时间系统，战斗仍始终即时。测试场景生成器已新增 `ProgressionMode` 按钮和中文默认文案，PlayMode 用例改为真实点击 HUD；现有测试 Prefab 仍需等 Unity 独占环境后由生成器重建并补跑 PlayMode。

2026-08-16 补充：StackCraft `TextButton` / `MenuToggle` / `DayTimeUI` / `GameplayPrefsUI` 的 Click 点击反馈已按当前 UI 链吸收：模板 `Click.wav` 迁入为 `Assets/Audio/SFX/界面点击.wav`，测试生成器把 `界面点击音效` 写入 `GameConfig` 的 UI 提交音效，生成出的按钮 / 标题开关统一挂 `UINavigationTarget` 并发送现有 `AudioPlaybackRequestedEvent` 到 `AudioSystem.InterfaceSoundFX`；不恢复模板 `AudioManager`、`AudioId.Click` 或旧 TextButton 体系。现有 Prefab / 场景仍需等待 Unity 独占后由生成器重建并补跑 PlayMode。

2026-08-16 补充：StackCraft `GameplayPrefsUI` 的日长滑条已按当前框架接管：标题入口保留“整天持续秒数”玩家选项，测试生成器对齐模板 `60-180` 秒、默认 `120` 秒；正式运行把它作为 `ScenarioStartOptions` 本局覆盖值保存到单局快照，并由 `ScenarioRun` 换算成当前剧本的每回合秒数。不恢复模板 `GameplayPrefs`、`TimeManager` 或固定标题场景。代码和回归测试已补，Unity 独占前只能完成静态校验，场景重建与 PlayMode 待补跑。

2026-08-16 补充：StackCraft `InfoPanel` 的全局信息队列不迁入，悬浮信息、任务 / 配方日志、日终提示和确认按钮分别由当前正式 UI owner 承担；不恢复 `InfoPriority`、`InfoRequest`、`TextButton` 或旧 `MenuView` 悬浮转发。`MenuToggle` 的“日终开始时收起菜单”玩家效果已由 `ScenarioJournalPanel` 接管：进入当前单局非空闲日终阶段时关闭日志面板，把焦点交还给日终 HUD。该切片仍需等待 Unity 独占环境后重建场景并补跑 `ScenarioJournalPanelPlayModeTests`。

2026-08-16 补充：StackCraft `TradeManager.PlayActivationSequence` 的“卡包商贩解锁提示序列”已由 `ScenarioRun` 接管：任务完成数跨过 `PackVendorDefinition.MinimumCompletedQuests` 时发布镜头聚焦和卡牌高亮，只作为已提交任务事实后的只读表现；不恢复 `TradeManager`、`InfoPanel` 或第二份 `isUnlocked`。

2026-08-16 补充：StackCraft `MenuView` / `RecipesView` / `QuestsView` 的新内容红点和首次查看已读状态已由当前单局接管：`ScenarioRun` 保存任务 / 配方行动日志条目的已读集合并写入 `ScenarioRunSnapshot`，`ScenarioJournalPanel` 只读投影红点并在当前页刷新后标记可见项已读。不恢复旧 `GameData.SeenItems`、旧菜单基类、任务分组折叠或配方分类折叠；旧折叠依赖 StackCraft 枚举和旧 Manager，继续排除。本轮优先收口不需要启动 Unity 的问题：已清理陈旧锁文件，修复 `界面点击.wav.meta` 末尾换行缺失导致的 YAML 解析失败，并静态补齐 `牌桌测试视图设置.asset` 的投射物视图引用与排序值；PlayMode 仍需 Unity 独占后补跑。
2026-08-16 补充：阶段 C 后续执行顺序改为先代码级预检、再 Unity 验证。代码级预检包括参考源码对照、当前源码旧结构扫描、资源 GUID / YAML 对照、`.spec` lint 和非 Unity 脚本测试；只有 Unity 编译、Prefab / Scene 序列化回读、真实输入链和 PlayMode 运行结果留到编辑器阶段。新增 `node .spec/tools/gameplay-static-preflight.mjs` 作为静态预检入口，当前通过；仅提示仓库没有独立 `.sln/.csproj`，C# 编译必须留到 Unity 阶段。当前扫描未发现正式 Gameplay 源码恢复 StackCraft 旧结构或旧路径；测试临时存档目录前缀已改为 `Gameplay-*`。

2026-08-16 补充：StackCraft `CardInstance.GetInfo` 的“活动制作 / 行动名与剩余时间、多卡牌堆聚合数量摘要”已由 `TabletopCardInfoPanel` 承接。面板只读当前可读卡牌所属牌堆、`Tabletop.ActiveActions` 和行动作者源，追加“进行中行动”和“牌堆”文本；`ActionInstance` 仅新增参与卡与剩余回合只读查询，`TabletopView` 复用同一锚点查询。角色生命仍由卡牌视图显示，战斗明细继续归 GNS / EX-GAS 后续公开属性裁决；不恢复旧 `InfoPanel`、`CraftingManager` 或 StackCraft 本地 `CombatStats`。该切片已完成源码静态校验，仍需等待 Unity 独占环境后重建场景并补跑相关 PlayMode。

## 模块分层裁决

本计划分成两个层次，避免把引擎宿主、领域对象和模板目录混为一谈：

1. **模块 0：技术宿主前置条件。** 它只负责进程组合、既有基础设施的唯一启动权和 Unity 全局入口；不产生卡牌、剧本、任务或其它玩法对象。
2. **模块 1 起：Gameplay 领域聚合。** 一级模块只按玩家可感知对象及其生命周期划分；候选、输入、投影、校验和算法只作为所属聚合的子模块，不再因技术阶段升格为并列玩法系统。

因此，场景与地图的技术加载后端仍在模块 0 审查；“哪个场景属于哪一次剧本运行、何时组合或释放”属于模块 2 的 `ScenarioRun`。同理，行动作者定义属于内容作者源，而行动运行实例属于牌桌，剧本回合和任务日志属于同一个单局剧本聚合。

## 模块顺序

| 模块 | 聚合与吸收范围 | 当前状态 | 进入条件 | 一级验收口径 |
|---|---|---|---|---|
| 0. 技术宿主前置条件 | `GameManager`、资源、输入、UIKit、EventKit、场景加载后端和 GAS 的进程级组合。 | 已完成当前吸收范围。 | 无。 | 没有第二启动、输入、资源或事件入口；不建立单局玩法状态。 |
| 1. 内容作者源（静态） | SO 作者数据、唯一内容 ID、EX-GAS 标签引用、内容引用和单资产作者校验。资源加载继续归 `ResourceSystem`。 | 已完成当前静态作者层范围并回验。 | 模块 0。 | 作者只维护一份内容事实；不因内容作者源建立全局内容索引、内容会话或 Mod 协议。 |
| 2. 剧本运行时核心 `ScenarioRun` | 选择并冻结当前内容集合、创建/结束单局、组合剧本场景，并建立后续剧本能力接入的正式生命周期。 | **2.1-2.3 已完成当前范围。** | 模块 1。 | 场景随单局组合与释放；技术切换只走 `SceneSystem`，剧本场景地址只由 `ScenarioDefinition` 声明。 |
| 3. 牌桌 `Tabletop` | 卡牌、角色卡、牌堆、桌面区域、放置、拖拽意图和状态投影。行动作者定义归模块 1，行动运行归模块 4。 | **3.1-3.4 已完成当前范围。** | 模块 2。 | 牌桌拥有唯一卡牌与放置状态；输入、视图、解算器不持有第二状态。 |
| 4. 牌桌内行动 | 行动定义领域字段、候选 / 计划、请求复核、运行实例、进度、结算和行动快照。 | **4.1-4.5 已完成当前范围；补齐材料使用次数与卡包打开。** 卡包继承卡牌作者源，槽位数量唯一决定使用次数；点击启动仍进入正式行动与权威随机结算。 | 模块 1-3。 | 行动实例只属于当前牌桌；不恢复模板式全局制作或卡包管理器。 |
| **阶段 A：StackCraft 核心闭环验收** | 在统一场景完整复现“进入场景 -> 加载内容 -> 创建卡牌 -> 拖拽放置 -> 选择行动 -> 推进 -> 结算 -> 反馈”。 | **已通过。** | 模块 0-4。 | 必须使用 Gameplay 正式 owner；模板脚本运行、模块单测各自通过或文档裁决都不能替代整条玩家流程。 |
| 5. 剧本循环与旅行 | 回合/即时推进、天数、任务日志、发现事实、场景旅行和模板日程循环。 | **5.1-5.4 已完成当前范围。** | 阶段 A。 | 日程、任务和发现归 `ScenarioRun`；旅行调用正式场景后端，不恢复模板 Manager 链。 |
| 6. 牌桌内战斗与 EX-GAS | 参战关系、阵型、角色卡 ASC、实时能力调用和结算。 | **6.1-6.4 地基切片及当前补充效果完成。** 已支持增援 / 离开、区域重叠自动合并，以及独立于平时回合 / 即时模式的实时自动战斗；角色卡引用所持有的 GAS Ability，`Battle` 按 GAS 攻速排序并用权威随机选择对方目标，Ability 完成后重置进度，生命归零后移除角色并结束战斗。战斗快照明确排除。 | 模块 5。 | GNS/EX-GAS 是能力、属性、数值和效果真相；StackCraft 数值只可作为临时模板参数，`Battle` 是牌桌内对象，不成为模板式全局战斗总管。 |
| **阶段 B：StackCraft 运行玩法验收** | 在阶段 A 闭环上继续验证日程、任务、旅行和战斗等选定模板功能。 | **已通过。** | 模块 5-6。 | 同一 `ScenarioRun` 已连续走通日程、任务、地区旅行、角色 GAS 状态保留和固定伤害战斗；排除项有明确产品理由。 |
| 7. 正式 UI 组合 | 卡牌详情、HUD、操作选择、角色状态和交互反馈的正式界面组合。 | **7.1-7.4 已完成当前地基范围。** | 阶段 B。 | UI 不拥有规则真相，测试 UI 不冒充正式界面。 |
| 8. 单局快照与存档恢复 | 非战斗单局、牌桌、行动、任务、角色长期 GAS 状态和当前内容集合的真实快照与恢复。 | **8.1-8.4 功能实现与自动化运行验收完成。** 活动战斗不进入存档；模板等价操作入口已由 UIKit + SaveSystem 复现。 | 阶段 B。 | 快照来自运行时唯一状态，不保存 Unity 视图、战斗进度或重复业务数据。 |
| 9. 作者工具与关卡编辑支撑 | 内容校验、引用选择、剧本/牌桌作者工具与可扩展编辑入口。 | **9.1-9.4 当前模板吸收范围完成。** 复用正式 SO Inspector、类型受限引用、YooAsset 场景选择器和内容校验；不把未来游戏内编辑器冒充已实现。 | 模块 7-8 的正式数据。 | 工具不要求策划维护可推导内部 key，也不另造作者源。 |
| **阶段 C：StackCraft 代表性验收** | 对照模板功能矩阵验证已选玩家效果，并用 Starter / Beginning 业务竖切证明当前框架能承接模板业务。 | **机制清单当前版已收口，代表性业务审计已通过；表面 / 动画一致性未收口。** 剩余 StackCraft 卡牌 / 卡包 / 配方 / 任务 / 遭遇 `.asset` 未逐项转换，但按当前用户口径属于后续可选迁移。 | 阶段 A-B、模块 7-9。 | 必须提交已复现、明确排除、仍缺失三张清单，代表性业务审计证据，以及表面 / 动画对照结论；不得把机制或代表性验收说成全量业务、最终画面或模板可删。删除参考目录仍需用户授权和删除后 Unity 验证。 |
| 10. 联机与 Mod 边界接入 | FishNet 后端、控制权、命令、可见性、权威随机、内容包依赖、版本和恢复协议。 | 已裁决 FishNet，但尚未安装、接入或实现业务协议。 | 阶段 C 形成稳定领域边界，并先确定玩家席位 / 控制权模型。 | 不以单机全权调用、全局可见状态或裸随机为正式假设；预测只按需用于高频表现。 |

模板经济、卡包、交易和具体生存规则只作为效果复现实验内容，不自动成为 CardLoop 最终策划；在阶段 C 试玩裁决前也不能仅凭临时策划将其排除。

## 一级模块的固定推进方式

每个一级模块按以下小步推进，但不把这些小步升格为新的总管或平铺系统：

1. 对照 StackCraft、现有框架和插件的同职责流程，写清吸收、改造、保留或排除的依据。
2. 锁定该领域对象的状态 owner、唯一写入口、作者源和生命周期。
3. 审查已有候选代码。职责正确则保留，错误则删除或重构，不能因历史测试通过而默认接管。
4. 只实现当前子模块需要的正式链路，不提前填充职业、剧本、联机、Mod 或原创玩法。
5. 用 `FoundationTest` 完整复现本模块的玩家可见能力，并用自动化测试覆盖其公开契约；只有用户明确排除的效果可以不复现。当前地基过程不做最终 UI 设计验收，但如果模板表面、素材、反馈或动画承载玩家效果，必须进入参考对照，不能用“不是正式视觉验收”跳过。

“已有候选代码”只表示工作区里已有可审查的实现，不表示该模块已经完成、已经是最佳实践，或已经可以进入下一模块。

## 阶段集成门禁

领域模块解决“职责放在哪里”，阶段门禁解决“组合后是否真的得到参考模板功能”。两者必须同时通过：

1. **阶段 0：技术与内容。** 模块 0-1 完成后，验证进程唯一入口能加载一份正式内容资产并正常关闭；当前已经通过。
2. **阶段 A：核心卡牌行动闭环。** 模块 2-4 的当前正式职责完成后，在 `FoundationTest` 走真实输入和正式 owner，复现从进入场景到行动结果反馈的完整玩家流程。
3. **阶段 B：模板运行玩法。** 模块 5-6 完成后，在同一闭环加入日程、任务、旅行和战斗，不建立另一套测试入口或测试专用运行链。
4. **阶段 C：完整模板等价。** 模块 7-9 完成后，对照功能矩阵验收 UI、存档、作者工具和全部模板玩家效果；只有用户明确排除的效果可以从验收范围移除。只有此阶段通过，才能称为 StackCraft 功能吸收完成。
5. **阶段 D：项目扩展。** 模块 10 再接入联机与 Mod 正式协议；它不是模板等价验收的替代条件，也不允许反向破坏阶段 A-C 的 owner。

每个阶段都必须产出三类结果：**已由新框架复现**、**明确排除并说明产品理由**、**尚未完成且阻止阶段通过**。单模块测试、旧模板场景运行、文档裁决或局部截图只能证明各自覆盖的事实，不能替代阶段集成验收。

阶段 A-C 仍属于框架吸收过程，测试场景 UI 会随模块持续变化。除非用户明确要求，过程中的 GameView 截图只用于诊断“入口不可操作、必要反馈不可读”等功能阻塞，不执行最终视觉评分或最终图片交付。但阶段 C 需要单独完成 StackCraft 表面 / 动画参考对照：它不是 CardLoop 最终 UI 设计验收，而是证明“我的框架、他的玩家效果”没有漏掉卡面、材质、进度、命中、拖拽、移动、受击、粒子和投射物等可见效果。只有权威计划另行标记整体界面进入稳定交付候选后，才启动正式视觉验收；视觉验收通过代表该交付阶段收口，不代表某个中间模块单独通过。

阶段门禁只控制验证节奏，不降低模块设计标准。每个模块都按当前已知需求建立正式对象、生命周期和扩展边界；不得为了尽快通过阶段测试引入临时 API、测试专用运行链、残缺 owner 或以后必须推倒的 MVP 实现。

## 重新细化的模块执行卡（2026-08-11）

一级模块仍按领域聚合保留，不能为了小步推进再堆出一批顶级 `System`。下面的编号是同一聚合内必须串行完成的审查 / 重构切片；前一项没有结论时，不越过它直接实现后一项。

| 一级模块 | 串行子模块 | 当前结论 |
|---|---|---|
| 1. 内容作者源（静态） | 1.1 最小共同契约与唯一内容身份；1.2a EX-GAS 静态标签作者语义；1.2b 单 ID 内容引用作者入口；1.3 各领域定义的继承边界；1.4a 单资产校验与引用选择器；1.4b YooAsset 收集规则。 | **已完成当前静态作者层范围并回验。** 没有创建“当前内容集合”；`ContentIndex`、跨资产校验上下文和资源句柄生命周期进入模块 2 裁决。 |
| 2. 剧本运行时核心 `ScenarioRun` | 2.1a 本次剧本的内容集合选择与解析；2.1b 跨资产校验和不可变查询集合；2.1c 资源句柄归属；2.2 单局创建 / 结束；2.3 场景组合。 | **2.1-2.3 已完成当前范围。** 下一步进入模块 3 回审，模块 2-4 完成后执行阶段 A。 |
| 3. `Tabletop` | 3.1 卡牌实例与牌堆；3.2 桌面区域、位置和放置；3.3 拖拽意图；3.4 视图投影。 | **3.1-3.4 已完成当前范围。** `TabletopView` 是单张牌桌的唯一 Unity 表现对象，输入和视图均不拥有玩法状态。 |
| 4. `Tabletop` 内行动 | 4.1 行动定义的领域字段；4.2 候选与计划；4.3 请求复核和运行实例；4.4 回合 / 即时进度、暂停与结算；4.5 行动快照。 | `ActionInstance` 必须属于当前牌桌，不能恢复模板式全局制作管理器。 |
| 阶段 A | A.1 核心功能覆盖矩阵；A.2 统一场景真实闭环；A.3 自动化与玩家可见反馈验收。 | 模块 2-4 通过后立即执行，不得跳过。 |
| 5. 剧本循环与旅行 | 5.1 回合 / 即时节奏；5.2 天数与日程阶段；5.3 任务日志与发现事实；5.4 场景旅行。 | **5.1-5.4 已完成当前范围。** 都属于当前 `ScenarioRun`，阶段 B 负责组合验收。 |
| 6. 牌桌内战斗与 EX-GAS | 6.1 参战关系与阵型；6.2 角色卡唯一 ASC；6.3 实时能力调用与结算；6.4 权威随机与存档排除边界。 | **地基切片及当前补充效果完成。** `Tabletop` 支持创建战斗、增援、离开、自动区域合并和结束；`Battle` 独立按真实秒数累积 GAS 攻速、排序自动行动者并使用权威随机选择对方目标，角色作者源只引用 ASC 已授予的自动战斗 Ability；伤害继续由 Ability -> Timeline -> GameplayEffect 结算，死亡后离场。战斗不存档；StackCraft 命中 / 闪避 / 暴击和 RPS 克制规则已经由 GNS/EX-GAS 伤害链按源码公式接管，牌桌文本飘字已接入 `TabletopView`。模板 `CombatStats` / `CombatType` 不恢复，只作为参数和标签效果来源对照；投射物前摇、战斗音效和 HitUI 式命中图标 / punch 缩放已接入牌桌表现链；Unity 场景重建与 PlayMode 验证待编辑器空闲后补跑。 |
| 阶段 B | B.1 日程 / 任务 / 旅行覆盖；B.2 战斗覆盖；B.3 统一场景组合验收。 | **已通过。** 统一场景组合用例、完整 Foundation、全量 PlayMode 和全量 EditMode 均已验收。 |
| 7. 正式 UI 组合 | 7.1 牌桌可读状态；7.2 行动选择 / 填槽；7.3 角色卡状态投影；7.4 HUD 与交互反馈。 | **7.1-7.4 已完成当前地基范围。** 角色卡直接投影唯一 EX-GAS 生命；日程 HUD 投影同一 `ScenarioRun` 的回合 / 即时进度。完整角色侧栏等待职业、装备、技能与经历领域成立。 |
| 8. 单局快照与存档恢复 | 8.1 内容集合版本事实；8.2 剧本 / 牌桌 / 行动快照；8.3 角色 GAS 快照；8.4 文件存档与恢复。 | **8.1-8.4 已完成当前地基范围。** 不保存 Unity 视图、资源句柄、活动战斗或重复业务数据。 |
| 9. 作者工具与关卡编辑支撑 | 9.1 内容校验；9.2 剧本作者工具；9.3 牌桌 / 关卡作者工具；9.4 可扩展编辑入口。 | **9.1-9.4 已完成当前模板吸收范围。** 工具只编辑正式作者源，不制造第二套 key、配置表或运行时真相。 |
| 阶段 C | C.1 完整功能矩阵；C.2 UI / 存档 / 作者工具真实流程；C.3 缺失与排除清单。 | **补充审计进行中。** `FoundationTitleTest -> FoundationTest` 已覆盖标题四命令、友好模式、日长滑条和既有运行范围；完整文档效果正按日循环、行动材料、战斗、旅行和经济重新核对，模板玩家效果默认全部复现；旧 UI 结构和皮肤不照搬，但玩家可见表面 / 动画必须按专项清单对账。 |
| 10. 联机与 Mod 边界接入 | 10.1 内容包依赖与版本；10.2 权限、命令与可见性；10.3 权威随机；10.4 同步、恢复和 Mod API。 | **10.1、10.3、10.4a 数据型 Mod 作者入口、10.4b 加载取消和 10.4c 配置 / 压缩包 / 删除事务边界已完成代码收口；10.2 已完成前置审查。** 数据型 Mod 统一进入 `ContentAsset` / `ContentIndex`；配置严格读取并原子保存；压缩包独立、确定性解压；删除先验证依赖与全部路径，成功后才消费状态；运行中实际包集合由 `ResourceSystem` 唯一持有；取消贯通扫描和逐包加载。Unity 已重新打开并完成脚本编译，当前日志没有编译错误；本环境未暴露 Unity Test Runner 调用入口，新增删除事务用例仍缺新鲜运行结果。FishNet、代码型 Mod、EX-GAS 动态作者表、游戏内编辑器和联机同步 / 恢复仍未接入。 |

### 模块 1 的执行边界

| 子模块 | 本轮要裁决的对象 | 明确不做 | 完成口径 |
|---|---|---|---|
| 1.1 最小共同契约与唯一内容身份 | `ContentAsset`、`ContentId`、自动 ID 生成与人工覆盖规则。 | 不把卡牌、行动、剧本、任务或资源包字段塞进通用基类；不建立内容运行时索引。 | 每个需要跨存档、联机或作者引用的内容只有一个稳定 ID；基类只保留真正所有内容共有的静态作者事实。 |
| 1.2a EX-GAS 静态标签 | `ContentAsset` 的静态标签、官方选择器、标签图初始化前提和层级查询语义。 | 不建立本地标签表、枚举分类、字符串符号表或整数相等查询。 | 内容静态标签只保存 EX-GAS 标签码；层级判断只走 `TagHelper` / GAS 正式入口。 |
| 1.2b 单 ID 内容引用 | `ContentIdReference` 属性、选择器和跨资产引用的序列化结果。 | 不把 Unity 对象引用与内容 ID 并列保存；不在编辑器选择器中建立运行时内容索引。 | 作者选择资产后，序列化结果仍只有一个内容 ID；类型约束与不存在引用能在作者校验中明确暴露。 |
| 1.3 领域定义边界 | `CardDefinition`、`ActionDefinition`、`ScenarioDefinition`、`QuestDefinition` 等候选的继承关系与字段归属。 | 不提前填充荒岛、职业、配方、战斗或任务业务字段；不预建空壳类型。 | 每种定义只继承它真实需要的静态契约，专属字段回到所属领域。 |
| 1.4a 作者校验与选择器 | 单资产校验、领域引用校验和编辑器引用选择器。 | 不由校验器加载或保存运行时内容集合。 | 作者不手填可推导内部 key；错误能定位到对应作者资产。 |
| 1.4b YooAsset 收集规则 | YooAsset `ContentAssetFilterRule` 及其作者资产筛选边界。 | 不新增 Gameplay 资源加载器，不让收集规则替代未来 `ScenarioRun` 的内容集合解析。 | 收集规则只决定哪些正式作者资产可进入资源构建，不拥有加载、内容选择或运行状态。 |

### 当前已识别的跨模块迁移

- `ContentIndex` 是一次已解析内容集合的只读查询协作者，不是进程级作者源。它的构建、跨资产校验、资源句柄持有和销毁必须在 **2.1** 与 `ScenarioRun` 一起裁决。
- 进程级 `ContentRegistrySystem` 已在 2.1a 删除。`ScenarioDirector` 开局时通过 `ResourceSystem` 解析默认包与已启用 Mod 包，成功构建 `ContentIndex` 后才发布 `ScenarioRun`；10.1 已补齐 Mod 依赖拓扑、稳定身份和严格包版本事实。资源覆盖仍不允许，热切换协议未实现。
- 内容资源句柄由创建它的 `ScenarioDirector` 唯一持有并随活动单局释放；`ScenarioRun` 不直接依赖 YooAsset / `ResourceHandle`。10.1 的单局快照只保存 Mod 版本事实，不保存资源句柄；运行中热切换仍未作为正式协议实现。
- 模块 1 完成后只证明静态作者源正确。待模块 2.1 让单局持有已冻结内容集合后，再在 `FoundationTest` 用“读取一张已选择的卡牌内容并实例化到牌桌”完成跨模块的真实链路验收；不得为了让模块 1 单独显示画面而新造全局内容运行时。

## 当前阶段任务

- [x] 锁定“自己统合实现”而非复制或默认重写的原则。
- [x] 汇总已有模块与未收口边界。
- [x] 把旧 10 模块表改为当前可执行顺序，并标明已完成切片与下一真实子模块。
- [x] 0.1a 审查进程级唯一启动入口：`GameManager` 与 `ModAPI` 的重复启动必须直接失败。
- [x] 0.1b 审查 `GameManager` 的子系统树、依赖排序和停止/关闭所有权。
- [x] 将原 0.1c 并入 0.2：启动失败和关闭不能脱离资源、Mod、GAS 各自的原子生命周期单独裁决。
- [x] 0.2a.1 审查 `ResourceSystem` 的重复初始化、部分失败和自身清理责任。
- [x] 0.2a.2 审查 `ModAPI` 的加载失败和自身清理责任。
- [x] 0.2a.3 审查项目侧 EX-GAS 组合入口的外部重入与自身清理责任。
- [x] 0.2a.4 汇总三者的初始化原子性，确认没有第二个 owner。
- [x] 0.2b 审查 `GameManager` 对三者的成功后所有权、启动失败、取消和关闭顺序。
- [x] 0.2c 用真实 `FoundationTest` 启动/关闭链回归模块 0.2 的组合结果。
- [x] 0.3 审查输入、UIKit 和 EventKit 的正式入口。
- [x] 0.4a 收口 `SceneKit` 的项目 YooAsset 场景加载器：只保留一条加载、卸载与句柄释放链。
- [x] 0.4b 审查 `TransitionSystem` 与 `SceneKit` 的过场时序、失败和取消边界。
- [x] 0.4c 将技术场景切换从 `MapSystem` 的旧 RPG 地图、检查点、重生和存档职责中抽离。
- [x] 0.4d 用 `FoundationTest` 验证单场景切换、叠加场景卸载、资源包占用释放和事件时序。
- [x] 汇总模块 0 裁决并决定是否存在需要重构的正式入口。
- [x] 1.1 收窄内容作者源，并回验唯一 ID 的生成稳定性与显示型内容边界。
- [x] 1.2a 审查 EX-GAS 静态标签的作者选择、合法性校验和层级查询语义。
- [x] 1.2b 审查单 ID 内容引用的作者入口、序列化结果和跨资产校验边界。
- [x] 1.3 审查各领域定义的继承边界与专属字段归属。
- [x] 1.4a 审查单资产作者校验与引用选择器的职责边界。
- [x] 1.4b 审查 YooAsset 收集规则只负责构建期筛选，不接管资源加载、内容选择或单局状态。
- [x] 2.1a 审查本次剧本内容集合的选择与解析，并迁移删除进程级全量内容登记职责。
- [x] 2.1b 审查 `ContentIndex` 的跨资产校验与不可变查询边界。
- [x] 2.1c 审查单局内容资源句柄的归属与释放边界。
- [x] 2.2 审查单局创建、结束与重复开局的状态边界。
- [x] 2.3 审查剧本单局与场景组合、切换及释放的职责边界。
- [x] 3.1 审查卡牌实例、角色卡派生关系与牌堆成员归属。
- [x] 3.2 审查桌面区域、牌堆位置和原子放置解算。
- [x] 3.3 审查拖拽意图、屏幕阈值、按下偏移和输入层写权限。
- [x] 3.4 审查卡牌视图投影、资源句柄和表现状态边界。
- [x] 4.1 审查行动定义的领域字段、作者源与配方冲突边界。
- [x] 4.2 审查行动候选、玩家选择与行动计划的职责边界。
- [x] 4.3 审查行动请求复核与运行实例的职责边界。
- [x] 4.4 审查回合 / 即时进度、暂停与完成结算的职责边界。
- [x] 4.5 审查行动快照与恢复边界。
- [x] 阶段 A：模块 2-4 完成后，用统一测试场景复现 StackCraft 核心卡牌行动闭环。
- [x] 5.1 审查剧本回合 / 即时节奏与单局推进边界。
- [x] 5.2 审查天数派生与日程阶段边界。
- [x] 5.3 审查任务日志与内容发现事实。

### 5.2 天数与日程阶段裁决

- **参考来源**：StackCraft `TimeManager` 独立保存时间与日期，`DayCycleManager` 固定串联通知、喂食、卖卡、遭遇和新一天。
- **当前正式状态**：`ScenarioRun` 只保存总确认回合；当前日和当日已确认回合由剧本的每日回合数推导。跨日时先把新日期事实提交给所属 `QuestLog`，再发布已有 `ScenarioTurnConfirmedEvent`。
- **吸收**：保留“世界回合跨过日界后，消费者能读取已经提交的新日期”这一效果。回合制和即时制共用同一日界。
- **删除 / 排除**：不复制独立当前日期、`TimeManager` / `DayCycleManager`、固定五阶段、全局 `Time.timeScale`、输入锁、喂食、卖卡、遭遇、通知弹窗或自动保存。
- **不新增的理由**：当前跨日消费者只有任务日志与 HUD；已有回合事实已携带日期，二者都不需要可暂停、可等待或可确认的阶段流程。新增日开始 / 日结束事件、阶段枚举、规则注册表或空 pipeline 都会形成没有真实职责的第二入口。
- **后续边界**：天气、饥饿、危机或存档出现首个真实跨日规则时，必须先按该规则的作者源、运行状态、同步和交互需求裁决它应直接消费回合事实，还是需要由 `ScenarioRun` 拥有可中断的日程运行对象；当前不预设答案。
- **代码结论**：现有日期派生和提交顺序符合当前需求，本切片不修改生产代码；用已有跨日与即时跨日行为测试回验。

### 5.3 任务日志与内容发现事实裁决

- **参考来源**：StackCraft `QuestInstance` 暴露任务状态与整数进度，`QuestManager` 用固定枚举订阅多个全局 Manager；2DRPGEngine 以 `QuestProgress -> IQuestTaskProgress` 表达任务与子项运行对象。
- **吸收**：保留任务定义、前置解锁、子项进度、状态变化事实，以及行动完成、当前日期、内容发现三种已经存在的正式事实来源。
- **对象边界**：新增 `QuestProgress` 作为单个任务的运行对象，直接拥有定义、状态和子项运行状态；`QuestLog` 只拥有本局任务集合、前置解锁与事实分发，不再把每个任务藏成私有字典记录。
- **唯一写入口**：删除程序集内部可按任务 ID 直接完成任务的入口。任务只能由已激活子项消费正式事实后完成；同一次行动事件不会推进刚刚解锁的后继任务。
- **状态与事件区分**：行动完成是只消费一次的发生事实；当前日期和已发现内容是 `ScenarioRun` 已持有的当前状态。任务解锁后，单局统一刷新这两类当前状态，修复“发现会回放、日期不会回放”的不一致。
- **只读进度**：任务子项通过 `QuestTaskProgressSnapshot` 提供当前值与目标值，完成状态只由二者推导；正式 UI 后续直接读取 `QuestProgress.Tasks`，不另建 UI 进度副本。
- **明确不做**：不复制 `QuestType`、全局事件订阅、任务中央工厂、任意 Mod 事实入口、任务存档、任务 UI、遭遇或原创任务类型。
- **验证**：RED `Logs/TestResults-Gameplay-Module53-StateRefresh-RED-R2.xml` 精确复现后置日期任务错误；最终定向 `17/17`、Foundation `13/13`、全量 EditMode `432/433`（1 条既有忽略）、全量 PlayMode `30/30`，均零失败。

### 模块 0 的执行边界

| 子模块 | 只处理什么 | 不处理什么 | 独立验收 |
|---|---|---|---|
| 0.1a 进程入口 | `GameManager`、`ResourceSystem`、`ModAPI`、EX-GAS 的唯一启动语义。 | 场景切换、玩法对象、Mod 内容格式。 | 重复初始化不能伪装成功；启动顺序有代码和测试证据。 |
| 0.1b 系统树 | `AGameSystem` 的场景装配范围、依赖排序、启动/停止/关闭顺序。 | 把牌桌、剧本、任务改成进程级系统。 | 未装配系统不可被取得；依赖循环和缺失直接失败。 |
| 0.2a 基础设施原子性 | 资源、Mod、GAS 各自的首次初始化、重复调用、部分失败和自清理责任。 | 提前实现 Mod 内容包协议或改第三方插件源码。 | 任一基础设施不会把半初始化状态交给 `GameManager`，也不接受第二个 owner。 |
| 0.2b 基础设施组合 | `GameManager` 对资源、Mod、GAS 的真实先后关系、成功后所有权、取消和关闭顺序。 | 新增恢复框架或第二套运行时状态。 | 进程入口只释放自己已成功接管的基础设施；失败不会伪装成已启动。 |
| 0.2c 真实组合回归 | `FoundationTest` 的启动、当前单局链和关闭过程。 | 正式 UI 或具体剧本业务。 | 真实场景经唯一组合链启动，且不会遗留基础设施状态。 |
| 0.3 全局交互入口 | 新输入系统、UIKit、EventKit 的正式使用入口。 | 正式卡牌 UI 设计。 | 不存在 Gameplay 第二输入或事件总线。 |
| 0.4 场景加载后端 | `SceneKit`、`MapSystem`、`TransitionSystem` 的技术加载、卸载、资源句柄与事件时序。 | 剧本选择哪些场景、场景属于哪个单局、固定场景名或剧本状态。 | 技术后端不保存 `ScenarioRun` 真相；加载失败、取消和卸载不遗留句柄或错误事件顺序。 |

场景加载后端的启动、资源句柄和 `SceneKit` 配置仍属于模块 0 的技术入口；但 `SceneKit / MapSystem` 与 `ScenarioRun` 的场景组合、换局和释放职责已移入模块 2。它必须由单局剧本聚合裁决，不能在进程层先建立独立的场景业务模块。

### 2.3 场景组合结论

- `ScenarioDefinition.InitialSceneAddress` 是剧本初始场景的唯一作者事实；作者通过场景资产选择器维护 YooAsset 地址，空值明确表示在当前场景运行。场景地址是资源定位，不是第二个内容 ID。
- `ScenarioDirector.StartScenarioAsync` 先加载并校验内容，再调用唯一技术入口 `SceneSystem.TransitionToAsync`；只有场景切换和初始任务激活都成功后才发布 `ActiveRun`。失败时不发布半成品单局，并释放临时内容句柄。
- `ScenarioDirector.EndScenarioAsync` 先结束旧 `ScenarioRun` 并释放本局内容句柄，再返回开局前场景；旧单局在返回过场期间已经不能继续推进。
- `GameManager` 是跨场景保留的唯一进程宿主。普通剧本场景和返回场景不能再配置第二个 `GameManager`；`FoundationTest` 同时承担测试宿主与测试桌面，只作为测试启动入口，不当作普通剧本场景重复装入。
- 验收：场景组合定向 PlayMode `1/1`、完整剧本内容夹具 `7/7`、全量 EditMode `420` 通过 / `1` 条既有忽略、全量 PlayMode `30/30`。下一步进入模块 3，不提前实现模块 5 的剧本内旅行。

### 3.1 卡牌实例与牌堆结论

- 吸收 StackCraft 的对象直观性：卡牌和牌堆都是单局内真实对象，卡牌可以直接回答自己的所属牌堆、逻辑位置和放置锁定状态；同一内容 ID 可以创建多个不同局内卡牌实例。
- 不吸收 StackCraft `CardInstance` 把 Transform、碰撞、Tween、悬浮 UI、战斗、装备、制作和资源数值混在同一个 MonoBehaviour 的结构。Gameplay 的 `TabletopCard` / `CharacterCard` 保存领域身份与角色唯一 ASC，表现继续由 View 投影。
- `TabletopCardStack` 是牌堆成员关系唯一写入口。构造、合堆、拆堆和移除会原子更新卡牌的 `Stack`；删除了 `TabletopCards` 中原本需要与牌堆列表同步维护的 `m_stackByCardId` 派生关系表。
- 原 `TabletopCardState` 已重命名为 `TabletopCards`。它是 `Tabletop` 直接拥有的卡牌 / 牌堆集合与内部索引，不是另一个聚合根；正式写操作继续由 `Tabletop` 对外收口。`TabletopCardStateSnapshot` 保留，因为它准确表示可序列化状态快照。
- RED：新增对象归属合同首先因卡牌没有 `Stack` / `Position` 而编译失败。GREEN：卡牌与牌堆定向测试 `10/10`；全量 EditMode `421/422`（`1` 条既有忽略）；全量 PlayMode `30/30`。下一步只进入 3.2 的桌面区域、位置和放置解算。

### 3.2 桌面区域、位置和放置结论

- `ScenarioDefinition` 是牌桌边界、禁放区、规则卡牌尺寸和 XY 堆叠步进的唯一作者源；`ScenarioRun` 创建牌桌时冻结一次。
- `Tabletop.TryPlaceStack` 是正式原子放置入口。输入、视图和行动结果不能传入另一套规则，也不能绕过边界与重叠解算直接移动牌堆。
- `TabletopViewSettings` 只保留表现参数；最大解算轮数作为内部算法预算，不再要求内容作者配置。
- 验收：全量 EditMode `423/424`（`1` 条既有忽略），全量 PlayMode `30/30`。

### 3.3 拖拽意图结论

- 吸收 StackCraft `CardController` 的按下点偏移、从中间卡开始拖动尾段、点击/拖拽区分和目标高亮；不吸收其在输入回调中拆堆、交易、装备、开战、制作暂停或直接改 Transform 权威状态。
- `TabletopCardDragInput` 只订阅正式 `GameCore.InputSystem`，从 `GameManager` 读取唯一主相机和 `EventSystem`。拖拽阈值直接使用 `EventSystem.pixelDragThreshold`，以屏幕像素判断，不随相机缩放改变。
- 组件所在对象的 Transform 就是牌桌投影平面；命中层、最大射线距离、拖拽阈值和牌桌平面引用均不再作为重复手填字段。射线距离由主相机远裁面推导，命中结果按 `TabletopCardView` 类型筛选。
- 释放意图区分真实指针牌桌坐标与保持按下偏移后的请求牌堆位置。输入不提交牌桌状态；空白放置由 `Tabletop.TryPlaceStack` 原子处理，目标卡牌只进入行动候选查询。
- RED 证明旧会话接口无法按屏幕像素独立判断阈值；GREEN 会话测试 `6/6`，真实 Foundation 输入链 `13/13`。最终全量 EditMode `425/426`（`1` 条既有忽略），全量 PlayMode `30/30`。下一步只进入 3.4 视图投影回审。

### 3.4 牌桌视图结论

- 原 `TabletopCardViewProjector` 实际统一拥有整张牌桌的卡牌视图、行动进度、战斗姿态、资源句柄和拖拽表现，不是卡牌算法协作者。它已订正为领域直观的 `TabletopView`，保持一个深表现模块而不拆成多个 Manager / Projector。
- 原 `TabletopCardSettings` 同时配置牌桌级视图资源、战斗排序和行动进度，已订正为 `TabletopViewSettings`。权威卡牌尺寸和 XY 步进仍只来自 `Tabletop.PlacementRules`。
- `TabletopView` 自身 Transform 是所有世界空间子视图的唯一根节点，删除 `m_viewRoot` 手填引用。单卡 `TabletopCardView` 保存对应 `TabletopCard` 对象引用，卡牌 ID 与内容 ID 均从对象读取，不复制两份身份。
- 视图只按牌桌修订读取权威状态。卡牌、卡面和行动进度实例继续通过 `ResourceSystem` 创建，全部句柄由同一个 `TabletopView` 在移除、解绑或销毁时释放；没有第二套 YooAsset 入口。
- 视图设置不再运行时补建空资源引用或静默夹取非法拖拽锐度；绑定时直接校验并报告作者配置错误。
- 定向视图测试 `2/2`、Foundation 真实资源与解绑流程 `13/13`；模块 3 最终全量 EditMode `425/426`（`1` 条既有忽略）、PlayMode `30/30`。下一步进入 4.1，不把测试 UI 升级为正式 UI。

### 0.4 子模块边界

| 子模块 | 只处理什么 | 明确不做什么 | 验收口径 |
|---|---|---|---|
| 0.4a 场景资源后端 | 让 `SceneKit` 通过官方加载器池扩展点直接使用项目的 YooAsset 场景加载器；单次加载、显式卸载和 Single 替换必须沿同一条句柄释放链收口。 | 修改 YokiFrame / YooAsset 第三方源码；新增 Gameplay 资源加载包装。 | 叠加场景显式卸载后，其资源包不再被项目后端判定为占用。 |
| 0.4b 过场协作 | `TransitionSystem` 的淡出、加载、淡入、取消和完成时序；不让 `SceneKit` 与项目各自持有同一段过场状态。 | 制作正式过场视觉或 UI。 | 成功、失败、取消都不会遗留“正在过场”状态，事件顺序可观察。 |
| 0.4c 技术场景入口 | 让唯一技术场景入口只负责场景切换和场景生命周期事件；将旧 RPG 的检查点、重生、角色传送、地图存档和导航从 CardLoop Foundation 中隔离。 | 删除尚有独立旧功能职责的 2D RPG 候选模块；实现剧本场景组合。 | Foundation 不再把旧地图业务当作技术场景 owner；技术事件不再误称地图业务。 |
| 0.4d 统一回归 | 在 `FoundationTest` 验证实际场景加载、切换、叠加卸载、输入过场锁定和资源后端释放。 | 正式剧本、正式 UI 或 Mod 内容协议。 | 新正式链路复现模板场景切换能力，且无资源句柄或事件时序残留。 |

### 0.4 场景加载后端结论

- `ResourceSystem` 直接把 `ResourceSystemSceneLoaderPool` 配置给 `SceneKit.SetLoaderPool`。原 `SceneKit -> ResKitSceneLoader -> 项目加载器 -> YooAsset` 的中转链已删除，场景加载、显式卸载、Single 替换和回收均由同一个项目加载器持有与释放 YooAsset 句柄。
- 显式卸载完成和异步加载返回无效场景时，项目加载器都会释放句柄并移除资源包占用。这样 Mod 包卸载可依据同一份真实占用集合拒绝或放行，而不是依赖已消失场景的缓存。
- `SceneSystem` 是唯一技术场景 owner，持有完整切换流程的串行状态，并声明依赖 `TransitionSystem`。`TransitionSystem` 只拥有淡入淡出播放状态；两者不保存剧本、地图、检查点或角色位置。
- `MapSystem` 继续只拥有旧 2D 地图业务：检查点、重生、传送、地图配置和地图存档。它需要跨场景时调用 `SceneSystem`，不再维护当前场景地址、加载器、过场状态或场景生命周期事件。
- 生命周期事件改为 `Scene*` 语义。`SceneTransitionCompletedEvent` 仅表示成功完成；`SceneTransitionEndedEvent` 覆盖成功、失败和取消，输入解锁订阅后者。旧 `Map*` 生命周期事件已删除。
- YokiFrame 的 `SceneKit` 不能真正取消底层 YooAsset 场景加载，取消参数只能中断等待者。`SceneSystem` 因此等待实际加载完成，不再对调用方伪造“加载已取消”；若未来确实需要强制取消下载/加载，需要作为 YokiFrame / YooAsset 官方扩展能力单独裁决，不能在 Gameplay 侧另造场景加载器。
- 验收：`GameManagerAndGameStateLifecycleEditModeTests` `9/9`、`PersistenceSystemRegistrationEditModeTests` `4/4`、`ResourceSystemLifecyclePlayModeTests` `8/8`、`ContentRegistryPlayModeTests` `4/4`、`FoundationTestScenePlayModeTests` `13/13`。Foundation 场景由正式生成菜单重建，配置唯一 `SceneSystem`，没有 `MapSystem`。

`0.2a` 的三个基础设施必须串行完成：资源系统 -> Mod API -> EX-GAS 项目组合入口。它们都操作进程级静态状态，不能为了并行而同时改动或用同一场景互相验证。

### 0.2a.4 组合审查结论

- `ResourceSystem`、`ModAPI` 和项目侧 EX-GAS 入口各自只有一个正式 owner；`GameManager` 只负责按顺序组合它们，不接管第三方未由本入口启动的状态。
- 资源初始化被关闭时，取消的是本轮初始化结果的提交权；YokiFrame 的底层初始化先收敛到可回滚状态，再由 `ResourceSystem` 统一清理，避免插件留下半初始化资源包。
- 取消尚未完成回滚时，新的资源初始化会直接失败，不能重入同一份进程级状态。
- 验收：资源生命周期 `8/8`、`GameManagerAndGameStateLifecycleEditModeTests` `9/9`、`FoundationTestScenePlayModeTests` `11/11`。
- 本轮没有修改 YokiFrame、YooAsset、Mod 或 EX-GAS 第三方源码，也没有进入内容、剧本或原创玩法实现。

### 0.2b 进程组合入口结论

- `GameManager` 只在资源、Mod、GAS 各自成功启动后记录关闭责任；外部入口冲突或子模块自身初始化失败时，不再误调用对应关闭入口。
- Unity 对象销毁的取消令牌同时传给资源和 Mod 初始化。资源入口负责第三方初始化收敛后的回滚；Mod 入口负责取消后阻止迟到清单提交。
- 成功后的关闭顺序保持为：受管理系统 -> GAS -> Mod -> 资源，保证依赖资源与 GAS 的上层对象先释放。
- 验收：外部资源启动冲突的 `GameManager` PlayMode 用例 `1/1`；Mod 生命周期 `4/4`；资源生命周期 `8/8`；系统生命周期 `9/9`；GAS 生命周期 `2/2`；`FoundationTest` `11/11`。
- 当前 Mod 加载器没有可中断扫描参数，取消保证“不提交迟到结果”而非立即中断正在进行的扫描或资源包操作；该限制已写入知识库，留待 Mod 模块单独裁决。

### 0.2c 真实组合回归结论

- `FoundationTest` 实际加载后确认 `GameManager`、资源系统、YokiFrame / YooAsset、ModAPI 和 GAS 都已进入运行态；销毁唯一 `GameManager` 后，同一用例确认它们全部退出且 GAS World 为空。
- 该验收直接覆盖真实场景的资源加载、输入、牌桌、剧本、行动和场景过渡组合，而不是只用静态字段或测试桩自证。
- 验收：新增真实关闭用例 `1/1`；完整 `FoundationTestScenePlayModeTests` `12/12`。模块 0.2 的进程组合收口完成。

### 0.3 全局交互入口结论

- `GameCore.InputSystem` 是唯一正式输入 owner：它保存 `PlayerInput` 动作资产、切换 Gameplay/UI 动作图、管理外部动作订阅和 UI 输入模块绑定。`TabletopCardDragInput` 只订阅它的 Click 动作，不读取旧 `UnityEngine.Input`。
- UIKit 自己拥有唯一 `UIRoot`、`EventSystem` 和 `InputSystemUIInputModule`。当前 UIKit 根预制体与 `PlayerInput` 都引用项目唯一的 `InputSystem_Actions` 作者资产；Unity UI 模块切换动作资产时会按同名 UI 动作重定向引用。移除测试夹具里重复调用 `SetActionMap` 后，真实 HUD 点击仍通过，因此不新增强制创建 UI 根或第二个绑定包装。
- 删除了 `InputSystem` 启动时把移动、交互、五个技能键和菜单键直接派发给旧 2D 角色命令系统的代码。全局输入不再拥有角色、技能或菜单业务；未来玩法对象必须通过正式动作订阅入口自行解释输入。
- `EventKit.Type` 仍是 Gameplay 的唯一通用事件总线。`ScenarioRun`、任务日志和 UI 直接发送或订阅类型事件；EX-GAS 的 `GASEventCenter` 仅是插件自己的属性变更入口，不是第二套 Gameplay 事件总线。
- `UISystem/UIManager` 没有进入 `FoundationTest` 场景，也不是当前 CardLoop 正式 UI 框架；它保留为 GameCore 的旧通用菜单候选，等待模块 5 有真实菜单消费者后再决定保留、重构或删除。唯一即时订正是移除来源项目缩写，默认 UIKit 菜单栈改为 `game-menu`。
- 删除只对白名单 `SampleScene` 写日志的 `FormalSceneSingletonConflictDiagnostics`。它既不是唯一 owner，也不能阻止错误状态；本模块改由 Foundation 场景架构守卫验证唯一正式输入节点。
- 验收：新增 Foundation 输入守卫 `1/1`；完整 `FoundationTestScenePlayModeTests` `13/13`；`GameManagerAndGameStateLifecycleEditModeTests` `9/9`。模块 0.3 只覆盖输入、UIKit 和事件入口，不宣称已完成正式 UI、旧菜单迁移、多人本地输入、联机授权或 Mod 输入 API。

## 决策

| 决策 | 依据 |
|---|---|
| 模板只提供证据，不自动决定实现方式。 | 只有模板设计本身满足 CardLoop 的长期约束时才保留。 |
| 联机和 Mod 是每个模块的验收约束，不是最后才考虑的独立玩法。 | 避免单机/固定包假设固化后再返工。 |
| `FoundationTest` 是统一模块验收场景，可随模块扩展。 | 用真实玩家链路验证自己的结构，不复制模板场景。 |

## 已知问题

| 问题 | 当前处理 |
|---|---|
| 历史文档的“模块完成”口径混有不同阶段结论。 | 先做模块归位，后续以本计划和重审文档的当前状态为准。 |
| 当前工作区存在大量既有未提交改动。 | 只触碰本计划与当前模块明确涉及的文件，不回滚或清理无关内容。 |
| PuerTS Unity MCP 当前配置端口无响应。 | 改用当前 UnitySkills `8090` 服务验证，不重复调用失效端点。 |
| UnitySkills 测试结果接口首次误用 GET。 | 服务实际只接受 `POST /skill/test_get_result`；已按服务端入口修正，不重复尝试 GET。 |
| 一次静态搜索把 PowerShell 不支持的 `Assets\\Scenes\\*.unity` 原样传给 `rg`。 | 已改为先枚举 `.unity` 文件再传路径；不影响源码或 Unity 状态。 |
| EX-GAS Wiki 没有 `README.md`。 | 已改读插件正式总入口 `Wiki/EX-GAS.md`、`SKILL.md` 和源码；不把缺失 README 当作插件故障。 |
| Unity 通过正式生成器写出的 `FoundationTest.unity` 含尾随空格。 | 源码与文档的定向 `git diff --check` 通过；不手改 Unity 生成 YAML，场景以 Unity 编译和 PlayMode 回归作为真相。 |
# 当前进度（2026-08-12）

- [x] 7.2a 删除手填行动清单，建立正式牌桌交互协调对象。
- [x] 7.2b 重审多候选选择面板：视图只回传选择，缺口可读，不执行玩法。
- [x] 7.2c 建立牌桌拥有的可填充行动计划，并跑通真实鼠标拖拽填槽与提交。
- [x] 同步模块 7.2 结论到项目知识库，并记录最终自动回归证据。
- [x] 7.2 过程诊断记录：候选选择态与填槽缺员态曾用 GameView 确认测试入口可读；该截图不再作为模块完成门禁。
- [x] 模块 7.2 已关闭；下一步进入模块 7 的下一子模块，不提前实现正式荒岛 UI 或其它业务。
- [x] 7.3 角色卡状态投影：吸收模板角色卡生命显示，并用 EX-GAS 唯一 Health/MaxHealth 动态更新；牌堆只显示顶牌状态，战斗阵型显示参战卡自身状态。
- [x] 7.3 明确排除模板卡牌自持生命、手写 CombatStats 和装备面板；不提前实现尚无领域真相的职业、装备、技能、经历侧栏。
- [x] 7.4 日程 HUD：显示当前日、日内回合和统一进度；即时制禁用手动推进并显示“即时推进中”，UI 不保存第二份时间状态。
- [x] 7.4 完整 Foundation PlayMode `21/21`，零失败、零跳过；日志未发现泄漏、悬空引用或未处理异常。
- [x] 7.4 回合制和即时制功能链均通过；测试界面的禁用按钮与可点击状态足以支撑运行验收，不执行正式视觉验收。
- [x] 模块 7 已关闭；下一步进入模块 8 单局快照与存档恢复，不提前实现正式荒岛 UI 或其它原创业务。
- [x] 8.1 内容集合事实：保存本局冻结内容集合的全部唯一内容 ID；读档允许安装额外内容，但一次性拒绝所有缺失依赖。
- [x] 8.2 整局领域快照：`ScenarioRun` 一次性聚合当前地区、全部地区牌桌、牌堆、活动行动、任务日志、发现内容、回合 / 即时进度和每地区权威随机状态。
- [x] 8.2 单局共享的下一卡牌实例号只保存一次；地区牌桌快照不再各自保存第二份编号序列。未提交填槽计划不存档，活动战斗和旅行事务明确拒绝存档。
- [x] 8.2 定向整局 JSON 往返 `1/1`、`ScenarioRun` 全组 `13/13`、任务 / 行动 / 牌桌 / 内容相关回归 `42/42`、战斗存档边界 `8/8` 通过；全量 EditMode `441/442`（零失败、1 条既有忽略），Foundation PlayMode `21/21`。
- [x] 8.3 角色 EX-GAS 长期状态：按 UE GAS 职责校准后不修改插件、不导出完整 ASC。当前 ASC 预设提供结构，角色快照只保存已成立的 ASC 等级和预设属性 `BaseValue`；恢复使用当前预设重建 Cell 后覆盖基础值，失败时释放未发布 Cell。
- [x] 8.3 边界：永久技能、永久标签和职业成长尚无正式业务入口，本轮不提前保存未来集合；瞬时 Ability / GE / 冷却 / 临时标签 / Cue / Timeline 和战斗随机流继续排除。Gameplay 不读取 ECS Buffer，不复制 GAS 运行时。
- [x] 8.4 接入 GameCore 文件槽位与模板一致的存档玩家界面；不得新增第二套 SaveSystem。
- [x] 8.4 吸收裁决：GameCore `SaveSystem/SaveKit` 只负责槽位、文件、元数据和模块容器；`ScenarioDirector` 负责整局快照与原子恢复。模板 `SavedGamesUI/SavedGameSlot/ModalWindow` 的布局和交互等价复现，脚本职责全部替换。
- [x] 8.4 文件层：`SaveSystem` 已重构为 SaveKit 模块容器入口；旧 RPG `SaveDataBlock` 只是一个模块。槽位 API 全部改为整数 `slotId`，删除文件名解析、字母后缀和哈希映射；槽位 UI 仅读 `SaveMeta`。
- [x] 8.4 文件层 RED 精确缺少容器入口；GREEN 独立模块 + 旧世界块同槽往返 `1/1`，GameCore 全组 `96/96`。
- [x] 8.4 导演文件接入：`ScenarioDirector` 把活动 `ScenarioRunSnapshot` 注册到同一 SaveKit 整数槽位，保留槽内其它模块；读档先加载内容、校验快照并构造完整候选单局，场景成功后才替换活动单局和内容句柄。
- [x] 8.4 非角色端到端：统一 `FoundationTest` 场景已验证普通卡、回合和可见牌桌经真实槽位保存 / 读取后恢复，牌桌视图、交互和新输入系统统一改绑。该结果不覆盖角色 EX-GAS 状态。
- [x] 8.4 存档列表文件能力：GameCore 直接复用 SaveKit 的有效槽位枚举和删除，提供按整数槽位排序的元数据、诚实的单槽删除结果与删除全部计数；不自行扫描目录或增加第二套槽位索引。
- [x] 8.3 验证：角色定向 EditMode `9/9`、Gameplay EditMode 全量 `124/124`、真实角色槽位保存 / 读取 PlayMode `1/1`、Gameplay PlayMode 全量 `29/29`。
- [x] 8.4 模板存档 UI：动态存档列表、删除确认、清空全部、局内保存并返回标题的操作结果均由端到端测试通过；使用现有 SaveSystem / UIKit，不建立第二套槽位或文件入口。既有 GameView 图片只保留为过程诊断记录。
- [x] 9.1 内容校验：现有 `ContentValidator`、Console 对象上下文、内容引用选择器与行动槽位选择器已覆盖唯一 ID、EX-GAS 标签、类型引用、任务循环、剧本组成、行动槽位和结果引用；当前 Unity 扫描 11 个作者资产，零错误零警告。StackCraft 的同材料配方签名不恢复，因为同参与条件多行动是玩家可选项。
- [x] 9.2 剧本作者工具：`ScenarioDefinition` 通过类型受限选择器维护地区与任务唯一 ID，`ScenarioRegionDefinition` 独占 YooAsset 场景地址、牌桌规则和抵达位置；现有 SO Inspector 是唯一作者入口，不新增重复的剧本窗口。
- [x] 9.3 牌桌 / 关卡作者工具：地区内嵌牌桌边界、禁放区域、卡牌尺寸和堆叠步进；已补齐四个中文 Inspector 标签。StackCraft 分类堆叠矩阵依赖已排除枚举，不吸收为第二规则真相。
- [x] 9.4 可扩展编辑入口：YooAsset 按 `ContentAsset` 派生类型收集；代码 Mod 可派生内容、行动结果和任务子项，并由各自校验 / 运行 / 快照入口接入，中央索引不按 Mod 类型分支。正式 Mod 包加载、发布和游戏内编辑器仍属于后续模块，不在本步冒充完成。
