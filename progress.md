# Gameplay 地基工作记录

## 2026-08-18：StackCraft 表面 / 动画一致性口径纠偏

- 用户指出此前“一致”口径错误：机制、源码映射和代表性业务参数一致，不能证明卡牌表面、素材布局、进度条、命中反馈、拖拽手感、移动 / 受击 / 投射物 / 粒子动画也与模板一致。
- 当前正式口径改为：机制效果与代表性业务竖切已有自动化 / 只读审计证据；表面视觉与动画一致性尚未完成，不能据此宣称“和模板一致”“完整复刻”或“可以删除模板”。
- 新增专项审计入口 `.spec/knowledge/features/project/stackcraft-visual-animation-parity.md`，把 StackCraft 的卡牌 Prefab / 材质 / UI Prefab / VFX / 脚本动作与当前 `TabletopView`、测试 Prefab、生成器逐项对账。
- 下一步先补卡牌表面和必要反馈的源码 / Prefab 差异，再决定哪些视觉应迁入、哪些只做 CardLoop 风格等效；不再用“UI 外观不要求复制”跳过玩家看得到的模板效果。
- 第一轮静态表面订正已落盘：`TabletopCardView` 和测试场景生成器改为标题 / 价格 / 营养 / 当前生命分区显示，候选高亮改为四条外轮廓，命中造成实际伤害时触发卡牌本体闪白与 15 度摇晃；通过 UnitySkills 重新执行 `Gameplay/地基/重建测试场景` 后，`牌桌测试卡牌视图.prefab` 不再保留旧“表面详情”混合文本和整块绿色遮罩。
- 新鲜验证：Unity 编译 `0` 错误；`node .spec/tools/gameplay-static-preflight.mjs` 通过；`FoundationTestScenePlayModeTests` 最近一次 UnitySkills 结果为 `26/26` 通过，编辑器已退出 PlayMode。该结果只证明静态 Prefab / 字段 / 资源守卫和现有测试链路通过，不证明实际画面比例、字体落点、拖拽 / 移动 / 受击 / 投射物 / 粒子动画已经与 StackCraft 一致。

## 2026-08-17：StackCraft 当前一致性复核

- 本轮回答“和模板一致了没”的口径已收口：不能宣称 StackCraft 全部业务内容已完整迁移；可以宣称当前自动化覆盖的 StackCraft 机制效果已由 Gameplay 自有框架接管并通过，且 Starter / Beginning 代表性业务竖切已通过审计。
- 新鲜非 Unity 证据：`node .spec/tools/gameplay-static-preflight.mjs` 通过；额外扫描 `Assets/StackCraft` 下 708 个 Unity GUID，确认 `Assets/StackCraft` 外部正式文本资源没有引用这些旧模板 GUID。
- 新鲜 Unity 证据：UnitySkills 复用当前 `CardLoop_341F709E` 编辑器，`FoundationTestScenePlayModeTests` `26/26` 通过，全量 PlayMode `59/59` 通过，Unity 编译 `0` 错误。
- 全量 PlayMode 后 Console 出现的 `YokiFrame.YooInit 已由其它入口初始化` 是 `GameManagerInfrastructureLifecyclePlayModeTests.Start_WhenResourcesAreOwnedExternally_FailsWithoutTryingToCloseThem` 的预期负向测试日志；源码用 `LogAssert.Expect` 明确等待该异常。清空测试残留后 Console 复核为 `0` 错误。
- 阶段 C 机制清单当前版已写入吸收矩阵：当前没有已登记的“必须继续编码后才能覆盖 StackCraft 已审计机制效果”的缺口；按当前代表性业务验收口径，剩余 StackCraft 原业务 `.asset` 是后续可选迁移范围，不阻塞当前阶段。
- 本轮续跑文档与静态门禁：`node .spec/tools/spec-lint.mjs` 通过，`node --test .spec/tools/spec-lint.test.mjs` `2/2` 通过，定向 `git diff --check` 仅有换行提示，`node .spec/tools/gameplay-static-preflight.mjs` 通过。
- 业务数据对账：`Assets/StackCraft/Resources` 仍有卡牌 `103`、卡包 `11`、配方 `90`、任务 `66`、遭遇 `3` 个 `.asset`；当前 CardLoop 作者源明确映射了 Starter / Beginning 卡包和一批地基测试竖切内容，剩余业务已记录为后续可选迁移或放弃范围。
- 代表性业务审计：`node .spec/tools/stackcraft-business-representative-audit.mjs` 通过，覆盖 Starter 固定槽位、Beginning 三次打开槽位、权重、5 个配方候选、10% 配方概率和 Beginning 商贩价格 / 解锁任务数；该证据只证明代表性竖切，不证明 StackCraft 全量业务数据已迁移。

## 2026-08-17：自有素材目录规范订正

- 对照 2DRPGEngine / Mythril2D 的资源结构后，确认常规资源类别目录应使用英文约定名，例如 `Sprites`、`Textures`、`Audio`、`Prefabs`；中文应保留给 CardLoop 自有资源文件名、Prefab 根对象名和作者可见地址。
- 已把此前误放在 `Assets/Gameplay/素材/` 的自有原型素材迁移到标准目录：2D 卡面、图标和投射物进入 `Assets/Art/Sprites`，卡牌烟雾贴图进入 `Assets/Art/Textures`，卡牌烟雾材质进入 `Assets/Art/Materials`，卡牌烟雾粒子 prefab 进入 `Assets/Art/Prefabs`，音效进入 `Assets/Audio/SFX`；资源文件名继续使用中文现实名称。
- 测试场景生成器、静态预检、StackCraft 吸收矩阵、重审记录和任务计划中的当前素材路径已同步；旧 `Assets/Gameplay/素材` 目录与 `.meta` 已删除。
- 静态预检新增旧目录守卫：正式工程文本配置不得指向 `Assets/Gameplay/素材`，该目录重新出现也会失败。
- 当前资源引用审计结果：除 `Assets/Art/Sprites/箭矢投射物.png` 与 `Assets/Art/Sprites/魔法投射物.png` 暂无 GUID / 路径引用外，其它迁移素材均被测试场景、YooAsset 收集项、卡牌视图、牌桌视图设置、音效 Resolver 或粒子 prefab 链路引用。两个投射物贴图未使用的原因是当前投射物表现仍由 `Assets/Gameplay/Tests/牌桌/牌桌测试投射物.prefab` 使用占位卡面承担，箭矢 / 魔法弹贴图只是此前为后续投射物视觉预留，尚未接入正式测试 prefab；它们已保留在 `Assets/Art/Sprites`，不删除。

## 2026-08-17：阶段 C 当前阻塞口径订正

- 重新按项目入口读取 `.spec` 核心规范、StackCraft 吸收矩阵、计划和进度记录；确认当前任务仍是 StackCraft 玩家效果吸收审计，不切到原创《卡牌生存：无限》业务。
- 新鲜静态预检：`node .spec/tools/gameplay-static-preflight.mjs` 通过，仅保留“没有 .sln / .csproj，C# 编译必须留到 Unity”的预期提示。
- Unity guard 已放行 editor-automation；AIBridge 文件桥只读状态显示编辑器未播放、未暂停、未编译、未更新。
- 使用 StackCraft 源码脚本名对 `.spec/knowledge/features/project/stackcraft-system-reference-matrix.md` 做覆盖对账，当前所有 `Assets/StackCraft/Scripts/**/*.cs` 脚本名均已在矩阵出现；这只证明“没有未登记脚本名”，不证明每个玩家效果已经最终完成。
- 删模板前依赖审计第一轮：正式代码、测试、项目资源、场景、ProjectSettings 和 Packages 中只剩说明性 `StackCraft` 注释；未发现正式路径引用 StackCraft 资产 GUID、`Assets/StackCraft` 资源路径或 `CryingSnow` 源码命名空间；这些检查已固化进 `node .spec/tools/gameplay-static-preflight.mjs`，并覆盖整个 `Assets` 根、ProjectSettings 和 Packages。
- 已订正阶段 C 当前阻塞：2026-08-16 后补切片的 Unity 补跑已由全量 PlayMode `59/59` 覆盖，不再作为当前机制阻塞；后续阻塞不是机制编码缺口，而是业务数据迁移、试玩裁决、删除授权和删除后 Unity 验证。

## 2026-08-16：Unity 独占后阶段 C 回归收口

- 修复常驻 HUD 在 Gameplay 输入状态下无法接收鼠标点击的问题：`GameCore.InputSystem` 在切到 Gameplay 动作图后显式保持 `InputSystemUIInputModule` 的 Point / Click / Scroll 指针链启用，导航和 Submit 仍只由 UI 动作图通过 `EventSystem.sendNavigationEvents` 处理。
- `ScenarioContentPlayModeTests` 补齐和其它 PlayMode 用例一致的临时 SaveKit 目录隔离，避免真实持久目录槽位已满导致新剧本启动失败；生产存档逻辑未改。
- 通过正式生成器 `FoundationTestSceneMenu.RebuildTitleTestScene()` 重建标题入口测试场景和 `ScenarioTitlePanel.prefab`，补回已在生成器中定义但旧 prefab 尚未落盘的 `DayDuration` 日长滑条。
- 新鲜验证：`FoundationTestScenePlayModeTests` `31/31`、`ScenarioContentPlayModeTests` `8/8`、`ScenarioTitleScreenPlayModeTests` `5/5`、全量 PlayMode `58/58` 通过；`node .spec/tools/gameplay-static-preflight.mjs` 通过，仅保留“没有 .sln / .csproj，C# 编译必须留到 Unity”的预期提示。
- 这次验证证明 Unity 可运行链路已覆盖上述等待补跑项；阶段 C 仍是 StackCraft 完整玩家效果审计中，不能据此宣称模板已经全部吸收或可以删除参考工程。

## 2026-08-16：StackCraft 菜单未读红点吸收

- 对证 StackCraft `MenuView` / `RecipesView` / `QuestsView` / `GameData.SeenItems` 后，确认需要吸收的是“新任务 / 新发现配方在列表和页签上显示红点，首次查看后变为已读”的玩家反馈。
- CardLoop 不恢复 `MenuView`、旧 `TextButton`、旧红点字符串或 `GameData.SeenItems` DTO。已读事实由当前单局 `ScenarioRun` 拥有，并随 `ScenarioRunSnapshot` 保存 / 恢复；UI 不保存第二份任务或发现状态。
- `ScenarioJournalPanel` 在任务页和“已发现配方 / 行动”页投影未读红点，刷新当前可见页后把本页条目标记为已读；隐藏页签仍能提示该页存在未读内容。
- 任务分组折叠和配方分类折叠继续排除：它们依赖 StackCraft 旧 `QuestGroup`、`RecipeCategory` 和旧 Manager 结构，当前项目没有对应正式作者源。
- 本轮已补单局快照回归和日志面板 PlayMode 断言；非 Unity 静态问题已先收口：清理上次 batch 残留 `Temp/UnityLockfile`，修复 `Assets/Audio/SFX/界面点击.wav.meta` 缺少文件末尾换行导致的 YAML 解析失败，并静态补齐 `Assets/Gameplay/Tests/牌桌/牌桌测试视图设置.asset` 的投射物视图引用与排序值。仍需按 Unity guard 跑新鲜编译 / 测试后才能把该切片标为验证完成。
- 已补项目验证顺序规范：StackCraft 吸收先完成代码级预检，再进入 Unity 编辑器 / PlayMode。新增 `node .spec/tools/gameplay-static-preflight.mjs` 作为不启动 Unity 的静态预检入口；当前运行通过，仅提示仓库没有独立 `.sln/.csproj`，C# 编译必须留到 Unity 阶段。当前静态扫描未发现正式 Gameplay 源码恢复 StackCraft 旧 Manager / 单例 / DTO / 旧资源路径 / 直接查找依赖；测试临时存档目录前缀已从 `CardLoop-*` 改为 `Gameplay-*`，避免旧项目名继续误导。

## 2026-08-16：StackCraft 卡牌悬浮信息 / 堆叠摘要吸收

- 对证 StackCraft `CardInstance.GetInfo` / `CombatStats.GetFormattedStats` 后，确认模板悬浮信息还包含三类上下文反馈：活动制作 / 行动的名称与剩余时间、多卡牌堆的聚合数量摘要、角色卡的当前生命与战斗明细。
- `TabletopCardInfoPanel` 已接管文本化上下文反馈：在现有单卡描述、箱子存币、商贩价格 / 收藏进度、角色已装备列表基础上，追加所属牌堆聚合摘要和进行中行动名称 / 剩余回合 / 约合秒数。
- `ActionInstance` 只新增参与卡查询与剩余回合查询，`TabletopView` 和详情面板复用同一只读入口；行动状态、牌堆成员、进度和结算仍由 `Tabletop` / `ActionInstance` 拥有。
- 本轮不恢复 StackCraft `InfoPanel`、`CraftingManager`、旧 `CombatStats` 或第二份 UI 状态；角色生命仍由卡牌视图显示，角色战斗明细留给 GNS / EX-GAS 公开属性边界后续裁决。
- 同步复核 `TradeZone` / `CardBuyer` / `PackSlot` / `PackInstance`：出售、购买、卡包按槽打开、未发现配方优先抽取和收藏进度已有正式行动链或前序缺口登记；本轮没有发现新的交易 / 卡包结算缺口。
- 新鲜静态验证：定向 `git diff --check` 通过；构造调用扫描确认没有旧 `TabletopCardInfoPanelData` 一参入口残留。Unity 自动化仍被同工程 Unity / ShaderCompiler 与 `Temp/UnityLockfile` 阻塞，待独占后补跑 PlayMode。

## 2026-08-16：StackCraft 剩余 UI / 商贩提示静态审计

- 对证 StackCraft `TradeManager.PlayActivationSequence` / `PackVendor` 后，确认“任务数达标后提示卡包商贩解锁”的玩家反馈尚未完整吸收：模板会锁输入、暂停时间、显示 `Pack Unlocked`、镜头聚焦商贩并短暂高亮。
- CardLoop 现在已覆盖商贩解锁门槛、分批付款、成交生成卡包、付款归零、收藏进度、购买任务事实和一次性解锁反馈。`ScenarioRun.RefreshQuestState` 比较任务完成数前后变化，跨过 `PackVendorDefinition.MinimumCompletedQuests` 时向当前牌桌发布镜头聚焦和卡牌高亮表现提示；不保存第二份 `isUnlocked`。
- 本轮不恢复 `TradeManager`、`InfoPanel`、`InputManager`、`TimeManager` 或旧输入锁定协程；提示只作为已提交任务事实后的只读表现，不参与交易结算和商贩状态 owner。
- 对证 StackCraft `MenuView` / `RecipesView` / `QuestsView` / `GameData.SeenItems` 后，确认日志面板当时已覆盖任务 / 配方查看与刷新；同日后续已把新内容红点和首次查看已读状态接入当前单局快照。分组 / 分类依赖旧枚举和旧 Manager，不回流。
- `ScreenFader` 与 `ProgressUI` 复核后仍维持既有裁决：转场淡入淡出归 `TransitionSystem`，行动世界进度归 `TabletopView` 的行动进度视图；本轮未发现新的运行玩家效果。
- 新鲜验证：`spec-lint` 通过，规范测试 `2/2` 通过，定向 `git diff --check` 通过；Unity `PackVendorEditModeTests` `10/10` 通过。Unity batch 成功后留下的陈旧 `Temp/UnityLockfile` 已按项目守卫脚本确认无 Unity 进程后清理。

## 2026-08-16：GameCore / EX-GAS 集成文档缺口口径订正

- 对照当前 `CharacterCard`、`CharacterAbilitySystemSnapshot`、`GameplayEffectDamageSystem`、`TabletopView` 和阶段 C 记录后，确认 `.spec/knowledge/features/gamecore-gas.md` 存在旧口径：仍把角色卡 ASC 快照、权威随机种子接入、投射物、战斗音效和 HitUI 式反馈写成未完成缺口。
- 已把该文档订正为当前事实：角色卡只保存 ASC 等级、预设属性 `BaseValue` 和装备快照；牌桌战斗通过 `ScenarioRun -> Tabletop -> Battle` 派生权威种子；卡牌烟雾、投射物、音效和命中图标都是牌桌表现链，不承担规则结算。
- 同步给 `gameplay-foundation-reaudit.md` 增加“当前阅读口径”：该文档保留模块 1-6 历史回审过程，2026-08-16 以前的旧缺口不自动代表当前事实；阶段 C 最新事实以矩阵、`task_plan.md` 和本进度为准。
- 这次只修知识库事实，未新增玩法代码。新鲜验证：`.spec` lint 通过，规范测试 `2/2` 通过，旧缺口短语扫描无残留；Unity 自动化仍被同工程 Unity / ShaderCompiler 和 `Temp/UnityLockfile` 阻塞。

## 2026-08-16：StackCraft 未登记辅助类与作者工具审计

- 对证 StackCraft 脚本覆盖表后，未在矩阵点名的剩余脚本主要是旧模板 Editor 抽屉、空类型标记、局部值对象和接口，不是新的玩家运行效果。
- `ChestDefinition` 已由 `ChestCardDefinition` / `ChestCard` 的容量与存币状态覆盖；`GrowerDefinition` / `ResearchDefinition` 是空类型标记，按项目规范不迁入，相关效果继续由卡牌作者源和行动链表达。
- `PackEntry` 已由 `CardPackEntry` 覆盖；新框架权重是相对权重，不要求归一化到 100。模板归一化按钮只作为未来作者工具体验参考，不成为当前第二套权重真相。
- `StatType` / `Stat` / `StatModifier` 继续排除，数值与装备修正归 GNS / EX-GAS；`IClickable` / `IOnStackable`、`InputManager` 和 `VectorExtensions.Flatten` 不建立正式入口，分别由行动链、正式输入状态和局部算法处理。
- StackCraft 的 `CardDefinitionEditor`、`ChestDefinitionEditor`、`EnclosureDefinitionEditor`、`GrowerDefinitionEditor`、`LimitBoosterDefinitionEditor`、`ResearchDefinitionEditor` 等旧 `*Editor` 与 `RenderPipelineSwitcher` 不迁入。当前作者入口继续用 Odin 中文标签、类型受限内容引用、内容校验和现有 SO Inspector；后续若需要权重归一化或冲突可视化，应在当前 Gameplay 作者源上做专用工具。
## 2026-08-16：StackCraft EquipmentPanel 装备可读反馈吸收

- 对证 StackCraft `EquipmentPanel`：模板装备卡离桌后，仍通过角色旁装备面板让玩家看到已装备物品。
- CardLoop 不恢复装备面板单独脚本、漂浮装备卡、材质槽位、`InfoPanel` 或 `TimeManager` 锁交互。当前由 `CharacterCard` 暴露只读装备枚举，`TabletopCardInfoPanel` 在角色详情里追加“已装备”列表，读取装备槽位和装备卡作者源显示名称。
- 装备事实仍由角色卡唯一拥有，装备 / 卸装继续由行动结算和 EX-GAS GE 处理；UI 只是投影，不保存或同步第二份装备状态。已补装备 EditMode 只读枚举断言；Unity 自动化等待同工程进程空闲后补跑。
## 2026-08-16：StackCraft WorldCanvas 与候选高亮吸收

- 对证 StackCraft `WorldCanvas`：模板只是把 Canvas 切成世界空间并绑定 `Camera.main`。CardLoop 不恢复全局 `WorldCanvas.Instance` 或运行时相机查找，牌桌世界空间表现继续由唯一绑定的 `TabletopView` Transform 承载，屏幕 UI 继续归 UIKit。
- 对证 StackCraft `Highlight`：模板的玩家效果是拖拽候选目标显示高亮。当前由 `TabletopCardDragInput`、`TabletopView.SetDropTargetHighlight` 和 `TabletopCardView` 的“候选高亮”子节点承担，不新增独立高亮类、材质实例管理器或第二表现状态。
- 现有 Foundation PlayMode 已断言拖拽到候选卡牌时 `IsHighlighted` 为真，释放后为假；源码不需要改。Unity 自动化仍需等同工程 Unity / ShaderCompiler 空闲后补跑。
## 2026-08-16：StackCraft 暂停灰阶与日终暗角吸收

- 对证 StackCraft `BuiltInPostProcess` / `CustomPostProcessFeature` / `CustomPostProcess.shader`：模板暂停时淡入灰阶，退出暂停时淡出；日终处理时淡入暗角，新一天开始后淡出。
- CardLoop 不恢复模板 `TimeManager`、DOTween、`OnRenderImage`、RendererFeature 或模板后处理 Shader。当前由 `ScenarioScreenEffectView` 只读投影 `GameStateSystem.Menu` 和 `ScenarioRun.DayCyclePhase`，不保存第二份暂停、天数或菜单状态。
- 地基测试运行根新增中文对象“剧本屏幕效果”和 URP `VolumeProfile` 作者资源 `Assets/Gameplay/Tests/牌桌/剧本屏幕效果配置.asset`；主相机由生成器开启 URP 后处理。
- PlayMode 合同已补暂停灰阶进入 / 恢复和日终暗角进入 / 新日恢复；源码定向 `git diff --check`、`.spec` lint 与规范测试通过。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑。

## 2026-08-16：StackCraft CardStatsUI 状态 HUD 吸收

- 对证 StackCraft `CardStatsUI` 与 `CardManager.GetStatsSnapshot()`：模板常驻显示食物营养 / 需求、当前货币和卡牌数量 / 上限，并在日终处理期间隐藏。
- CardLoop 不恢复 `CardStatsUI`、`StatsSnapshot` 管理器、`CardManager.OnStatsChanged`、`CardCategory.Currency` 或全局统计事件；正式 owner 是当前单局 `ScenarioRun` 和现有 UIKit `ScenarioTurnPanel`。
- `ScenarioRun.GetTabletopStats()` 从全部地区牌桌即时派生统计：食物读取 `FoodCardDefinition` 与剩余使用次数，需求读取日终规则和角色卡数量，货币从箱子声明和售卡结果声明的货币卡推导，卡牌数量沿用现有 `CountsTowardCardLimit` 与卡牌上限加成。
- `ScenarioTurnPanel` 在普通阶段把统计追加到现有日程 HUD 文案；进入日终阶段后继续更新只读缓存但不显示统计，保留日终提示 / 遭遇文案。
- 新鲜静态验证：定向 `git diff --check` 通过；Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建测试资源并补跑相关 PlayMode。
## 2026-08-16：StackCraft 设置面板吸收

- 对证 StackCraft `GameOptionsUI` / `GraphicsManager` / `AudioManager`：模板设置面板包含分辨率、全屏、垂直同步、帧率上限、阴影预设、SFX 音量、BGM 音量、Reset 和 Close。
- CardLoop 不恢复 `GraphicsManager`、`AudioManager`、模板单例、模板 `PlayerPrefs` 键或 `PlayerPrefs.DeleteAll()`；正式 owner 是进程级 `DisplaySettingsSystem`、现有 `AudioSystem` 和 UIKit 设置面板 `UISettings`。
- 新增 `DisplaySettingsSystem` 接管显示设置与 `_UnscaledTime` Shader 全局值；音频设置继续由 `AudioSystem` 管理，并新增只清理自身音频键的重置入口。
- `UISettings` 只把按钮映射到正式系统入口；Reset 走现有确认弹窗，只重置显示和音频设置，不删除存档、Mod 配置或其它系统偏好。
- 测试场景生成器已补运行时根 `DisplaySettingsSystem`、背景音乐 / 玩法音效 / 界面音效通道和设置面板按钮引用；Unity 自动化仍被同工程 Unity / ShaderCompiler 进程与 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑 PlayMode。

## 2026-08-16：StackCraft UI 点击音效吸收

- 对证 StackCraft `TextButton`、`MenuToggle`、`DayTimeUI` 和 `GameplayPrefsUI`：模板的菜单按钮、日程按钮和设置开关点击后都会播放 `AudioId.Click`。
- CardLoop 不恢复 `AudioManager`、`AudioId.Click` 或模板文本按钮体系；正式 owner 是现有 `GameConfig` UI 提交音效、`UINavigationTarget` 和 `AudioSystem`。
- 新增项目自有中文音效素材 `Assets/Audio/SFX/界面点击.wav`，并由测试生成器生成 `界面点击音效` 解析器写入 `GameConfig.m_submitSound`；测试运行根同步配置 `InterfaceSoundFX` 通道。
- 测试场景生成器会给生成出的按钮和标题开关挂 `UINavigationTarget`；鼠标点击现在检查被点按钮本身是否可交互后播放提交音效，不依赖当前选中对象。
- 新鲜静态验证：新增音效 `.meta` GUID 未重复，定向 `git diff --check` 通过。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建 Prefab / 场景并补跑 PlayMode。
## 2026-08-16：StackCraft 日程 HUD 推进模式入口吸收

- 对证 StackCraft `DayTimeUI`：模板存在玩家点击日程 HUD 切换时间推进的入口；这是玩家操作入口，不是模板 `TimeManager` 速度枚举本身。
- CardLoop 不恢复 `Paused / Normal / Fast`、全局 `Time.timeScale` 速度系统或第二套时间真相；普通行动只在现有 `ScenarioRun` / `Tabletop.ProgressionMode` 的回合制与即时制之间切换，战斗仍始终按真实秒数推进。
- `ScenarioTurnPanel` 新增推进模式按钮并调用现有 `ScenarioRun.UseRealTimeProgression()` / `UseTurnBasedProgression()`；即时制回合中途不能无损切回时按钮禁用并显示“即时推进中”，日终阶段显示“日终处理中”。
- 测试场景生成器已补 `ProgressionMode` 按钮和中文默认文案，PlayMode 用例改为真实点击 HUD 按钮，不再直接调用运行时方法代替玩家操作。现有 Prefab 仍需等 Unity 独占后由生成器重建，不能手工拼序列化引用。
- 新鲜静态验证：定向 `git diff --check` 通过，`.spec` lint 通过；`TabletopViewSettings` 的 `XTag` 引用已改为完整命名空间，不再触发先前缺少 `XTag` 名称的源码错误。Unity batch 与 UnitySkills 均被同工程两个 Unity Editor、两个 ShaderCompiler 和 `Temp/UnityLockfile` 阻塞。

## 2026-08-16：StackCraft 任务 / 配方菜单吸收口径订正

- 对证 StackCraft `QuestsView` / `RecipesView`：模板玩家效果是菜单里查看任务进度、查看已发现配方，并在任务或发现状态变化后刷新。
- CardLoop 不恢复模板 `MenuView`、`QuestManager`、`CraftingManager` 或配方分类 UI。当前正式 owner 是单局 `ScenarioRun` 与 UIKit 面板 `ScenarioJournalPanel`：任务读取 `QuestLog`，配方 / 行动读取本局发现集合。
- 本轮把日志页签和运行时标题从“已发现行动”改为“已发现配方 / 行动”，明确承接模板 RecipesView 的玩家语义；代码仍使用 `ActionDefinition`，因为当前地基里配方和交互行动共用同一个作者源。
- 同步为 `ScenarioJournalPanel` 的 Inspector 暴露字段补中文 `LabelText`，符合项目中文作者入口规范。Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑日志面板 PlayMode。

## 2026-08-16：StackCraft 牌桌相机平移 / 缩放 / 聚焦吸收

- 对证 StackCraft `CameraController`：模板牌桌相机支持中键拖拽平移、鼠标滚轮缩放，并在遭遇 / 解锁等表现序列中聚焦到目标牌桌位置。
- CardLoop 不恢复 `CameraController`、`Board` 单例、旧输入读取或 DOTween。当前由主相机上的 `TabletopCameraController` 消费正式 `GameCore.InputSystem` 的中键与滚轮输入；牌桌聚焦由 `TabletopPresentationCueKind.CameraFocus` 携带牌桌坐标，`TabletopView` 不处理该提示，避免表现层和镜头组件重复消费。
- `CameraShake` 不再在震动开始时强制把相机局部 XY 归零，避免命中 shake 把玩家平移后的牌桌视角拉回原点。`FoundationTest` 生成器会把唯一 `TabletopView` 写入主相机的 `TabletopCameraController` 并保存后回读校验。
- PlayMode 回归已增加正式输入平移和表现提示聚焦断言；同时把命中图标断言从 StackCraft 英文 Sprite 名改为项目中文素材名。Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑。

## 2026-08-16：StackCraft 遭遇提示文本与素材命名收口

- 对证 StackCraft `EncounterDefinition.NotificationMessage` 与 `EncounterManager.ExecuteEncounter`：日终遭遇可以先给玩家显示事件说明，再生成卡牌并播放烟雾反馈。
- CardLoop 不恢复 `EncounterManager`、`InfoPanel` 或独立遭遇系统；遭遇仍归 `ScenarioRun` 的日终规则处理，新增“提示文本”作者字段，并在 `ScenarioDayEncounterResult` 与现有回合 HUD 中展示。
- 日终测试剧本、测试场景生成器和回归断言已同步“夜里传来了陌生脚步声。”，现有资产本体已定点补字段，避免依赖重建场景才生效。
- 自有粒子 prefab 已确认放在 `Assets/Art/Prefabs/` 并使用中文文件名 / Prefab 根对象名，贴图与材质分别放在 `Assets/Art/Textures` 和 `Assets/Art/Materials`；旧 `Puff` 兼容字段存量为零后，已删除 `TabletopViewSettings` 上的旧来源兼容标记，规范补充“无真实存量时删除旧来源兼容名”。
- 新鲜静态验证：相关调用点扫描无漏改；`git diff --check`、`.spec` lint 和规范测试通过。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程与 `Temp/UnityLockfile` 阻塞，待环境独占后补跑场景重建与 PlayMode。

## 2026-08-16：StackCraft 暂停菜单吸收

- 对证 StackCraft `PauseMenu`：玩家按 Cancel/Esc 打开或关闭暂停菜单，继续按钮恢复游戏，设置按钮打开选项，标题按钮保存并返回标题；旧实现通过 `TimeManager.SetExternalPause`、`GameDirector.Instance.BackToTitle` 和固定标题场景串联。
- CardLoop 不恢复 `TimeManager`、旧 `PauseMenu`、旧 `GameDirector` 单例或固定场景名。当前由 `ScenarioPauseInput` 订阅正式 `GameCore.InputSystem.OpenGameMenu`，`ScenarioPausePanel` 走 `UISystem/UIManager` 菜单栈，暂停语义由 `GameStateSystem.Menu` 和 `Time.timeScale=0` 统一承担。
- 设置面板压入同一 UIKit 菜单栈；关闭设置后回到暂停菜单。继续按钮弹出暂停菜单并恢复 Gameplay 输入。保存并退出复用 `ScenarioDirector.SaveActiveRunToSlot` 与 `EndScenarioAsync`，不直接切标题场景。
- 新增统一地基 PlayMode 回归 `FoundationMenu_PauseSettingsAndContinueUseFormalMenuStack`，覆盖 Esc 打开、设置压栈、设置关闭、Esc 关闭、再次打开和继续恢复。当前已完成源码静态检查；仍需按 Unity guard 重建测试场景并运行该用例。

## 2026-08-16：正式场景入口排除参考模板和插件 Demo

- 横向扫描发现 `GameCoreSceneMenu.g.cs` 仍生成 StackCraft 参考场景和插件 Demo 场景菜单；源头是 `SceneUtil` 从整个 `Assets` 查询场景，导致参考模板场景可能进入正式场景菜单和场景地址选择器。
- 已将 `SceneUtil` 的正式场景快照收窄到 `Assets/Scenes` 与 `Assets/Settings/Scenes`，并同步当前生成菜单；StackCraft 参考场景、插件 Demo 和 Recovery 场景不再自动进入正式入口。
- 规范已补充：场景地址选择器、编辑器场景菜单和正式场景快照只能收项目场景根下的正式场景，不能把参考模板或第三方样例场景当正式 YooAsset 场景地址。
- 新鲜验证：静态搜索确认生成菜单不再包含 `Assets/StackCraft/Scenes`，`.spec` lint 与规范测试仍通过；Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞。

## 2026-08-16：StackCraft 原型素材迁入中文项目资源

- 继续执行“模板可删”的地基要求：测试场景生成器不再直接读取 `Assets/StackCraft/Sprites` 或 `Assets/StackCraft/Sounds/SFX`，卡面、命中图标和音效改为读取 `Assets/Art/Sprites`、`Assets/Audio/SFX` 下的中文项目素材。
- 已同步现有测试内容资产、牌桌测试 Prefab 和 YooAsset 测试收集项：卡面地址改为 `卡牌占位图`，GUID 改为项目自有图片 `0f54d867b9639e44c646777766827b09`；卡牌烟雾粒子继续使用 `Assets/Art/Prefabs/卡牌烟雾粒子.prefab`。
- `TabletopCardView` 的 Inspector 提示去掉“临时复用 StackCraft 原型素材”表述，改为项目图标配置；参考来源仍保留在吸收记录里，不作为正式资源路径。
- 新鲜验证：静态搜索确认生成器、牌桌表现和 `Assets/Gameplay` 范围内没有 `Assets/StackCraft/Sprites` / `Sounds` 直接路径、旧卡面地址或旧 GUID；`.spec` lint 通过，规范测试 `2/2` 通过。`git diff --check` 对 Unity YAML 资产仍报告历史空字段尾随空格，不能作为本轮通过证据；Unity 自动化仍需等待独占环境后重建 `FoundationTest` 并补跑 PlayMode。

## 2026-08-16：StackCraft Board / LimitBooster 动态牌桌边界吸收

- 对证 StackCraft `Board` 与 `LimitBoosterDefinition`：加成卡不只增加容量，也扩大可摆放桌面；桌面收缩时模板会把牌堆重新拉回边界内。
- CardLoop 不恢复 `Board` 单例、BlendShape 权威状态或 `CardManager`。当前 `Tabletop` 以剧本 / 地区基础放置规则为作者源，再按桌面卡牌 `CardLimitBonus` 派生当前边界；收缩时复用已有牌桌放置解算重排。
- `ScenarioRun` 的容量统计改为读取各地区 `tabletop.CardLimitBonus`，避免剧本侧和牌桌侧分别计算同一加成。
- 新增边界跟随加成并在收缩时重排的公开契约测试。当前已完成静态检查与 `.spec` 校验；Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，新增用例尚未运行。
- 素材规则确认：参考模板粒子若改造成项目自有素材，必须放入 `Assets/Art/Sprites`、`Assets/Art/Textures`、`Assets/Art/Materials`、`Assets/Art/Prefabs`、`Assets/Audio/SFX` 等标准资源目录，并使用中文资源名；本轮已将模板烟雾粒子迁入为 `Assets/Art/Prefabs/卡牌烟雾粒子.prefab`，并登记到 YooAsset 测试收集配置。Unity 自动化仍需等待独占环境后重跑。

## 2026-08-16：StackCraft 日终 Game Over 吸收

- 对证 StackCraft 文档 3.6：日循环中如果牌桌没有人物卡，应进入 Game Over，提示玩家返回标题，并清除当前活动存档。
- CardLoop 不新增 GameOverSystem。已有 `ScenarioRun` 在日终进食后统计角色卡，没有幸存角色时进入 `ScenarioDayCyclePhase.GameOver`；`ScenarioTurnPanel` 把确认按钮转交 `ScenarioDirector.GameOverAsync()`，由导演删除当前槽位并结束单局。
- 新增回归测试覆盖“最后一个角色因饥饿死亡后进入 GameOver，且不能继续新日”。当前只能完成静态校验；Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，新增用例尚未运行。

## 2026-08-16：StackCraft CardAI 拖拽中暂停自动行为

- 对证 StackCraft `CardInstance.IsBeingDragged` / `CardAI.CanMove()`：被本地拖拽持有的卡牌不会推进自动行为；效果只作用于被持有的卡，不暂停整个牌桌。
- CardLoop 不恢复 `CardAI`、旧协程或拖拽状态表。`TabletopCardDragInput` 在按下命中卡牌后通知当前 `Tabletop` 持有该卡，释放或取消时释放。
- `Tabletop.AdvanceRealTime` 在周期产出和自动移动计时前跳过被持有卡牌，释放后必须重新等待完整间隔；该状态不进入作者源、存档、联机协议或事件总线。
- 新增回归测试覆盖拖拽期间不累计周期产出 / 自动移动时间。当前只能完成静态校验；Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，新增用例尚未运行。

## 2026-08-16：StackCraft CardAI 围栏容量吸收

- 对证 StackCraft `EnclosureDefinition` / `EnclosureLogic` / `CardAI.ShouldStayInEnclosure`：围栏只约束同一牌堆内的自动移动，非敌对自动移动卡位于围栏卡上方且在容量范围内时，跳过本次自动移动。
- CardLoop 不恢复 `EnclosureLogic`、特殊围栏运行组件或工位系统。自动移动留存容量落在 `CardDefinition`，由当前唯一牌桌 `Tabletop.AdvanceRealTime` 在自动移动请求入队前按牌堆顺序判断。
- 敌对角色卡忽略留存容量，继续执行追击、开战和增援；玩家拖拽拆堆不受影响，普通行动和战斗占用仍按既有正式入口阻止自动移动。
- 新增 EditMode 合同覆盖“容量内非敌对卡留在牌堆、容量外卡照常抽出移动”，并补充作者校验负容量报错。当前只能完成静态校验；Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，新增用例尚未运行。

## 2026-08-16：StackCraft CardAI 敌对追击与战斗加入吸收

- 对证 StackCraft `CardAI` 敌对自动行为：敌对卡优先靠近并加入玩家相关战斗；没有可加入战斗时寻找最近玩家角色，进入攻击半径后开战，未进入半径则向玩家移动。
- CardLoop 不恢复 `CardAI`、旧协程、`CombatManager`、固定 Player / Mob 分组或第二套阵营字段。敌对身份读取角色唯一 EX-GAS 阵营标签，索敌 / 攻击半径落在 `CharacterCardDefinition`，执行由唯一牌桌 `Tabletop.AdvanceRealTime` 推进。
- 敌对自动行为和非敌对巡逻一样先只抽出自身一张卡；开战、增援、移动均走当前 `Tabletop` / `Battle` 正式入口，继续复用权威随机、战斗区域合并和 EX-GAS 结算链。
- 新增 EditMode 合同覆盖“敌人进入攻击半径后发起战斗”和“敌人靠近已有玩家战斗后加入敌方”。当前只能完成静态校验；Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，新增用例尚未运行。

## 2026-08-16：StackCraft CardAI 非敌对随机巡逻吸收

- 对证 StackCraft `CardAI.AutoMove` / `MoveRandomly`：非敌对可移动卡按移动间隔周期性随机巡逻；候选点必须已经在桌面有效区域内，无效点只重试，不由放置解算夹回桌面。
- CardLoop 不恢复 `CardAI`、旧协程或 AI 总管。自动移动配置落在 `CardDefinition` 的“自动移动间隔秒数 / 自动移动半径 / 自动移动尝试次数”，运行时由唯一牌桌 `Tabletop.AdvanceRealTime` 使用权威随机推进。
- 自动巡逻提交仍走 `Tabletop.TryPlaceSingleCard`，所以多卡堆中只抽出自身一张卡，剩余牌堆保持顺序；参与普通行动或战斗的卡牌不自动移动。
- 新增公开契约测试覆盖“计时触发后只移动中间自动卡”。当前只能完成静态校验；Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，新增 EditMode 用例尚未运行。

## 2026-08-16：StackCraft CardAI 自动移动前置 - 单卡抽出

- 对证 StackCraft `CardAI.EnsureDetachedFromStack` / `DetachFromStack`：自动移动前如果 AI 卡所在牌堆超过一张，模板只把 AI 卡自己抽成新牌堆，不会像玩家拖拽那样带走它上方的尾段卡牌。
- CardLoop 不恢复 `CardAI`、旧协程或 AI 总管。本次只在正式牌桌对象层补足“只移动单张卡牌”的原子放置入口：`Tabletop.TryPlaceSingleCard` -> `TabletopCards.TryPlaceSingleCard` -> `TabletopCardStack.DetachSingleAt`。
- 该入口保留卡牌局内 ID、内容 ID、角色卡状态和周期产出累计秒数；剩余牌堆保持相对顺序，放置仍走当前牌桌唯一 `TabletopCardStackPlacementSolver`，不增加第二套位置状态。
- 新增 EditMode 合同覆盖“抽出中间卡时顶牌仍留在原堆，选中卡独立成堆并保留运行状态”。Unity 自动化仍被同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，测试尚未运行。

## 2026-08-16：StackCraft CardAI 周期产出吸收

- 对证 StackCraft `CardAI.StartAI`、`ProduceLoop` 和 `SpawnProduce`：非敌对卡如果配置了 `ProduceCard`，会按 `ProduceInterval` 周期在自身位置生成目标卡，并播放卡牌烟雾反馈；模板源码在卡牌被拖拽、所属牌堆制作中或在战斗中时跳过本次产出。
- CardLoop 不恢复 `CardAI`、`CardManager.CreateCardInstance`、旧协程或移动 AI 总管。周期产出是卡牌内容作者源的一项数据，落在 `CardDefinition` 的“周期产出卡牌 / 产出间隔秒数”；运行时由当前唯一牌桌 `Tabletop.AdvanceRealTime` 推进。
- 牌桌创建产物时仍走正式 `Tabletop.CreateCard`、放置预检和 `TabletopPresentationCueKind.CardSmoke` 表现提示；产出累计秒数保存在 `TabletopCard` 并进入卡牌快照，角色卡继承同一张卡牌实例状态。
- 当前吸收“非敌对卡周期产出 + 卡牌烟雾反馈”的主体效果，并已跳过活动行动和战斗中的卡牌；拖拽中的跳过需要先确立输入层与单局实时推进的正式交互暂停策略，不在 `Tabletop` 里新增临时拖拽锁。StackCraft 自动移动、敌对追击、围栏容量和战斗加入逻辑仍按后续子切片审查。
- 新鲜静态验证：周期产出构造调用点扫描无漏改；定向 `git diff --check`、`.spec` lint 和规范测试通过。Unity 自动化仍被 guard 阻塞：当前同工程存在 2 个 Unity Editor、2 个 ShaderCompiler 和 `Temp/UnityLockfile`，新增 EditMode 用例尚未运行。

## 2026-08-15：StackCraft 卡牌烟雾粒子反馈吸收

- 对证 StackCraft `PuffParticle`、`CardInstance.PlayPuffParticle`、`RecipeDefinition`、`ChestLogic`、`TradeZone`、`EncounterManager` 和 `CardAI`：模板的卡牌烟雾反馈是空间粒子 + `Puff.wav` 音效，常见于制作 / 生成、卡牌死亡或耗尽、箱子存取币、交易区成交、日终遭遇生成和非攻击生物产出。
- CardLoop 不恢复 `PuffParticle`、`AudioId.Puff`、`AudioManager` 或新的 VFX Manager。规则层只提交只读 `TabletopPresentationCue`，并在需要空间反馈时携带牌桌坐标；实例化、音效播放和资源释放都由当前 `TabletopView` 负责。
- `TabletopViewSettings` 新增卡牌烟雾粒子预制体、卡牌烟雾反馈音效和排序值作者配置；对应图片、材质、粒子预制体和音频统一落到 `Assets/Art/Sprites`、`Assets/Art/Textures`、`Assets/Art/Materials`、`Assets/Art/Prefabs`、`Assets/Audio/SFX` 标准资源目录，并继续通过 `SoftAssetReference` / `ResourceSystem` 加载。
- 已接入触发点：行动产物 / 卡牌耗尽 / 普通移除、购买卡包、箱子存取币、售卖锚点、战斗死亡、日终进食耗尽、日终遭遇生成和非敌对卡周期产出。
- 新鲜静态验证：旧结构扫描未发现 `AudioId`、`AudioManager`、`PlaySFX`、`CombatManager`、`ProjectileManager`、`HitUI` 或 DOTween 回流；定向 `git diff --check` 通过。Unity 自动化仍被 guard 阻塞：当前同工程存在 2 个 Unity Editor、2 个 ShaderCompiler 和 `Temp/UnityLockfile`，待编辑器独占后补跑场景重建与 PlayMode。

## 2026-08-15：StackCraft 非战斗反馈音效吸收

- 对证 StackCraft `CardController`、`RecipeDefinition`、`ChestLogic`、`PackVendor`、`CardBuyer` 和 `AudioManager`：模板的通用反馈包括拿起卡牌 `CardPick`、释放卡牌 `CardDrop`、日终进食滑动 `CardSwipe`、进食 `Eat`、制作 / 生成 `Pop`、取币 `Coin`、存币 / 出售 `Coins`、购买 `CashRegister`。
- CardLoop 不恢复 `AudioManager`、`AudioId` 或输入回调直接执行业务的旧结构。拖拽输入只在真实按下 / 释放后请求 `TabletopView` 播放反馈；日终进食从 `ScenarioRun` 发布只读表现事实；行动结算只返回牌桌表现提示，仍由 `TabletopView` 通过现有 `AudioPlaybackRequestedEvent` 交给 GameCore `AudioSystem`。
- `TabletopViewSettings` 新增 8 个牌桌反馈音效作者引用，测试场景生成器使用已迁入的中文项目音效生成对应 `AudioClipResolver`；资源加载和播放仍归 `ResourceSystem` / `AudioSystem`，没有第二套音频 ID 或 YooAsset 封装。
- 该切片当时未硬接 `Puff`：它是粒子 VFX + 音效，需要单独裁决；随后已由上方“卡牌烟雾粒子反馈吸收”切片接入。
- 新鲜验证：源码定向 `git diff --check` 通过；静态搜索确认没有恢复 `AudioId`、`AudioManager`、`PlaySFX`、`CombatManager`、`ProjectileManager`、`HitUI` 或 DOTween。Unity 自动化被 guard 阻塞，当前同工程仍有 2 个 Unity Editor 和 2 个 ShaderCompiler 进程，待编辑器独占后补跑场景重建与 PlayMode。

## 2026-08-15：StackCraft 战斗音效吸收

- 对证 StackCraft `CombatTask.AttackSequenceCoroutine` / `AudioManager`：自动攻击起手播放 Attack 音效；命中后播放按攻击类型区分的 Hit 音效；暴击额外播放 Critical；未命中播放 Miss。
- CardLoop 不恢复 `AudioManager`、`AudioId`、`CombatType` 或新的音频总管。`Battle` 暴露一次只读“攻击开始”表现事实；`TabletopView` 把攻击开始、命中、暴击和未命中映射到 `TabletopViewSettings` 上的 `AudioClipResolver`，再通过现有 `AudioPlaybackRequestedEvent` 交给 GameCore `AudioSystem`。
- 测试场景生成器为 `FoundationTestRuntimeRoot` 配置现有 `AudioSystem` 的 `GameplaySoundFX` 通道，并用 StackCraft SFX 资源生成 8 个临时 `AudioClipResolver` 资产。它们只服务原型验收，不成为新的音频 ID 或资源加载真相。
- 新增 EditMode 合同覆盖近战没有投射物时仍会产生“攻击开始”表现事实；远程 / 魔法仍复用投射物前摇链路。静音伤害结算不播放 Miss / Hit / Critical，和现有 GameCore 静音表现语义一致。
- 新鲜验证：`.spec` lint 通过，规范测试 `2/2` 通过；当前切片源码 `git diff --check` 通过；正式代码搜索未发现 `AudioId`、`AudioManager`、`CombatType`、`CombatManager` 或 `ProjectileManager` 回流。Unity 自动化被 guard 阻塞，原因是当前同工程存在 2 个 Unity.exe 和活动 ShaderCompiler；待编辑器空闲后补跑测试。仍未吸收：`HitUI` Sprite / DOTween punch。

## 2026-08-15：StackCraft 战斗投射物前摇吸收

- 对证 StackCraft `CombatTask.AttackSequenceCoroutine` / `CombatProjectile`：远程和魔法攻击先播放 `0.6s` 线性投射物，飞完后才进入伤害结算；近战不生成投射物。
- CardLoop 不恢复 `CombatManager`、`CombatType` 或新的投射物总管。`Battle` 只保存本次自动攻击的前摇表现事实，`Tabletop` 在前摇结束后才激活 EX-GAS Ability，伤害仍由 Timeline -> GameplayEffect -> GNS/EX-GAS 结算。
- `TabletopView` 通过现有 `SoftAssetReference` / `ResourceSystem` 实例化 `TabletopProjectileView`，该组件只移动和隐藏表现对象，不发布规则事件、不提交伤害、不保存第二状态。
- 测试场景生成器已新增“牌桌测试投射物”预制体和 YooAsset 收集项；新增 EditMode 合同覆盖远程自动攻击前摇期间生命不变、无伤害表现事件，前摇结束后才由 EX-GAS 结算。
- 新鲜验证：源码静态检查无旧 `BeginTurn` 调用，无 `CombatManager` / `CombatStats` / `CombatType` / `ProjectileManager` 回流；`git diff --check` 通过。Unity batch 与 UnitySkills 验证被 `.spec/tools/unity-verify.mjs` 阻塞，原因是当前同工程存在 2 个 Unity.exe 和 2 个 UnityShaderCompiler.exe；本轮不并发启动 Unity。仍未吸收：音效、`HitUI` Sprite / DOTween punch。

## 2026-08-15：StackCraft 命中镜头 shake 吸收

- 对证 StackCraft `CombatTask.AttackSequenceCoroutine`：命中后 `CameraController.Shake()`，Miss 只播放 Miss 音效，不 shake。
- CardLoop 不恢复 `CameraController` / `CombatManager` 或 DOTween 依赖；`CameraShake` 直接监听纯 ASC 伤害结算表现事件，非 Miss、非静默且未带 `NoCameraShake` 时播放现有 Transform 震动。
- `FoundationTest` 主相机已挂 `CameraShake`，测试配置只打开 `AbilitySystemDamageResolved` 来源；场景生成器同步维护相同配置，强度 `0.1`、持续 `0.3s` 对齐模板参数。
- 新增 PlayMode 回归覆盖事件触发镜头位移并在结束后复位。当前投射物已由后续切片吸收；仍未吸收：音效、`HitUI` Sprite / DOTween punch。

## 2026-08-15：StackCraft RPS 克制与文本反馈吸收

- 对证 StackCraft `CombatType` / RPS 分支后，吸收“近战克远程、远程克魔法、魔法克近战”的玩家效果；不恢复 `CombatType` 枚举、`CombatStats` 或模板战斗总管。
- EX-GAS 作者源新增 `Combat.Melee / Combat.Ranged / Combat.Magic` 标签，GE `2003 / 2004` 通过 `FormalDamage.Matchups` 声明来源标签、目标标签、倍率和表现语义；优势倍率为 `1.5x`，劣势倍率为 `0.75x`。
- `DamageSolver` 只执行 GE 配置的克制规则，来源 / 目标标签直接从唯一 ASC 查询；配置了克制规则但缺来源或目标 ASC 时直接报错，不静默跳过。克制倍率发生在命中与防御之后、暴击之前，保持模板源码顺序。
- 牌桌表现事件携带克制结果，`TabletopCardView` 显示优势 / 劣势文本和颜色；这只是必要文本反馈，不等价于模板 `HitUI` Sprite / DOTween punch。
- 新鲜验证：Unity 编译日志 `Logs/RpsMatchup-Compile.log` 无脚本编译错误；GameCore RPS 定向 `Logs/RpsMatchup-EditModeResults-R3.xml` 为 `1/1`；Gameplay RPS 定向 `Logs/RpsMatchup-GameplayEditModeResults-R1.xml` 为 `2/2`；`.spec` lint 与规范测试已通过。

## 2026-08-15：StackCraft 命中 / 闪避 / 暴击与牌桌飘字源码对照吸收

- 订正验收口径：本切片不再用端到端截图或视觉测试证明模板效果；主证据是 StackCraft `CombatTask.ResolveAttack` / `HitUI.Initialize` 与 CardLoop `DamageSolver` / `GameplayEffectDamageSystem` / `TabletopView` / `TabletopCardView` 的源码逐项映射。
- 规则结算：`DamageSolver` 改为 StackCraft 源码顺序：命中率为 `Accuracy - Dodge` 并钳制 `5% - 95%`，Miss 后不再判暴击；伤害用减法防御且最小为 1；暴击在防御后按暴击倍率计算。攻击力输入仍来自 GNS/EX-GAS 的正式 GameplayEffect 参数，不恢复 `CombatStats`。
- 表现接线：纯 ASC 目标在 GE 写回 Health 后发送只读表现事件，牌桌视图只投影当前牌桌内对应角色卡，卡牌视图显示 `Miss` 或实际伤害数字并区分暴击颜色。该事件不是第二事件总线，也不承担规则真相。
- 未吸收：投射物、音效、`HitUI` Sprite / DOTween 动画仍待后续效果切片；RPS 固定枚举结构不恢复，玩家效果已在后续 RPS 切片中通过 EX-GAS Tag / GE 配置吸收。后续必须继续按源码对照裁决，不能用原创策划或当前 UI 外观直接排除。

## 2026-08-15：战斗数值吸收口径订正

- 对证 StackCraft 源码后确认：模板确实包含命中、闪避、暴击、攻防、攻速、投射物、伤害飘字、音效和镜头反馈；这些不是原创扩展臆测。
- 用户裁决：正式数值系统走 GNS；当前为了复现模板效果，可以暂时使用 StackCraft 的数值参数和公式口径。
- 文档已订正为“旧结构排除，效果吸收”：不得恢复 `CombatManager`、`CombatStats`、`CombatType` 或第二套角色属性；后续实现命中 / 闪避 / 暴击时，数值必须映射到 GNS/EX-GAS，随机走战斗权威随机，表现走 Timeline / Cue / 牌桌视图。
- 本轮只同步裁决和验收口径，没有修改生产战斗代码，也没有宣称该效果切片已经实现。

## 2026-08-14：模板吸收验收口径订正

- 用户明确：StackCraft 后续吸收重点是用 CardLoop 新框架实现相同游戏效果，不要求 UI 外观、尺寸、皮肤或布局完全一致。
- 项目硬规则已订正：默认验收玩家操作、规则结算、状态变化和反馈结果；参考 UI 只用于识别功能入口与必要信息，新 UI 只需完整、可读、可操作并符合 CardLoop 自身设计。
- 后续重新按游戏效果审计模板，不因 UI 不同判定缺失，也不因做出相似界面就宣称对应玩法效果已经吸收。

## 2026-08-14：StackCraft 标题入口与关闭生命周期验收

- 新增 `FoundationTitleTest` 标题入口，四个命令直接复用正式职责：新游戏交给 `ScenarioDirector`，读取存档交给 `ScenarioSavePanel`，设置交给 `UISettings`，退出交给 `ConfirmationDialogPanel`；没有新增主菜单系统、第二套存档或资源入口。
- 标题场景与 `FoundationTest` 共用唯一 `FoundationTestRuntimeRoot` 进程根。场景生成器改为保存后从 `AssetDatabase` 重新加载持久预制体，不再把已销毁的临时 `GameManager` 组件当成正式资产引用。
- 存档窗口和退出确认框按现有 4K Canvas 基准放大并从正式生成源重建。最终真实 GameView：`Assets/Screenshots/FoundationTitle-Title-R2.png`、`FoundationTitle-Settings-R2.png`、`FoundationTitle-Load-Final.png`、`FoundationTitle-Quit-Final.png`；四个状态均通过可读性、裁切、重叠和层级审计。
- `ScenarioTitleScreenPlayModeTests` 定向回归 `3/3` 通过，覆盖标题命令、正式弹窗和新游戏场景切换。`GameManager` 在 `OnApplicationQuit` 先于插件退出回调执行唯一逆序关闭，随后 `OnDestroy` 不重复关闭；清空控制台后的真实 Play -> Ready -> Stop 验证为 `0` 错误。
- 自动化根因订正：项目 `runInBackground=0` 时，终端抢走焦点会让 Play Mode 停在第 2 帧，YooAsset 和 UIKit 因 PlayerLoop 不推进而看似卡死；这不是资源异步死锁。系统 `aibridge` skill 已补充主线程等待与失焦冻结的区分、窗口聚焦及会话级后台运行规则。
- 本结果只关闭 StackCraft 当前选择吸收范围的标题闭环，不代表原创荒岛、职业、游戏内编辑器、联机权限或完整 Mod 平台已经实现。

## 2026-08-13：模块 10.3 权威随机复审完成

- 复审确认不需要新增联机随机系统：`ScenarioDirector` 提供单局根种子，`ScenarioRun` 派生地区牌桌随机流，`Tabletop` 拥有行动分支和战斗种子，`Battle` 为每次 Ability 激活提供种子，EX-GAS Timeline 继续为 GameplayEffect 派生种子。
- 行动随机结果会写入行动实例，地区牌桌随机状态已进入非战斗单局快照；战斗按产品裁决不存档。现有测试已覆盖相同种子的行动分支复现、战斗激活种子序列、缺失牌桌随机流时失败，以及单局快照恢复后的随机状态一致。
- 未来 FishNet 服务器 / 主机只接管现有随机流的推进和结果分发，不把根种子发给客户端重演隐藏规则。公开结果与秘密结果的发送范围依赖 10.2 玩家席位 / 可见性模型，因此联机消息与重连协议仍留在 10.4。

## 2026-08-13：模块 10.4 Mod 作者入口复审

- `ModInfo.metaData` 没有任何项目消费者、键定义、版本合同或校验入口，已删除。它不能作为 Mod 业务数据库；玩法扩展继续统一进入 `ContentAsset`、内容校验和当前单局 `ContentIndex`。
- `ModLoader` 发现同一目录包含多个 `*.cfg` 时直接抛出错误并列出所有文件，不再依赖文件系统返回顺序选择清单。一个 Mod 目录只有一个清单作者源。
- 定向 EditMode 测试 `LoadAllMods_WhenOneDirectoryContainsMultipleManifests_ThrowsInsteadOfPickingOne` 通过；GameCore EditMode 全量 336 项通过、0 失败。此前新增测试误引入测试程序集不存在的 Newtonsoft 依赖，已改为内联最小清单 JSON，Unity 编译恢复。
- 当前仍未实现代码 Mod 执行环境、游戏内编辑器、创意工坊发布、Mod 内容类型扩展协议和联机校验；本轮只清理作者入口的第二真相和不确定发现行为。
- 取消令牌已沿 `ModAPI -> IModLoader -> ResourceSystem.LoadModPackageAsync` 贯通。扫描前、每个 Mod、每个包和 YooAsset 包初始化各阶段后都会检查取消；已开始的底层 YooAsset 操作自然结束后销毁未发布包，并停止加载后续包，不虚构“立即中断 I/O”。修改后 GameCore EditMode 全量 112 项通过、0 失败。
- Mod 清单读取改为同步结构化入口：文件内容为空或反序列化为 `null` 时按路径明确报错，不再静默跳过目录。项目 Mod API 版本配置无效时直接拒绝创建 `APIValidator`，不再回退到 `0.1.0`。
- 已存在的 Mod 配置文件损坏时不再记录错误后生成默认配置并覆盖原文件；现在会保留原文件并拒绝初始化。加载时同时验证 API 版本、状态列表、状态 ID 唯一性和状态枚举。GameCore 与 GameCore EditModeTests 使用 Unity 本轮响应文件独立编译均为退出码 0；Unity 主进程被此前异步测试死锁，运行回归等待编辑器恢复。
- Mod 配置保存改为同目录临时文件原子替换；临时缺失 Mod 的禁用 / 删除状态不再被启动过程自动清理，避免重装后无意恢复默认启用。
- Mod zip 按规范化路径顺序串行解压到同名独立目录；目标目录已存在、路径穿越、重复目标路径或压缩包损坏时直接失败，保留原 zip 并清理本轮残缺目录。批量解压职责已从 `ModAPI` 公开入口收回 `ModLoader`，避免绕过取消和失败语义。
- 上述运行时与测试源码再次使用 Unity 响应文件编译，GameCore 和 GameCore EditModeTests 均为退出码 0。运行测试仍等待挂死的 Unity 主进程恢复。

## 2026-08-13：联机后端候选裁决

- 已确认未来正式 Unity 联机后端采用 FishNet；官方能力满足服务器权威、监听服务器、独立服务器以及 `Replicate` / `Reconcile` 客户端预测与服务器校正。
- 客户端预测只作为即时战斗高频移动、瞄准等表现的按需能力；牌桌放置、行动、资源、EX-GAS 结算、随机和叛徒可见性仍由主机 / 服务器权威确认。
- 当前不安装 FishNet，不创建网络组件、RPC、网络变量、玩家身份或权限协议。模块 10.2 继续等待正式玩家席位和控制权模型，不以网络包安装冒充联机接入完成。

## 2026-08-13：模块 10.1 内容包依赖与版本完成

- Mod 身份改为稳定 `modId`；启停状态不再使用会随版本变化的组合名称，删除 `FullName/fullName` 和 `loadOrder` 语义。删除 Mod 清单手填哈希，改为加载 YooAsset 官方 `.hash` 构建产物。
- `ModInfo.dependencies` 声明依赖 Mod ID 和包含式版本范围；`ModDependencyResolver` 统一验证缺失依赖、禁用依赖、版本不兼容、循环、重复 Mod ID、重复 YooAsset `packageName` 和无效版本，并输出依赖优先、同层按 `modId` 排序的确定顺序。
- `ModLoader` 改为先发现、统一解析、再加载；加载中途失败反向卸载本轮包。`ResourceSystem` 删除包加载优先级参数，包地址 / 资源路径 / 场景定位出现多包命中时直接报错，不静默覆盖。
- `ModAPI.CreateActivePackageSetSnapshot()` 只记录当前启用且已加载的 Mod：稳定 ID、语义版本、YooAsset 官方包哈希和生效清单版本。`ScenarioRunSnapshot` 冻结该集合，恢复前严格比较，版本、包哈希、清单版本、缺失或额外 Mod 任一不同都会拒绝。
- 验证：GameCore EditMode `222/222`；Gameplay EditMode `384/384`；Gameplay PlayMode `32/32`；GameCore PlayMode `9/9`；`spec-lint passed`。AIBridge 的 PlayMode 延迟请求只返回 Processing，最终以 Unity `TestResults.xml` 为准。
- 未实现：网络后端、RPC、联机权限 / 可见性协议、创意工坊、游戏内 Mod 编辑器和资源覆盖优先级。下一步先审查 10.2 的权限 / 命令 / 可见性职责，仍不提前实现具体联机后端。

## 2026-08-13：模块 10.2 前置审查

- 当前项目没有 NGO、Mirror、FishNet 或 Photon 运行时网络依赖；`com.unity.multiplayer.center` 只是编辑器入口，不是运行时联机后端，不能凭空实现 RPC。
- 行动请求已由 `ScenarioRun` 复核发现权限后提交给牌桌；卡牌放置、行动计划填槽 / 取消属于 `Tabletop` 聚合的核心状态写入。尝试把后者再转发到 `ScenarioRun` 后复审为薄包装，已删除，不制造假权威层。
- 当前还没有“玩家身份、可控制角色集合、队长授权、叛徒可见性”的正式作者源和运行状态，因此不能正确实现权限或可见性。下一步必须先选定运行时网络后端，并根据联机规则明确这些领域事实；在此之前只保留现有单机聚合与请求复核，不新增伪 RPC、`PlayerId`、权限表或同步副本。
- 复用审查：`GameCore.GameCommandContext` 只服务 `CharacterBase`、旧 2D RPG 玩家控制器、AI、投射物和能力触发链；其中 `RemotePlayer` 只是来源枚举和字符串标识，没有连接、授权、验证、同步或重放语义。2DRPGEngine 的正式 `ICommand` 也只有无上下文的 `Execute()`，不能证明存在可直接迁入的联机命令框架。
- 处理裁决：不删除仍被旧 GameCore 实体链使用的 `GameCommandContext`，不把它接到 `TabletopCard`，也不在 Gameplay 新建同职责 `CommandContext` / `RemotePlayerContext`。等真实网络后端和 CardLoop 玩家控制权模型确定后，再以 `ScenarioRun`、`Tabletop` 和具体玩家席位为权威对象设计可序列化请求。

## 2026-08-11：模块 3.4 牌桌视图完成

- 对象订正：把实际承载整张牌桌表现生命周期的 `TabletopCardViewProjector` 重命名为 `TabletopView`，保持深模块，不拆成卡牌、战斗、进度和资源薄系统。
- 设置订正：`TabletopCardSettings` 重命名为 `TabletopViewSettings`；它只配置视图资源、Z 深度、排序和拖拽手感，权威尺寸与 XY 步进仍来自牌桌放置规则。
- 唯一根节点：删除 `m_viewRoot`。`TabletopView` 自身 Transform 是卡牌与世界空间进度视图的父节点，场景不再重复回填子节点引用。
- 身份去重：单卡视图保存 `TabletopCard` 对象引用，卡牌 ID 与内容 ID 从对象派生；姿态查询直接使用卡牌的所属牌堆，不再遍历全部牌堆重建关系。
- 配置失败：资源引用为空、拖拽锐度非法或布局参数非有限时在绑定阶段明确失败，不运行时补值或夹取继续。
- 验证：定向 EditMode `2/2`、Foundation 真实 YooAsset / 解绑释放链 `13/13`、最终全量 EditMode `425/426`（`1` 条既有忽略）、PlayMode `30/30`；场景和测试视图设置已由正式生成器更新，旧类型名与 `m_viewRoot` 正式入口残留扫描为空。
- 下一步：模块 4.1，回审行动定义领域字段，不提前实现原创配方或正式 UI。

## 2026-08-11：模块 3.3 拖拽意图与输入边界完成

- 参考吸收：保留 StackCraft 从按下点拖动、牌堆尾段预览、点击/拖拽区分和目标高亮；排除输入回调直接拆堆、交易、装备、战斗、制作和 Transform 权威写入。
- 坐标语义：拖拽会话同时读取屏幕坐标和牌桌坐标。屏幕坐标只判定是否越过像素阈值，牌桌坐标只计算按下偏移、预览位置和释放请求。
- 唯一配置：阈值来自正式 `EventSystem.pixelDragThreshold`，相机来自 `GameManager.MainCamera`，射线距离来自相机远裁面；删除命中层、最大距离、拖拽距离和牌桌平面四个 Inspector 字段。
- 输入边界：输入只产生 `TabletopCardPointerReleaseIntent`。空白释放由牌桌原子放置，目标卡牌释放由当前单局查询行动候选；输入和视图均无牌桌写权限。
- 验证：会话定向 `6/6`、Foundation 真实输入 `13/13`、全量 EditMode `425/426`（`1` 条既有忽略）、全量 PlayMode `30/30`；测试场景已由正式生成器重建。
- 下一步：模块 3.4，回审视图投影、资源句柄和表现状态，不提前进入正式 UI。

## 2026-08-11：模块 3.2 桌面区域、位置和放置完成

- 参考吸收：保留 StackCraft `Board` 拥有桌面边界、`CardPhysicsSolver` 作为内部解算协作者的职责关系；不吸收其全局单例、Transform 权威状态或视图反写规则。
- 唯一作者源：`ScenarioDefinition` 内嵌一份牌桌放置定义，声明边界、禁放区、卡牌规则尺寸和 XY 堆叠步进；`ScenarioRun` 创建 `Tabletop` 时冻结为唯一运行时规则。
- 规则去重：当前 `TabletopViewSettings` 删除卡牌尺寸和 XY 步进，只保留视图资源、Z 深度、排序与拖拽手感；牌桌视图直接读取 `Tabletop.PlacementRules`。
- 写入口收口：`TryPlaceStack` 不再接收调用方规则；删除绕过边界与重叠解算的 `MoveStack`。正式创建卡牌也使用同一规则并在提交前完成候选解算。
- 作者负担订正：删除剧本可配置的最大解算轮数。该值是内部算法预算，由放置解算器维护，不再让策划或 Mod 作者填写技术参数。
- 原子结算：行动提交在删除参与卡前，先用同一规则预演剩余牌堆和全部产物；空间不足时零修改失败，多个独立牌堆产物不再合法重叠在锚点。
- 验证：RED 编译证据命中缺少牌桌规则 owner；GREEN 定向牌桌 `11/11`、行动结算 `11/11`；全量 EditMode `423/424`（1 条既有忽略），全量 PlayMode `30/30`。
- 下一步：模块 3.3，回审拖拽意图和输入层边界。

## 2026-08-11：模块 3.1 卡牌实例与牌堆完成

- 参考吸收：保留 StackCraft `CardInstance` / `CardStack` 的直观对象关系，排除其把 Unity 表现、战斗、装备、制作和 UI 混入卡牌本体的结构。
- 对象重构：`TabletopCard` 直接持有所属 `TabletopCardStack`，并公开只读逻辑位置与锁定状态；角色卡继续通过继承拥有唯一 EX-GAS `AbilitySystemCell`。
- 唯一关系：牌堆负责加入、转移、拆分和移除成员；删除 `m_stackByCardId` 派生关系表，不再由集合在每次操作后同步第二份卡牌归属。
- 命名订正：`TabletopCardState` 重命名为 `TabletopCards`，测试夹具的 `CardState` 同步改为 `Cards`；序列化 `TabletopCardStateSnapshot` 保留。
- RED：对象归属测试首先因 `TabletopCard` 没有 `Stack` / `Position` 编译失败。
- GREEN：`TabletopCardsEditModeTests` `10/10`；全量 EditMode `421/422`（`1` 条既有忽略）；全量 PlayMode `30/30`。
- 下一步：模块 3.2，审查桌面区域、牌堆位置和放置解算；不提前改拖拽或视图职责。

## 2026-08-11：模块 2.3 场景组合完成

- 作者源：`ScenarioDefinition` 新增由场景资产选择器维护的初始场景地址；空地址表示当前场景运行，地址不作为内容 ID。
- 运行时：`ScenarioDirector` 改为异步开局 / 结束。它复用 `SceneSystem` 完成技术切换，切换成功后才发布 `ScenarioRun`；结束时先关闭旧局和内容句柄，再返回来源场景。
- 测试作者源：统一生成器新增场景型测试剧本，场景地址从真实 `SceneAsset` 名称推导；没有手填第二份内部 key。
- 场景边界：`GameManager` 是唯一跨场景进程宿主，普通剧本场景不配置第二个宿主。测试使用纯来源地图验证“来源地图 -> 剧本地图 -> 来源地图”。
- RED：场景型剧本资产尚未生成时，新增 PlayMode 合同 `0/1`；初次错误地把宿主场景当返回场景时，重复 `GameManager` 日志和装配竞态直接暴露了场景职责错误，没有用忽略日志掩盖。
- GREEN：定向场景组合 `1/1`；完整 `ScenarioContentPlayModeTests` `7/7`；全量 EditMode `420/421`（`1` 条既有忽略）；全量 PlayMode `30/30`。
- 下一步：进入模块 3 的牌桌聚合回审；剧本内旅行仍归模块 5，不在 2.3 提前实现。

## 2026-08-10：重新划分模块

- 状态：进行中。
- 已读取：项目 AGENTS、`.spec` 规范中心、模块矩阵、地基提案、全量重审、任务拆解与文件式计划流程。
- 已确认：之前的模块表仍保留有效历史证据，但不足以作为当前执行顺序；已在 `task_plan.md` 建立按生命周期重新排列的模块计划。
- 已同步：模块矩阵与地基提案均已指向 `task_plan.md` 作为当前执行顺序，旧模块表和阶段表降级为历史来源对照。
- 本轮尚未修改 Gameplay 运行时代码、Unity 场景或资源。
- 验证：`.spec/tools/spec-lint.mjs` 已通过。

## 2026-08-10：模块 0.1 启动

- 状态：进行中。
- 目标：只审查进程级初始化与系统装配；不新增 Gameplay 业务对象或测试场景功能。
- 验收：确认现有进程入口是否已经承担正确职责，或给出有源码证据的最小重构范围。
- 发现：`ModAPI.Initialize` 的重复调用只写日志后返回，已经新增 `ModApiLifecycleEditModeTests` 准备保护“重复启动必须失败”的公开契约。
- 工具记录：PuerTS Unity MCP 编译请求因 `127.0.0.1:18990` 空响应失败一次；实际 UnitySkills REST 服务监听 `127.0.0.1:8090`，后续改走该入口。

## 2026-08-10：模块 0.1a 完成

- 范围：只处理进程级 Mod 初始化的重复调用失败语义；没有接入任何新 Mod 格式、玩法或模板代码。
- 修改：`ModAPI.Initialize` 检测到已经初始化时改为直接抛出明确异常，不再只写日志后返回。
- RED：`GameCore.Tests.ModApiLifecycleEditModeTests` 首次运行 `0/1`，失败原因是原实现没有抛异常。
- GREEN：编辑器刷新脚本后同一测试 `1/1` 通过。
- 回归：`GameCore.Tests.GameManagerAndGameStateLifecycleEditModeTests` 为 `8/8` 通过。
- 工具记录：第一次用 GET 查询异步测试结果返回 404；依据 UnitySkills REST 入口改为 POST 后成功取得结果。该工具调用错误未影响 Unity 测试本身。
- 下一步：继续 0.1b，审查 `GameManager` 子系统树与其真实 owner 关系，不提前进入模块 1 的内容作者源。

## 2026-08-10：模块 0.1b 完成

- 裁决：保留 `GameManager` 作为进程级组合根和 `AGameSystem` 的有限系统树；它已经比 2DRPGEngine 的全场景系统扫描更符合唯一 owner 和领域对象边界。
- 修改：`GameManager` 的重复系统初始化/启动、`GameStateSystem` 的重复启动均改为明确异常；停用后的正常重启不受影响。
- RED：收紧后的两个生命周期测试首次为 `7/9`，两个失败都来自旧实现静默返回。
- GREEN：`GameCore.Tests.GameManagerAndGameStateLifecycleEditModeTests` 为 `9/9`；`GameCore.Tests.ModApiLifecycleEditModeTests` 为 `1/1`。
- 模块顺序订正：原 0.1c 依赖资源、Mod、GAS 的原子初始化，不能脱离它们单独裁决；已并入 0.2。
- 下一步：进入 0.2a，先审查三个基础设施各自的重复初始化、部分失败和清理责任，再处理 `GameManager` 组合关闭链。

## 2026-08-11：模块计划重新对齐

- 用户要求恢复按模块推进的明确计划。
- 已重新审查 StackCraft 的实际一级目录（Card、Core、Crafting、Combat、Encounter、Quest、SaveSystem、UI 等）与当前 Gameplay 的领域目录（Content、Scenarios、Tabletop、Actions、Quests）。
- 裁决：保留按真实对象生命周期排列的模块 1-10；模块 0 明确降为技术宿主前置条件。场景加载后端仍在模块 0，剧本场景组合和释放归模块 2 的 `ScenarioRun`，不再把它们并列为两个独立的业务 owner。
- 当前继续点：验证本轮 `ResourceSystem.Shutdown` 的所有权修正，并完成 0.2a.1 的定向及场景回归。

## 2026-08-11：模块 0.2a.1 完成

- 范围：只处理 `ResourceSystem` 的唯一启动权、外部状态拒绝、初始化失败回滚和关闭所有权；没有修改 YooAsset / YokiFrame 第三方源码，也没有接入 Mod 内容协议。
- 修正：资源系统改为仅在自己发起初始化时持有关闭责任；全局 YooAsset 已启动不再被误判为本项目拥有。
- RED：`ResourceSystemLifecyclePlayModeTests` 首次 `5/6`，外部初始化后的关闭没有抛出，且日志证明外部 YokiFrame 资源被销毁。
- GREEN：同一测试 `6/6`。
- 回归：`FoundationTestScenePlayModeTests` `11/11`；`ContentRegistryPlayModeTests` `3/3`；`GameManagerAndGameStateLifecycleEditModeTests` `9/9`。
- 下一步：进入 0.2a.2，按同一原则审查 `ModAPI` 的部分失败、外部状态和关闭责任。

## 2026-08-11：模块 0.2a.2 完成

- 范围：只处理 `ModAPI` 的加载失败、并发初始化、关闭中的迟到提交和自身清单 / 配置清理；没有新增 Mod 内容格式、依赖解析、脚本执行、联机协议或资源包关闭入口。
- RED：新增 Mod 生命周期测试后首次 `1/3`，加载器返回失败仍正常返回，挂起加载也接受第二次初始化。
- 修正：扫描结果改为局部暂存，成功后一次提交；未完成初始化由 ModAPI 自己取消，失败与取消清空本模块状态；资源包仍交由 `ResourceSystem` 回收。
- GREEN：`ModApiLifecycleEditModeTests` `3/3`；`ModRuntimeSecurityEditModeTests` `2/2`；`GameManagerAndGameStateLifecycleEditModeTests` `9/9`；`FoundationTestScenePlayModeTests` `11/11`。
- 工具记录：脚本刷新后的首次编译状态查询遇到 Unity 域重载导致的短暂连接拒绝；等待服务恢复后编译空闲，未构成代码或测试失败。
- 下一步：进入 0.2a.3，核对项目侧 EX-GAS 组合入口的重复进入、启动失败与关闭责任。

## 2026-08-11：模块 0.2a.3 完成

- 范围：只处理项目侧 EX-GAS 组合入口的外部重入、启动失败、生成缓存异常与关闭责任；没有修改 EX-GAS 第三方源码、生成代码或 GAS 作者表。
- RED：新增“外部入口先启动 GAS”用例后为 `1/2`，旧组合入口继续接管外部 World，插件仅警告重复初始化，标签图重复异常被反射辅助写日志后吞掉。
- 修正：项目入口先拒绝外部 GAS 状态；自身初始化失败时只回滚本轮启动的 GAS World；跨程序集生成缓存反射改为强制传播失败。测试辅助和战斗测试均迁回该正式入口，不再直接修改私有状态或启动 `GASManager`。
- GREEN：`FormalAbilityRuntimeLifecycleEditModeTests` `2/2`；`FormalDamagePipelineEditModeTests` `7/7`；`BattleEditModeTests` `5/5`；`BattleFormationEditModeTests` `3/3`；`GameManagerAndGameStateLifecycleEditModeTests` `9/9`；`FoundationTestScenePlayModeTests` `11/11`。
- 下一步：进入 0.2a.4，汇总资源、Mod 和 GAS 的启动成功条件、失败回滚范围和关闭责任，再决定 `GameManager` 是否仍有重复或过早取得关闭权。

## 2026-08-11：模块 0.2a.4 完成

- 范围：只审查资源、ModAPI、项目侧 EX-GAS 三个进程级基础设施组合时的初始化原子性、取消边界和唯一所有权；没有新增 Mod 内容协议、联机协议、剧本或玩法业务。
- 发现：资源初始化在关闭期间若直接把取消令牌传入 YokiFrame，插件会中止等待但保留已登记的资源包状态，导致项目侧无法可靠回滚。
- 修正：`ResourceSystem` 由自身持有本轮初始化取消源；关闭期间撤销本轮结果提交权，等待 YokiFrame 完成自己的初始化流程后，由 `ResourceSystem` 统一回滚。初始化尚未回滚完成时，新的初始化直接失败，禁止重入。
- RED/GREEN：资源关闭中取消用例先失败，调整生命周期后资源生命周期测试 `8/8` 通过；`GameManagerAndGameStateLifecycleEditModeTests` `9/9`；`FoundationTestScenePlayModeTests` `11/11`。
- 验证工具：UnitySkills `8090` 的 PlayMode / EditMode Test Runner；脚本程序集刷新后重新执行，未使用旧程序集结果冒充新验证。
- 下一步：进入 0.2b，审查 `GameManager` 对三项基础设施的成功后所有权、启动失败、对象销毁和逆序关闭。

## 2026-08-11：模块 0.2b 完成

- 范围：只处理 `GameManager` 对资源、ModAPI、项目侧 EX-GAS 的成功后关闭责任、启动失败和对象销毁取消；没有新增运行时总线、上下文、玩法系统或 Mod 内容协议。
- RED：外部入口先启动资源运行时时，旧 `GameManager` 在资源入口拒绝接管后仍调用关闭，产生第二条“资源系统关闭失败”异常；新 PlayMode 用例首次 `0/1`。
- 修正：三个私有标记改为只在对应基础设施成功返回后置位；`GameManager` 将 `destroyCancellationToken` 同时交给资源与 Mod 初始化。`ModAPI.Initialize` 用链接取消源，在扫描完成后的提交边界拒绝迟到结果。
- GREEN：外部资源用例 `1/1`；`ModApiLifecycleEditModeTests` `4/4`；资源生命周期 `8/8`；系统生命周期 `9/9`；GAS 生命周期 `2/2`；`FoundationTestScenePlayModeTests` `11/11`。
- 下一步：进入 0.2c，补真实 `FoundationTest` 的关闭后状态验收，确认资源、Mod、GAS 没有残留。

## 2026-08-11：模块 0.2c 完成

- 范围：只在真实 `FoundationTest` 场景验证 GameManager 的组合启动与销毁，不新增 UI、牌桌、剧本或 Mod 业务。
- 修改：在既有场景验收中新增关闭用例，先确认资源、Mod、GAS 与 YooAsset 运行，再销毁唯一 GameManager，验证所有基础设施和 GAS World 都已退出。
- 验证：定向关闭用例 `1/1`；完整 `FoundationTestScenePlayModeTests` `12/12`；没有未预期控制台错误。
- 下一步：进入 0.3，审查新输入系统、UIKit 与 EventKit 的唯一正式入口及模板遗留输入兼容代码。

## 2026-08-11：模块 0.3 完成

- 范围：只审查并收敛新输入系统、UIKit 与 EventKit 的正式入口；没有制作正式卡牌 UI、职业、剧本、联机或 Mod 业务。
- 裁决：`GameCore.InputSystem` 保留为唯一输入 owner，`TabletopCardDragInput` 继续只通过它订阅 Click。UIKit 的 `UIRoot`、`EventSystem` 和 `InputSystemUIInputModule` 是唯一 UI 根；`FoundationTest` 已验证它与唯一 `PlayerInput` 使用同一份动作资产，且没有旧输入模块。
- 删除：全局输入入口中原有的旧 2D RPG 自动角色命令派发；硬编码 `SampleScene` 的只读日志诊断；测试夹具里重复提交动作图的补丁。
- 保留但不接管：`UISystem/UIManager` 仍是未进入 CardLoop Foundation 的 GameCore 菜单候选，不能被称作正式 CardLoop UI；模块 5 出现真实菜单消费者前不新增迁移包装。
- 验证：新增 Foundation 输入唯一性架构守卫 `1/1`；完整 `FoundationTestScenePlayModeTests` `13/13`；`GameCore.Tests.GameManagerAndGameStateLifecycleEditModeTests` `9/9`。
- 下一步：进入 0.4，审查 `SceneKit`、`MapSystem` 与 `TransitionSystem` 的技术加载/释放边界，不让它们拥有剧本单局状态。

## 2026-08-11：模块 0.4 与模块 0 完成

- 场景资源后端：`ResourceSystem` 改为直接配置 `SceneKit.SetLoaderPool(ResourceSystemSceneLoaderPool)`，删除经 ResKit 的双层场景加载链。显式卸载和无效异步加载都会释放 YooAsset 句柄并清除资源包占用。
- 场景职责：新增 `SceneSystem` 作为唯一技术场景切换 owner；它依赖 `TransitionSystem`，前者负责整次切换串行和生命周期事件，后者只负责淡入淡出。`MapSystem` 不再拥有场景切换、当前场景地址或过场状态，保留旧 RPG 的检查点、重生、传送和地图存档。
- 事件语义：旧 `Map*` 生命周期事件全部迁为 `Scene*`。成功完成使用 `SceneTransitionCompletedEvent`，无论成功、失败或取消都会发送 `SceneTransitionEndedEvent`，输入系统据此解除输入锁定。
- 场景生成：通过 `Gameplay/地基/重建测试场景` 正式入口重建 Foundation 场景；场景只配置一个 `SceneSystem`，没有 `MapSystem`。
- 回归：`GameManagerAndGameStateLifecycleEditModeTests` `9/9`；`PersistenceSystemRegistrationEditModeTests` `4/4`；`ResourceSystemLifecyclePlayModeTests` `8/8`；`ContentRegistryPlayModeTests` `4/4`；`FoundationTestScenePlayModeTests` `13/13`。Unity 编译和当前控制台错误均为空。
- 边界：YokiFrame `SceneKit` 不能真正取消底层 YooAsset 场景加载；项目侧等待真实加载完成，不伪造已取消。强制取消需要未来按第三方官方扩展点单独裁决。
- 下一步：进入模块 1，先审查 SO 作者源、唯一内容 ID、EX-GAS 标签引用、活动内容会话和内容包组合边界；不提前实现剧本、牌桌、职业、联机或 Mod 业务。

## 2026-08-11：模块计划重新细化

- 用户要求恢复并重新检查按模块推进的计划。
- 已基于当前 `task_plan.md`、`ContentAsset` 候选代码、StackCraft `CardDefinition` / `CardManager`、项目架构规范和 EX-GAS 文档重新划分：模块 1 只处理静态内容作者源；本次剧本实际使用的内容集合移入模块 2 的 `ScenarioRun`。
- 已确认 `ContentRegistrySystem` 的固定全局标签加载只能作为待迁移代码，不能继续作为正式内容真相；模块 2.1 将负责其迁移与删除。
- 当前继续点：模块 1.1，审查 `ContentAsset`、`ContentId` 和自动 ID 生成是否仍符合唯一内容身份与窄技术基类原则。当前未改运行时代码，未开始玩法业务。

## 2026-08-11：模块 1 计划再次对齐

- 本次只同步计划与已存在源码，没有修改玩法运行时代码、StackCraft 参考代码、第三方插件或 Unity 场景。
- 保留原有九个一级模块及其对象生命周期顺序；把模块 1 的后续工作细化为 `1.2a` 静态标签、`1.2b` 单 ID 引用、`1.3` 继承边界、`1.4a` 作者校验与选择器、`1.4b` YooAsset 收集规则。
- 把模块 2 的内容集合前置切片细化为 `2.1a` 选择/解析、`2.1b` 冻结查询集合、`2.1c` 资源句柄归属。
- 首次整组文档补丁因进度日志的标题锚点已变更而未写入；改为按真实锚点拆分小补丁后已完成，不影响源码或 Unity 资源。
- 当前继续点：先对 1.1 的内容身份与最小共同契约运行新鲜验证；通过后才开始 1.2a。

## 2026-08-11：模块 1.1 完成并回验

- 新增 `Assets/Editor/Gameplay/Tests/ContentIdentityEditModeTests.cs`，作为唯一内容 ID 的稳定生成与既有身份不可自动漂移的公开契约测试；未修改 Gameplay 运行时代码。
- Unity 已通过正式资源重新导入为新测试生成 `.meta`，脚本编译状态为空闲。
- 新鲜 EditMode：`Gameplay.Tests.ContentValidationEditModeTests` `3/3`，`Gameplay.Tests.ContentIdentityEditModeTests` `2/2`。
- 同步修正项目知识中的旧表述：`ContentAsset` 只承载身份、静态标签和校验入口；展示字段属于 `DisplayableContentAsset`。
- 工具记录：两次本地编排语法错误没有向 Unity 发出命令；后续已改用独立 REST 请求，未影响工程或测试结果。
- 当前继续点：模块 1.2a，审查 EX-GAS 静态标签的作者选择、合法性校验和层级查询语义。

## 2026-08-11：模块 1.2a 完成并回验

- 范围：只审查内容静态 EX-GAS 标签的作者选择、编辑器合法性校验与层级查询语义；没有新增 Gameplay 标签体系、Mod 标签协议、行动运行逻辑或第三方插件修改。
- 保留：`ContentAsset` 继续只存 EX-GAS 整数标签码，作者直接使用 `GeneralGasChoiceHelper.Tags()`。编辑器内容校验读取同一官方选择数据，未知标签报 `CONTENT_TAG_UNKNOWN`，GAS 作者数据为空报 `CONTENT_TAG_AUTHORING_SOURCE_EMPTY`。
- 边界：静态标签层级比较只走 `TagHelper.HasTag`；角色动态状态只走 `AbilitySystemCell`。临时校验集合不保存、不参与运行时，也不构成项目标签表。
- 新鲜 EditMode：`Gameplay.Tests.GameplayTagCodeAuthoringEditModeTests` `2/2`；`GameCore.Tests.FormalAbilityRuntimeLifecycleEditModeTests` `2/2`。对应证据分别为 `Logs/TestResults-Gameplay-ContentTagAuthoring-Batch-R3.xml` 与 `Logs/TestResults-Gameplay-ExGasTagRuntime-Batch-R1.xml`。
- 当前继续点：模块 1.2b，先审查已存在的 `ContentIdReference` 候选是否只有唯一内容 ID 的序列化真相、是否错误接管运行时内容索引，再决定保留、重构或删除。

## 2026-08-11：模块 1.2b 完成并回验

- 范围：只审查单 ID 内容引用的作者选择、序列化结果、类型约束和领域引用校验；没有新增资源加载器、活动内容包、Mod 依赖协议或运行时引用缓存。
- 裁决：保留现有 `ContentIdReferenceAttribute` / `ContentIdReferenceDrawer`。作者选择资产后只写入 `ContentId.m_value`；编辑器映射只用于 Inspector 显示，非序列化、非运行时，也不拥有内容集合。
- 校验归属：行动、任务、剧本和结果意图在各自作者定义的校验入口中报告无效、未知和错误类型引用；没有新增万能中央引用校验器。进程级 `ContentRegistrySystem` 仍是模块 2.1 的迁移删除候选，本切片不替它加包装。
- 新鲜 EditMode：`ContentReferenceAuthoringEditModeTests` `3/3`、`ActionDiscoveryAndValidationEditModeTests` `4/4`、`ScenarioDirectorEditModeTests` `1/1`。Unity `6000.5.4f1` 对应测试日志没有目标编译错误。
- 当前继续点：模块 1.3，审查各领域定义的继承边界与专属字段归属。

## 2026-08-11：模块 1.3 完成并回验

- 范围：只审查静态内容作者源、嵌入式作者子项和单局运行时对象之间的继承边界；没有新增职业、地点、物品、角色、工位或结果系统空壳。
- 修正：`QuestDefinition` 取消 `sealed`，与 `CardDefinition`、`ActionDefinition`、`ScenarioDefinition` 一样作为代码 Mod 可派生的顶层内容作者源。它原有的前置任务和任务子项校验逻辑不变，派生类通过既有受保护校验钩子扩展。
- 保留：`ContentAsset` / `DisplayableContentAsset` 继续保持窄技术分层；任务子项和行动结果是嵌入式多态数据；`TabletopCard` 是运行时实例，不混入静态作者定义。2026-08-11 回审后，普通行动每回合秒数已归入 `ScenarioDefinition`，独立 `TurnTimingDefinition` SO 删除。
- TDD：扩展性合同先在旧实现 `0/1` 失败，再在修正后由 `ContentValidationEditModeTests` `4/4` 通过。证据为 `Logs/TestResults-Gameplay-ContentInheritance-RED-R1.xml` 与 `Logs/TestResults-Gameplay-ContentInheritance-GREEN-R2.xml`。
- 当前继续点：模块 1.4a，审查单资产作者校验与引用选择器的职责边界。

## 2026-08-11：模块 1.4a 作者校验与槽位选择器完成

- 范围：只补齐行动结果中两个内部槽位键的作者入口，并回审现有单资产校验菜单、领域引用校验和运行时快照键；没有新增内容索引、资源加载器、运行时事件或玩法业务。
- 修正：新增 `ActionSlotReferenceAttribute` 与 `ActionSlotReferenceDrawer`。Inspector 从所属 `ActionDefinition.ParticipationSlots` 显示槽位名称，序列化仍只写入已有的 `m_slotKey` / `m_anchorSlotKey`，没有增加对象引用、第二个 ID 或运行时状态。
- 失败语义：单槽位保留空值表示既有自动推导；多槽位空值、未知键、重复键、无槽位和无效所属对象均在作者入口明确报错，不自动选择第一个，也不静默修复行动资产。运行时结果校验和结算规则保持不变。
- 横向审查：`ActionInstanceSnapshot` 中的分支键和槽位键是存档/恢复事实，不是作者字段；没有把它们误接入 Inspector 选择器。当前搜索未发现其它 Gameplay 作者源要求手填同类内部键。
- TDD 证据：RED 为 `Logs/TestResults-Gameplay-ActionSlotReference-Selector-RED-R1.xml`，目标选择器缺失时 `0/1`；GREEN 为 `Logs/TestResults-Gameplay-ActionSlotReference-GREEN-R2.xml`，选择入口与单/多槽位规则 `2/2`；全量 Gameplay EditMode 为 `Logs/TestResults-Gameplay-Module14a-EditMode-R1.xml`，`91/91` 通过。
- 编译说明：第一次实现命中 Unity 6 `EditorGUI.Popup` 重载错误，已修正为 `GUIContent[]` 后重新编译并通过；最终 GREEN 日志未发现本轮脚本编译错误或初始化异常。现有第三方过时 API 警告不属于本轮新增。
- 当前继续点：模块 1.4b，审查 `ContentAssetFilterRule` 和 YooAsset `BundleCollectorSetting` 的构建期收集边界；不进入 `ResourceSystem` 或模块 2 的当前内容集合。

## 2026-08-11：模块 1.4b YooAsset 构建期收集边界完成

- 版本证据：当前项目实际使用 YooAsset `3.0.5`。该版本的 `EditorAssetUtility.FindAssets` 会把 `IAssetFilterRule.FindAssetType` 自动拼成 `t:<类型名>`，因此 `ContentAssetFilterRule.FindAssetType = nameof(ContentAsset)` 是当前官方接口的正确用法，不改成带 `t:` 的字符串。
- 保留：`BundleCollectorSetting` 的 Gameplay 内容组从 `Assets` 扫描，使用 `ContentAssetFilterRule`、`AddressDisable`、`PackCollector` 和构建标签 `gameplay-content`。它只决定哪些作者资产进入构建清单，不生成内容 ID，不决定某次剧本使用哪些内容，也不持有资源句柄。
- 代码整理：删除过滤规则对行动、剧本和牌桌命名空间的无用引用，并修正文档注释，明确规则只按 `ContentAsset` 类型筛选，不拥有运行时内容集合。
- 稳定契约验证：`ContentCollectionRuleEditModeTests` 直接调用 YooAsset 3.0.5 `BundleCollectorSetting.BeginCollect`，确认所有带 `gameplay-content` 的主资源都能读取为 `ContentAsset`、YooAsset 地址为空，并包含真实地基卡牌。证据 `Logs/TestResults-Gameplay-ContentCollection-Verify-R1.xml` 为 `1/1`。
- 回归：`Logs/TestResults-Gameplay-Module14b-EditMode-R1.xml` 为 Gameplay EditMode `92/92`；`Logs/TestResults-Gameplay-Module14b-ContentPlayMode-R2.xml` 为真实内容加载 PlayMode `4/4`。第一次 PlayMode 命令使用旧过滤前缀只发现 `0` 项，不计入验收。
- 模块 1 当前静态作者层范围完成。跨模块的玩家可见内容加载与实例化验收按计划在模块 2.1 接入单局内容集合后完成，不能为模块 1 单独造全局运行时。
- 当前继续点：模块 2.1a，审查本次剧本内容集合选择与解析，并迁移删除 `ContentRegistrySystem` 的进程级全量加载职责。
## 2026-08-11：模块 2.1a 单局内容集合选择与解析

- 删除进程级 `ContentRegistrySystem` 及其场景装配，不再让 `GameManager` 持有一份全局 Gameplay 内容真相。
- `ScenarioDirector.StartScenario` 通过现有 `ResourceSystem.LoadAssetsByAssetTagAsync<ContentAsset>` 读取当前默认包与已启用 Mod 包，构建 `ContentIndex`，完成初始任务激活后才原子发布 `ScenarioRun`。
- `ScenarioRun` 只读持有本次开局冻结的 `ContentIndex`；结束、停止、关闭以及开局失败都会释放对应资源句柄。
- 当前边界：尚无剧本级 Mod 包依赖、覆盖和热切换协议，因此本切片只冻结“默认包 + 已启用 Mod 包”的稳定顺序，不新增 Catalog、Registry、Session、Loader 或桥接层。
- 验证：RED `Logs/TestResults-Gameplay-ScenarioContentOwner-RED-R1.xml`（`0/1`）；GREEN `Logs/TestResults-Gameplay-ScenarioContentOwner-GREEN-R1.xml`（`1/1`）；剧本内容 PlayMode `4/4`；全量 EditMode `93/93`；全量 PlayMode `17/17`；场景重建返回码 `0`。
- 下一步：模块 2.1b，审查 `ContentIndex` 的跨资产校验与不可变查询边界。

## 2026-08-11：模块 2.1b 内容校验与不可变查询集合

- 保留现有 `ContentIndex`、`ContentValidator` 和派生内容的校验钩子，没有新增 Catalog、Registry 或中央类型分发器。
- `ContentValidationReport.Issues` 和传给 Mod 派生内容的 `ContentValidationContext.Assets` 改为真实只读视图，调用方不能通过强转清除错误或改变校验集合。
- 重复传入同一个内容资产由校验层报告 `CONTENT_ASSET_DUPLICATE_REFERENCE` 错误，避免继续执行到字典底层异常；内部非法输入仍直接失败，不静默去重。
- RED：`Logs/TestResults-Gameplay-ContentIndex-RED-R2.xml`，目标测试 `4/7`，3 个失败分别命中上述旧行为。
- GREEN：`Logs/TestResults-Gameplay-ContentIndex-GREEN-R1.xml`，目标测试 `7/7`。
- 全量 EditMode：`Logs/TestResults-Gameplay-Module21b-EditMode-R1.xml`，`418/418` 通过，`1` 条既有缺包测试跳过，失败 `0`。
- 下一步：模块 2.1c，单独审查资源句柄是否由单局 owner 成对持有和释放。

## 2026-08-11：模块 2.1c 单局内容资源句柄归属

- 裁决：保留 `ScenarioDirector` 作为内容资源句柄的唯一 owner。它创建外部句柄并负责失败回收、结束回收和系统关闭回收；`ScenarioRun` 只拥有 `ContentIndex`，不把 YooAsset / `ResourceHandle` 依赖带入领域对象。
- 生产代码无需重构：现有 `StartScenario` 在成功发布单局前不写活动字段，失败时释放局部句柄；`ReleaseActiveRun` 先清空活动引用，再结束单局并在 `finally` 释放句柄。
- 新增稳定生命周期特征测试 `EndScenario_ReleasesItsContentResourceHandle`，验证活动期间句柄有效，正式结束单局后句柄失效且活动单局清空。
- 定向 PlayMode：`Logs/TestResults-Gameplay-ContentHandle-PlayMode-R1.xml`，`1/1`。
- 全量 PlayMode：`Logs/TestResults-Gameplay-Module21c-PlayMode-R1.xml`，`28/28`。
- 限制：运行中卸载 Mod 包与活动单局的协商协议尚不存在；当前也没有正式卸载调用入口。该问题登记到 9.1，不在本切片新增锁表、影子状态或 Mod 业务。
- 下一步：2.2，审查单局创建、结束和重复开局的状态边界。

## 2026-08-11：模块 2.2 单局创建与结束

- 裁决：当前同步开局边界没有真实并发入口，不新增 Starting / Ending 状态机、锁表、会话包装或第二生命周期枚举。
- 保留现有明确失败语义：导演未启动、内容 ID 无效、已有活动单局、没有活动单局时结束或推进，都会在正式入口直接抛出。
- 已有 `ScenarioRun` 结束后由唯一牌桌的结束状态拒绝旧引用继续写入；导演结束后可以重新解析内容并创建完全独立的新单局。
- 新增 `EndScenario_AllowsASeparateFreshRun`：验证活动时重复开局失败，结束后可重新开局，新旧 `ScenarioRun` 不同，新局回合归零，旧单局不能再推进。
- 定向 PlayMode：`Logs/TestResults-Gameplay-ScenarioLifecycle-PlayMode-R3.xml`，`1/1`。
- 全量 PlayMode：`Logs/TestResults-Gameplay-Module22-PlayMode-R1.xml`，`29/29`。
- 生产代码无需重构；下一步进入 2.3 场景组合职责。
## 2026-08-11：建立跨模块阶段集成门禁

- 用户目标明确为：通过分阶段可运行验证防止框架吸收跑偏，不能等全部模块完成后才第一次复现 StackCraft。
- 权威计划改为“领域模块 + 阶段门禁”两层。模块继续决定 owner；阶段门禁验证多个 owner 组合后是否形成真实玩家流程。
- 模块 2 聚焦剧本运行时核心，正式完成内容集合、创建/结束和场景组合；回合、日程、任务、发现与旅行仍归 `ScenarioRun`，但实施顺序移动到阶段 A 之后的模块 5。这里的聚焦不等于最小实现，也不允许保留临时架构。
- 阶段 A 固定在模块 2-4 后：统一 `FoundationTest` 必须走通进入场景、加载内容、创建卡牌、拖拽放置、选择行动、推进、结算和反馈。
- 阶段 B 在模块 5-6 后验证日程、任务、旅行与战斗；阶段 C 在模块 7-9 后验证 UI、存档、作者工具和全部选择吸收的模板功能。
- 每个阶段强制输出“已复现、明确排除、尚未完成”三类清单；尚未完成项存在时不能声明阶段或模板吸收完成。
- 当前执行点不变：继续 2.3 正式场景组合，随后回审模块 3、4 并执行阶段 A。

## 2026-08-11：模块 4.1 行动作者源与模板配方冲突语义订正

- 删除 `ActionDefinition` 的同参与条件签名比较和 `ACTION_CONDITION_SIGNATURE_SHARED` 警告。CardLoop 的同条件多行动表示玩家可选的探索、采集、焚烧等独立行为，不是 StackCraft 同材料随机配方冲突。
- `ActionDefinition` 新增负回合数作者校验，非法内容在建立运行时索引前报告 `ACTION_TURN_COST_INVALID`。
- `DisplayableContentAsset.Icon` 与 `CardDefinition.CardArt` 不再在 getter 中补建空 `SoftAssetReference`；读取可选表现配置不会修改 ScriptableObject 作者资产，`Artwork` 明确处理空引用。
- 新增对应稳定边界测试：同条件多行动无冲突、负回合数提前拒绝、读取可选表现引用不修改资产。
- 静态验证：`spec-lint` 通过，目标文件 `git diff --check` 无错误，横查未发现第二套签名实现或同类 getter 赋值。
- Unity 项目占用状态解除后已完成新鲜运行验收：行动作者校验 `5/5`，内容身份与可选表现引用 `3/3`，行动候选 `2/2`，统一 Foundation 场景 `13/13`。全量 EditMode 为 `427/428` 通过、`0` 失败、`1` 条既有 UnitySkills 缺包条件忽略；全量 PlayMode 为 `30/30`。证据分别为 `Logs/TestResults-Gameplay-Module41-Actions-GREEN-R2.xml`、`Logs/TestResults-Gameplay-Module41-Content-GREEN-R2.xml`、`Logs/TestResults-Gameplay-Module41-Candidates-R1.xml`、`Logs/TestResults-Gameplay-Module41-Foundation-PlayMode-R1.xml`、`Logs/TestResults-Gameplay-Module41-EditMode-R1.xml` 与 `Logs/TestResults-Gameplay-Module41-PlayMode-R1.xml`。
- 模块 4.1 完成；下一步进入 4.2，审查行动候选、玩家选择与行动计划边界。

## 2026-08-11：模块 4.2 行动候选、玩家选择与单局命令入口

- 发现并删除候选链中的第二定义来源：`ScenarioRun.FindActionCandidates` 不再接收行动 SO 集合，只接收唯一行动 ID；每个 ID 必须从当前单局冻结的 `ContentIndex` 解析为正式 `ActionDefinition`，未知或非行动内容立即失败。
- `ScenarioRun.StartAction` 成为玩家、UI 和测试场景的唯一公开行动命令入口。它先复核行动已在当前单局发现，再调用程序集内部的 `Tabletop.StartAction` 复核槽位绑定并创建运行实例；调用方不能绕过单局发现权限直接启动行动。
- `TabletopActionChoicePanelData` 持有所属 `ScenarioRun` 而不是牌桌写入口；按钮点击只把候选转换为 `ActionRequest` 并提交给单局。没有新增 ActionPlan、候选注册表、权限缓存或第二事件。
- 删除候选解析器内部无职责的转发方法；内部收到空行动或无效行动 ID 时直接报告不变量错误，重复行动 ID 仍按正常多来源合并语义只显示一次。
- 新鲜验证：单局候选与命令边界 `7/7`，统一 Foundation 场景 `13/13`；全量 EditMode `429/430`，`0` 失败、`1` 条既有 UnitySkills 条件忽略；全量 PlayMode `30/30`。证据为 `Logs/TestResults-Gameplay-Module42-ActionOwner-GREEN-R1.xml`、`Logs/TestResults-Gameplay-Module42-Foundation-PlayMode-R1.xml`、`Logs/TestResults-Gameplay-Module42-EditMode-R1.xml` 和 `Logs/TestResults-Gameplay-Module42-PlayMode-R1.xml`。
- 模块 4.2 完成；下一步进入 4.3，请求复核与运行实例。

## 2026-08-11：模块 4.3 行动请求复核与运行实例

- `ActionRequest` 明确为已确认计划形成的短暂提交命令，不再声称自身可保存或可同步；它只携带行动内容 ID、槽位 key 和局内卡牌 ID。存档事实继续由 `ActionInstanceSnapshot` 承担，未来网络 DTO 由联机协议决定。
- 待填充 `ActionCandidate` 不能直接转换为请求；填槽 UI 必须形成完整候选后才能提交，避免把不完整计划交给运行实例再失败。
- 请求从本局内容索引重建作者槽位后，在 `CreateCandidateFromRequest` 唯一入口复核参与数量、当前卡牌、内容类型和动态 GAS 标签。删除随后再次检查“作者源对象是否仍相同”“槽位对象是否仍相同”的不可达防护与重复复核。
- `ActionInstance` 继续冻结行动 ID、参与绑定、回合消耗、权威结果分支、结果计划、进度和生命周期状态；这些是进行中行动的真实运行事实，不是作者源副本。当前没有网络命令或外部持久引用消费者，因此不提前新增运行实例 ID。
- 修正一条耦合旧错误文案的测试：现在验证结算后旧请求被拒绝且不会重复结算；测试先明确建立行动已发现前提，并走 `ScenarioRun.StartAction` 公开入口。
- 新鲜验证：待填充请求 `2/2`，行动实例 `16/16`，结果结算 `11/11`，统一 Foundation 场景 `13/13`；全量 EditMode `429/430`、`0` 失败、`1` 条既有忽略；全量 PlayMode `30/30`。证据为 `Logs/TestResults-Gameplay-Module43-IncompleteRequest-GREEN-R1.xml`、`Logs/TestResults-Gameplay-Module43-ActionInstance-GREEN-R1.xml`、`Logs/TestResults-Gameplay-Module43-Settlement-R3.xml`、`Logs/TestResults-Gameplay-Module43-Foundation-PlayMode-R1.xml`、`Logs/TestResults-Gameplay-Module43-EditMode-R1.xml` 与 `Logs/TestResults-Gameplay-Module43-PlayMode-R1.xml`。
- 模块 4.3 完成；下一步进入 4.4，审查回合 / 即时进度、暂停与完成结算。

## 2026-08-11：模块 4.4 剧本时间规则与统一行动进度

- 删除无独立身份和生命周期的 `TurnTimingDefinition` ScriptableObject、测试资产、YooAsset 收集项和场景 Inspector 引用。它曾要求同一剧本额外配置一次每回合秒数，形成场景可替换的第二规则源。
- `ScenarioDefinition` 现在与每日回合数一起声明唯一 `SecondsPerTurn`；作者校验拒绝非有限值和非正数。`ScenarioRun` 在开局时冻结该值，并提供 `UseRealTimeProgression()` / `UseTurnBasedProgression()` 模式入口。
- `Tabletop` 只在程序集内部接收当前单局的秒数并把 `deltaTime / SecondsPerTurn` 写入同一个 `ActionInstance.ProgressedTurns`；行动作者仍只有 `TurnCost`，战斗时间线不读取该值。
- Foundation 场景由正式生成器重建，测试剧本保存 `0.35` 秒/回合；测试场景不再持有时间规则引用。默认回合制、切即时制、全局暂停、行动暂停、恢复和完成继续走同一运行实例。
- 完成结算已复核：提交前完整校验移除对象、产物内容、锚点、战斗占用、容量和放置规则，当前单线程唯一写入口没有真实证据需要结算失败影子状态或事务包装。
- 新鲜验证：剧本时间规则 `8/8`，Foundation `13/13`；全量 EditMode `430/431`、`0` 失败、`1` 条既有忽略；全量 PlayMode `30/30`。证据为 `Logs/TestResults-Gameplay-Module44-ScenarioTiming-R1.xml`、`Logs/TestResults-Gameplay-Module44-Foundation-PlayMode-R1.xml`、`Logs/TestResults-Gameplay-Module44-EditMode-R1.xml` 与 `Logs/TestResults-Gameplay-Module44-PlayMode-R1.xml`。
- 模块 4.4 完成；下一步进入 4.5，审查行动快照与恢复边界。

## 2026-08-11：模块 4.5 活动行动快照边界

- 保留 `ActionInstanceSnapshot` 对活动行动真实事实的保存：行动内容 ID、冻结回合消耗、已推进回合、运行 / 暂停状态、参与卡牌绑定、已选随机分支和冻结结果计划。
- 删除快照中的 `CancellationReason`。活动快照只来自 `Tabletop.ActiveActions`，取消和完成行动已经离开该集合，因此该字段没有合法非 `None` 状态；运行对象本身继续记录真实取消原因。
- 删除无消费者的原始 `ulong` 公共投影 `CardIdValues` 与 `RemovalCardIdValues`。Unity 序列化仍使用内部数值数组，对外只保留类型化局内卡牌 ID 和结果快照对象。
- 恢复仍先在局部集合重建和校验全部行动，全部合法后才一次发布；单项损坏不会留下半恢复活动集合。行动模式、世界回合、发现、任务、权威随机和内容包版本不属于行动快照，等待统一 `ScenarioRun` 存档边界，不在这里复制。
- 新鲜验证：行动快照 `16/16`；全量 EditMode `430/431`、`0` 失败、`1` 条既有忽略；全量 PlayMode `30/30`。证据为 `Logs/TestResults-Gameplay-Module45-ActionSnapshot-R1.xml`、`Logs/TestResults-Gameplay-Module45-EditMode-R1.xml` 和 `Logs/TestResults-Gameplay-Module45-PlayMode-R1.xml`。
- 模块 4.1-4.5 当前范围完成；下一步执行阶段 A 核心闭环门禁。

## 2026-08-11：阶段 A StackCraft 核心卡牌行动闭环

### 已由新框架复现

- 进入与启动：Test Runner 真实加载统一 `FoundationTest` 场景，`GameManager`、`ScenarioDirector` 和活动 `ScenarioRun` 完成启动。
- 内容与创建：测试内容由现有 `ResourceSystem` / YooAsset 加载，本局 `ContentIndex` 解析剧本、行动、任务和卡牌；牌桌创建四张卡牌并由 `TabletopView` 加载卡面与实例化视图。
- 拖拽放置：真实新输入系统鼠标拖拽覆盖中段拆堆、按下偏移、桌面边界和整堆重叠分离，最终位置只由 `Tabletop` 写入。
- 选择与启动：拖到目标卡牌只生成候选，UIKit 面板显式选择后通过 `ScenarioRun.StartAction` 复核发现权限和当前绑定，创建唯一 `ActionInstance`。
- 推进、结算和反馈：底部 HUD 两次确认回合，行动按 `TurnCost = 2` 完成；权威随机分支生成对应产物，旧参与卡与视图移除，产物视图出现，任务日志消费完成事实并显示完成。

### 明确排除

- 不吸收 StackCraft 合堆后自动扫描并启动制作；空间放置与行动选择保持分离。
- 不吸收多个完整配方按权重随机替玩家选择；随机只存在于玩家已选择行动的内部结果分支。
- 不吸收 `Resources.LoadAll`、固定 `Main` 场景、`CraftingManager`、`CraftingTask`、`RecipeDefinition.Execute`、`isContinuous` 或 UI 直接修改世界状态。

### 本阶段阻塞缺口

- 无。正式地点 / 工位行动提供者、待填充候选 UI、生产级界面、存档、联机和 Mod 协议属于后续模块，不在阶段 A 范围内，也没有被测试 harness 宣称为完成。

### 验证

- 最新 `Logs/TestResults-Gameplay-Module45-PlayMode-R1.xml` 中 `FoundationTestScenePlayModeTests` 为 `13/13`；内容视图、边界拖拽、重叠放置和 HUD 完整行动四项门禁均为 `Passed`。
- 同轮全量 EditMode `430/431`、`0` 失败、`1` 条既有忽略；全量 PlayMode `30/30`。
- 阶段 A 通过；下一步进入模块 5.1。

## 2026-08-11：模块 5.1 统一世界回合时间线

- 修正即时制只推进行动、不推进世界回合的断裂。`ScenarioRun` 现在按剧本 `SecondsPerTurn` 累计即时秒数；每跨满一个回合边界，就提交与手动确认相同的 `ConfirmedTurnIndex`、日期、任务事实和 `ScenarioTurnConfirmedEvent`。
- 即时推进按世界回合边界分段交给牌桌，保持行动完成、日期变化和回合事实的因果顺序；默认回合制继续只在玩家确认时推进。
- 即时制下手动 `ConfirmTurn()` 直接拒绝，避免自动时钟和按钮形成两个世界回合写入口。切回回合制只允许在世界回合边界，不能静默丢弃半回合时间。
- 没有复制 StackCraft `TimeManager`、`DayCycleManager`、时间倍率字段或固定日结阶段；`ScenarioDirector.Update()` 仍只把 Unity 游戏时间交给活动单局。
- 新鲜验证：单局时间线 `9/9`，Foundation `13/13`；全量 EditMode `431/432`、`0` 失败、`1` 条既有忽略；全量 PlayMode `30/30`。证据为 `Logs/TestResults-Gameplay-Module51-RealTimeWorldTurns-GREEN-R2.xml`、`Logs/TestResults-Gameplay-Module51-Foundation-PlayMode-R1.xml`、`Logs/TestResults-Gameplay-Module51-EditMode-R1.xml` 与 `Logs/TestResults-Gameplay-Module51-PlayMode-R1.xml`。
- 模块 5.1 完成；下一步进入 5.2，审查天数派生与日程阶段边界。

## 2026-08-11：模块 5.2 天数派生与日程阶段边界

- 对照 StackCraft `TimeManager`、`DayCycleManager` 与当前 Gameplay 全量跨日消费者后，确认当前只有任务日志和 HUD 使用日期；没有饥饿、天气、危机、输入锁、自动保存或其它需要分阶段等待的规则。
- `ScenarioRun` 已以总确认回合作为唯一可写时间事实，当前日与当日已确认回合由 `TurnsPerDay` 推导。回合制和即时制共用同一日界；跨日时先提交按天任务事实，再发布已有 `ScenarioTurnConfirmedEvent`。
- 因此本切片不修改生产代码，也不新增 `DayCycleSystem`、日开始 / 日结束事件、阶段枚举、规则注册表或空日程 pipeline。首个真实跨日规则出现后，才根据是否需要暂停、等待玩家选择或异步串行结算裁决正式运行对象。
- 文档已订正旧的“日结必然采用 pipeline”预设，并明确参考矩阵中旧 5.2-5.9 是历史实施编号；这些既有任务与发现能力将在现行 5.3 统一回审，避免重复实现。
- 新鲜验证：`ScenarioRunEditModeTests` `9/9`、统一 `FoundationTestScenePlayModeTests` `13/13`，均零失败。证据为 `Logs/TestResults-Gameplay-Module52-DayBoundary-R2.xml` 与 `Logs/TestResults-Gameplay-Module52-Foundation-PlayMode-R1.xml`。首次 Unity 命令附带 `-quit`，在测试开始前主动退出，没有生成 XML，不计入验收；移除该参数后同一正式链路通过。
- 模块 5.2 完成；下一步进入 5.3，重审任务日志与内容发现事实。

## 2026-08-11：模块 5.3 任务日志与内容发现事实

- 对照 StackCraft `QuestInstance / QuestManager`、2DRPGEngine `QuestProgress / IQuestTaskProgress` 与当前代码后，确认旧 `QuestLog` 把单个任务藏为私有字典状态，外部只能查询状态，无法读取任务对象与子项进度；程序集内部还可按 ID 直接完成任务。
- 新增 `QuestProgress` 领域对象，直接拥有任务定义、生命周期状态和只读子项运行状态。`QuestLog.GetQuest()` 返回同一运行对象，不再提供只返回状态的浅查询；任务完成提交改为日志私有步骤，不能绕过子项事实解释器。
- `QuestTaskProgressSnapshot` 公开当前值与目标值，完成状态由两者推导，避免数值进度与完成布尔形成双重真相。行动、天数、发现三种现有子项和测试 Mod 派生子项都从同一进度合同读取。
- 修复旧发现专用回放造成的语义不一致：`ScenarioRun.RefreshQuestState()` 统一刷新当前日期和已发现内容，后置解锁的状态型任务会立即读取已经成立的状态；行动完成仍只提交一次，不会推进刚解锁的后继行动任务。
- RED 证据 `Logs/TestResults-Gameplay-Module53-StateRefresh-RED-R2.xml` 为 `0/1`，失败明确是第 1 天已经成立后才解锁的按天任务仍为活动。GREEN 定向 `Logs/TestResults-Gameplay-Module53-QuestAggregate-GREEN-R3.xml` 为 `17/17`。
- 统一 Foundation 最终重跑 `13/13`；全量 EditMode `432/433`、`0` 失败、`1` 条既有忽略；全量 PlayMode `30/30`。最终 Foundation 证据为 `Logs/TestResults-Gameplay-Module53-Foundation-PlayMode-R2.xml`，全量证据为 `Logs/TestResults-Gameplay-Module53-EditMode-R1.xml` 与 `Logs/TestResults-Gameplay-Module53-PlayMode-R1.xml`。
- 模块 5.3 完成；下一步进入 5.4，审查场景旅行。

## 2026-08-11：模块 5.4 场景旅行

- `ScenarioRegionDefinition` 成为地区作者源，拥有地区场景地址、地区牌桌规则和抵达位置；`ScenarioDefinition` 只引用地区内容 ID 和初始地区，删除剧本级初始场景地址与牌桌配置。
- `ScenarioRun` 拥有多个 `ScenarioRegion`，每个地区长期保留自己的 `Tabletop`。旅行迁移原 `TabletopCard` 对象，跨地区共享局内卡牌 ID 序列，因此角色卡的唯一 EX-GAS `AbilitySystemCell` 不会因场景切换被重建或丢失。
- `ScenarioDirector.TravelAsync` 先完成旅行者、战斗 / 活动行动、目标牌桌放置和场景地址校验，再调用唯一 `GameCore.SceneSystem.TransitionToAsync`；成功回调提交迁移，失败取消待定旅行。未旅行卡牌继续留在来源地区，所有地区共享同一世界回合 / 即时时间线。
- `ScenarioTravelPlan` 保留为异步场景切换期间的一次性校验提交凭据，不是作者内容、存档副本或第二牌桌状态。无正式消费者的 `ScenarioRegionChangedEvent` 已删除。
- 旧固定场景名旅行、跨场景普通卡牌快照重建、`TravelTo(string sceneName)` 和第二套场景切换入口未进入正式链路。
- 定向旅行 `Logs/TestResults-Gameplay-Module54-Travel-PlayMode-R2.xml`：`1/1`；Foundation `Logs/TestResults-Gameplay-Module54-Foundation-PlayMode-R1.xml`：`13/13`；全量 PlayMode `Logs/TestResults-Gameplay-Module54-PlayMode-R1.xml`：`30/30`；全量 EditMode `Logs/TestResults-Gameplay-Module54-EditMode-R2.xml`：`433/434`，`0` 失败、`1` 条既有条件忽略。
- `node .spec/tools/spec-lint.mjs` 通过；旧字段搜索仅剩地区作者源 / 测试辅助中的合法 `m_tabletopPlacement`，未发现生产代码残留剧本级初始场景字段或地区广播消费者。
- 模块 5.4 当前范围完成；下一步进入模块 6 的牌桌内战斗与 EX-GAS 回审。模块 6 未完成前不执行阶段 B，也不开始原创剧本业务。

## 2026-08-11：模块 6.1 战斗方与阵型归属订正

- 删除 `BattleParticipant.FactionTagCode` 与按 GAS 阵营标签配置的 `BattleFactionFormationRules`。角色阵营 / 敌我关系是角色 ASC 与剧本关系规则事实，本场站在哪一边则是 `Battle` 的临时分组，不能合并成同一字段。
- `Battle` 现在直接拥有多个 `BattleSide`，每个战斗方拥有参战卡牌 ID；支持多个 GAS 阵营临时结盟到同一战斗方，也不会要求 Mod 新增阵营时修改阵型配置。
- `BattleFormationRules` 改为按战斗方顺序配置 `BattleSideFormationRules`，继续只派生表现位置和排序，不写卡牌权威位置。测试资产已由正式生成器重建，没有保留旧序列化字段兼容层。
- 离开后只剩一个有成员的战斗方时，由牌桌结束战斗。6.2 进一步确认旧活动战斗快照无法与角色 GAS 一起恢复，已整体删除；当时曾计划等待 6.4 重建，后被 2026-08-12“战斗不存档”的产品裁决明确覆盖，不再重建。
- 验证：战斗方 / 阵型定向 `Logs/TestResults-Gameplay-Module61-BattleSides-R1.xml` 为 `7/7`；Foundation `Logs/TestResults-Gameplay-Module61-Foundation-PlayMode-R1.xml` 为 `13/13`；全量 EditMode `Logs/TestResults-Gameplay-Module61-EditMode-R1.xml` 为 `432/433`、`0` 失败、`1` 条既有忽略；全量 PlayMode `Logs/TestResults-Gameplay-Module61-PlayMode-R1.xml` 为 `30/30`。
- 模块 6.1 当前范围完成；下一步进入 6.2，回审角色卡唯一 ASC 与战斗参与资格，不提前实现攻击业务。

## 2026-08-11：模块 6.2 角色卡唯一 ASC 与参战资格

- 新增 `CharacterCardDefinition : CardDefinition`。它只引用一个 EX-GAS ASC 预设，初始标签、属性集、等级和基础技能继续来自 EX-GAS 表，不在 Gameplay SO 中重复配置。
- `Tabletop.CreateCard` 成为普通卡和角色卡的唯一公开创建入口：普通 `CardDefinition` 创建 `TabletopCard`，`CharacterCardDefinition` 创建直接拥有唯一 `AbilitySystemCell` 的 `CharacterCard`。公开手工 `CreateCharacterCard(..., AbilitySystemCellConfig)` 已删除。
- ASC 预设 Inspector 使用 EX-GAS 官方通用选择访问层；Gameplay Runtime 只增加 `com.exhard.exgas.general` 正式引用，没有依赖 Luban 生成配置内部类型。
- `Tabletop.StartBattle` 只接受实际为 `CharacterCard` 的参战卡。普通物品、地点和事件卡没有 ASC，进入战斗会在唯一入口立即报错，不把类型判断拖进后续攻击循环。
- 删除 `BattleSnapshot`、`BattleSideSnapshot`、`TabletopBattleStateSnapshot`、牌桌战斗恢复构造函数和创建快照入口。旧链只能用普通卡牌恢复战斗，与角色唯一 ASC 契约冲突；6.4 必须等角色 GAS 状态具备正式快照后再建立。
- 验证：角色卡 / 战斗定向 `Logs/TestResults-Gameplay-Module62-CharacterCards-R3.xml` 为 `7/7`；Foundation `Logs/TestResults-Gameplay-Module62-Foundation-PlayMode-R1.xml` 为 `13/13`；ScenarioContent `Logs/TestResults-Gameplay-Module62-ScenarioContent-R1.xml` 为 `7/7`；全量 EditMode `Logs/TestResults-Gameplay-Module62-EditMode-R1.xml` 为 `432/433`、0 失败、1 条既有忽略；最终全量 PlayMode `Logs/TestResults-Gameplay-Module62-PlayMode-R2.xml` 为 `30/30`。
- 模块 6.2 当前范围完成；下一步进入 6.3，审查实时战斗调度如何直接激活角色卡 EX-GAS Ability，不建立第二技能或伤害链。

## 2026-08-11：模块 6.3 攻击 Ability 前置审查

- EX-GAS 当前有正式 `Attack(20001)` Ability 与 Timeline 101，但命中任务使用 `CatchAreaPolygon2D`，依赖 2D 场景角色的 `Movable`、姿态和物理层；纯牌桌 `CharacterCard` 不具备这些场景组件。
- 当前 EX-GAS ASC 预设的基础技能列表均为空，角色卡不会自然持有 `Attack(20001)`。生成常量只证明身份存在，不证明牌桌角色已经拥有或能正确执行它。
- 因此本步没有新增 `Battle.Update`、攻击进度字段、Ability 转发器或直接伤害入口。下一动作是通过 EX-GAS 正式表建立以 `AbilityActivationContext.MainTarget + CatchTarget` 选择牌桌目标的攻击 Ability，再由战斗聚合提交施法者、目标和 Ability 码。
- 该作者表前置未完成前，6.3 不能声明完成；这不是用手写攻击链绕开的理由。

## 2026-08-12：模块 6.3 战斗 Ability 请求边界

- `Tabletop.RequestBattleAbilityActivation` 成为牌桌战斗提交 GAS Ability 的正式入口。它只确认牌桌、活动战斗、施法角色、目标角色和施法者已经拥有的 Ability，再把施法者牌桌位置与目标角色唯一 ASC 写入 `AbilityActivationContext`。
- 激活可用性直接调用 `AbilitySpec.CheckActivation()`；成功后调用 `AbilitySpec.TryActivate(context)`。Gameplay 没有复制标签、Cost、Cooldown、Timeline、TargetCatcher、GameplayEffect 或伤害结算职责。
- 非参战卡作为目标、施法者没有对应 Ability 时直接报错；正常 GAS 激活失败返回官方 `AbilityActivationResult`，不自动授予技能或静默继续。
- RED 编译失败证据为 `Logs/Unity-Gameplay-Module63-BattleAbility-RED-R1.log`，缺失入口明确命中目标行为。GREEN 单项 `Logs/TestResults-Gameplay-Module63-BattleAbility-GREEN-R3.xml` 为 `1/1`；战斗聚合契约 `Logs/TestResults-Gameplay-Module63-BattleAbility-Contract-R1.xml` 为 `6/6`；Foundation `Logs/TestResults-Gameplay-Module63-Foundation-PlayMode-R1.xml` 为 `13/13`；全量 EditMode `Logs/TestResults-Gameplay-Module63-EditMode-R1.xml` 为 `434/435`、`0` 失败、`1` 条既有条件忽略；全量 PlayMode `Logs/TestResults-Gameplay-Module63-PlayMode-R1.xml` 为 `30/30`。
- 该进展只证明战斗聚合可以正确提交目标型 GAS Ability，不证明牌桌攻击已经完成。EX-GAS Excel 作者源、官方生成、ASC 正式授予和 GE 2003 真实扣血仍是 6.3 未完成项。

## 2026-08-12：模块 6.3 牌桌攻击作者链与真实结算收口

- 使用 EX-GAS 正式作者表创建牌桌攻击 Ability `20005 / TabletopBasicAttack`、Timeline `20005` 和 GE `2003`；ASC `1001` 正式授予 Ability `20005`。没有手改生成 JSON / C#，没有保留一次性作者脚本。
- 修复第三方 GAS 编辑器的两个实际问题。时间轴作者页不再用 EPPlus 删除整行重建数据区，改为清空数据区后重写，避免表头批注 VML 锚点被错误重定位；Ability 作者页按 EX-GAS 三段标签协议写入 `ActivationBlockedTags`，避免 `3003` 被错误解释为 `All` 而不是 `None`。
- 已用 Microsoft Excel 原生打开并保存损坏的时间轴工作簿，确认内部 VML 已恢复完整 `<x:Anchor>`；之后插件再次保存仍保留锚点。因此损坏的作者文件已修复，不是只绕过异常。插件源码改动属于本项目当前锁定版本的补丁，升级插件时必须重新审查并回归。
- 伤害结算已从“必须找到场景 `CharacterBase` 才执行”重构为“先解析唯一 GAS ASC，再按是否存在场景表现对象选择表现路径”。纯牌桌角色直接读取和写回 GAS FightUnit/Health；不复制生命值，也不要求角色卡创建 Unity 场景角色。
- 真实 RED/GREEN：新增 Foundation PlayMode 伤害测试首次 `0/1`，失败证据为目标生命未下降；修正伤害执行和纯牌桌冲击表现边界后 `1/1` 通过。场景角色正式伤害回归 `7/7`，Foundation `14/14`，全量 EditMode `433/434`（1 条既有忽略），全量 PlayMode `31/31`。
- 当前结论：6.3 已完成当前切片。已复现：牌桌战斗提交正式 Ability、Timeline 目标捕获、GameplayEffect 扣除牌桌角色 GAS 生命、Ability 正常结束。尚未完成：6.4 权威随机和战斗快照边界；因此模块 6 和阶段 B 仍未完成，不能进入原创业务。

## 2026-08-12：模块 6.4 权威随机与战斗快照边界

- 对照 StackCraft `CombatManager / CombatTask / CombatData` 后确认：模板只保存战斗双方卡牌和战斗区域位置，恢复时重新创建战斗任务；没有保存攻击进度、正在执行的 Ability、命中上下文、随机流或 GAS 状态。因此不吸收模板的残缺战斗快照行为。
- `Tabletop` 的单局权威随机流现在在创建战斗时派生独立战斗随机流；每次成功提交牌桌 Ability 时由所属 `Battle` 取得一次激活种子；EX-GAS `AbilityActivationContext` 携带该种子，Timeline 的 `TaskApplyEffects` 为每个 GameplayEffect 派生独立种子，GameCore 伤害结算优先使用该种子，不再用 ECS Entity 编号作为牌桌战斗随机来源。
- 牌桌未初始化权威随机时创建战斗直接失败；相同牌桌种子下战斗激活种子序列稳定，创建其它战斗不会消耗已有战斗的随机流。定向契约测试 `BattleEditModeTests` 为 `7/7`，真实牌桌攻击回归 `FoundationTestScenePlayModeTests` 为 `1/1`。
- 用户产品裁决：战斗不存档。恢复激活 Ability、持续 GameplayEffect、冷却、临时标签、Timeline 进度和随机流复杂度高，而本游戏重心在局外成长，收益不足。因此模块 8 不再接管战斗快照；未来存档流程在活动战斗期间如何处理，等正式存档交互阶段再决定，但不得保存半套战斗状态。
- 命中、闪避、暴击和属性公式当前不定案。Foundation 测试配置关闭闪避与暴击，只用 EX-GAS 表中的固定基础攻击、攻击和防御测试值证明 Ability -> Timeline -> GE -> GAS Health 链路；这些测试数值不是正式游戏平衡。
- 固定伤害配置的新鲜回归证据为 `Logs/TestResults-Gameplay-Module64-FixedDamage-PlayMode-R1.xml`：目标真实攻击用例 `1/1` 通过，零失败、零跳过。
- 当前结论：6.4 与模块 6 当前地基切片完成；下一步进入阶段 B 组合验收。自动战斗调度和复杂命中规则属于未来明确玩法需求，不阻塞模板战斗地基吸收。

## 2026-08-12：阶段 B 运行玩法组合验收

- 统一 `FoundationTest` 新增一条连续玩家流程：在同一个 `ScenarioRun` 中选择并完成行动，推进两个世界回合进入第 2 天，完成任务，携带同一角色卡和唯一 EX-GAS `AbilitySystemCell` 旅行到第二地区，再通过 Ability `20005`、Timeline 和 GE `2003` 对敌方角色造成固定基础伤害；战斗结束后，原日程、任务和角色状态保持不变。
- 第一条有效 RED 为 `Logs/TestResults-Gameplay-StageB-RED-R3.xml`，行动、日程和任务已完成后，正式 `TravelAsync` 明确报出统一测试剧本不包含第二地区。测试作者源随后新增“地基战斗测试地区”，并由现有“重建测试场景”入口写入统一测试剧本，没有手改 SO YAML 或新增测试专用运行 API。
- 组合验收发现第二地区牌桌没有随机流，导致同一单局旅行后无法开战。权威随机职责已从测试场景手工初始化牌桌，重构为 `ScenarioDirector` 开局提供一次非零根种子，`ScenarioRun` 按地区创建顺序派生独立牌桌种子，`ScenarioRegion` 创建牌桌时立即注入；合法单局不再存在“牌桌已创建但随机行动 / 战斗不可用”的半合法状态。显式根种子入口供未来权威端、回放和确定性测试使用。
- 阶段 B 还暴露 EX-GAS 原生内存析构缺口：Ability 标签、GameplayEffect 条件和 Cue 列表使用 Persistent `NativeArray`，原插件销毁效果时只释放堆叠数组，关闭 World 时也没有完整释放。新增插件内部统一原生容器析构，并让正常 GE 销毁和 GAS World 关闭共同遵守同一所有权规则；定向日志由 `25` 笔泄漏降到 `1`，最终降为 `0`，没有用测试屏蔽或修改生成代码绕过。
- GREEN 证据：组合用例 `Logs/TestResults-Gameplay-StageB-GREEN-R6.xml` 为 `1/1`；完整 Foundation `Logs/TestResults-Gameplay-StageB-Foundation-PlayMode-R1.xml` 为 `15/15`；全量 PlayMode `Logs/TestResults-Gameplay-StageB-AllPlayMode-R1.xml` 为 `32/32`；全量 EditMode `Logs/TestResults-Gameplay-StageB-AllEditMode-R2.xml` 为 `435/436`、零失败、1 条 Cinemachine 已安装导致的条件不适用跳过。最终日志均没有 `Leak Detected`。
- 已由新框架复现：日程推进、任务完成、同一角色跨地区旅行、角色 GAS 状态连续、牌桌战斗创建、正式 Ability / Timeline / GameplayEffect 固定伤害和战斗结束后的单局状态连续性。明确排除：战斗存档；StackCraft RPS 旧枚举结构不吸收。命中 / 闪避 / 暴击不再作为产品排除，只是尚未在阶段 B 实现完整链，后续按 GNS/EX-GAS 数值链和模板临时参数复现。尚未完成但不阻塞阶段 B：正式 UI、非战斗单局存档、作者工具、自动战斗调度、联机和 Mod 协议。
- 阶段 B 通过；下一步进入模块 7.1，先审查牌桌可读状态与正式 UI 组合，不把现有测试 HUD 和行动选择面板直接升级为正式 UI。
## 2026-08-12：模块 7.1 牌桌可读状态完成

- StackCraft 对照：吸收悬浮卡牌即时显示信息和悬浮结束后恢复持续焦点的玩家效果；排除 `CardInstance` 直接调用全局 `InfoPanel`、卡牌自行拼接 UI 字符串、请求者字典与 UI 内第二套优先级状态。
- 正式职责：`TabletopView` 作为当前牌桌的唯一 Unity 表现对象，拥有本地悬浮与选择状态；悬浮临时覆盖选择。状态不进入权威 `Tabletop`、存档或联机真相，现有 `TabletopCardDragInput` 是唯一更新入口。
- UIKit 投影：新增 `TabletopCardInfoPanel`，直接绑定 `TabletopView`。名称和描述读取正式 `CardDefinition`，当前实例读取 `TabletopCard`；没有新增 `InfoSystem`、UI Context、事件包装、第二资源入口或 GameCore 菜单依赖。
- 生命周期：权威卡牌移除或牌桌解绑会清理可读状态；面板同步清空卡牌 ID、标题和描述并解除订阅。旧 `UISystem/UIManager` 不接管牌桌详情，测试面板布局仅是 Foundation 验收夹具。
- TDD 与回归：可读状态缺失 RED 精确命中；详情链 `1/1`、移除清空 `1/1`；最终 `Logs/TestResults-Gameplay-Module71-Foundation-PlayMode-Final.xml` 为 `17/17`。真实入口截图 `Logs/Gameplay-Module71-CardInfo-PASS-Candidate.png` 经 AI 审计 `PASS 91/100`，仅证明测试详情投影可读且不遮挡。下一步进入模块 7.2 行动选择 / 填槽，不提前制作角色详情、职业或荒岛正式 UI。

## 2026-08-12：模块 7.2 行动选择与填槽

- 删除调用方维护的可用行动 ID 清单。`ScenarioRun.FindActionCandidates` 现在只从当前冻结内容索引中筛选本局已发现的 `ActionDefinition`，测试 Harness 不再保存第二份行动白名单。
- 新增 `TabletopInteraction`，作为当前牌桌的玩家交互协调组件：解释空白释放与目标卡牌释放，精确拥有自己打开的 UIKit 面板实例。测试装配器只记录输入事实和候选结果，不再执行正式玩家行为。
- `TabletopActionChoicePanel` 已去除剧本执行职责，只展示本次候选并回传玩家选择。完整候选由 `TabletopInteraction` 提交；未完整候选创建牌桌行动计划并打开填槽面板。
- 新增 `ActionPlan` 领域对象，由 `Tabletop` 直接拥有。卡牌加入、移出、取消和提交统一经过牌桌，持续复核内容、EX-GAS 条件、槽位数量与重复卡牌；UIKit 不保存第二份槽位绑定。计划完整后创建既有 `ActionRequest` 和 `ActionInstance`。
- 新增非阻塞 `TabletopActionPlanPanel` 与槽位视图。面板保持 Gameplay 输入，使玩家可继续把牌桌卡拖入 UIKit 槽位；开始按钮只在计划完整时启用，取消显式删除牌桌计划。
- 同一牌桌允许存在多个待计划；UIKit 仍只使用一个 `TabletopActionPlanPanel`，通过上一项 / 下一项切换投影牌桌集合。创建第二计划不会覆盖第一计划或留下不可达状态，取消 / 提交当前计划后继续显示剩余计划。
- 牌桌移除卡牌时会从所有待计划槽位同步解绑；填在待计划中的卡牌不能跨地区旅行，必须先调整或取消计划。计划不会引用已经离开所属牌桌的卡牌 ID。
- 测试作者源新增一个需三名参与者的“协同行动”，只在对应验收中显式发现。真实鼠标链路验证一次拖拽出现两个候选、选择未完整候选、输入恢复、第三张牌拖入槽位并提交正式行动。
- RED：`Logs/TestResults-Gameplay-Module72c-ActionPlan-RED-R2.log` 精确缺少 `ActionPlan` 与牌桌生命周期；卡牌生命周期 RED `Logs/TestResults-Gameplay-Module72-PlanCardLifecycle-RED.xml` 精确证明旅行未拒绝待计划参与卡。领域当前 GREEN：`Logs/TestResults-Gameplay-Module72-ActionPlan-GREEN-R2.xml` 为 `5/5`；多计划 UIKit 定向 `Logs/TestResults-Gameplay-Module72-MultiPlan-PlayMode-R2.xml` 为 `1/1`。
- 按钮视觉状态已在测试 UI 作者源订正：按钮底图保持白色，由 `ColorBlock` 独占正常色与禁用色，避免绿色底图与禁用色相乘后仍呈现“可开始”的绿色语义。正式生成菜单已据此重建填槽面板预制体与测试场景。
- 作者源修正后的新鲜自动回归：`Logs/TestResults-Gameplay-Module72-Foundation-VisualFinal-R1.xml` 为 `19/19`，`Logs/TestResults-Gameplay-Module72-EditMode-VisualFinal-R1.xml` 为 `23/23`，均零失败、零跳过；两份日志未发现内存泄漏、悬空引用或未处理异常。AI Bridge 的一次 PlayMode 请求虽匹配 19 个用例，但 PlayMode 切换后回调丢失并落出 `0/0` 空结果，已明确排除为无效证据，没有冒充通过。
- 当前代码版真实 GameView 有序验收图：第一步 `Logs/Gameplay-Module72-Choice-GameView-R2.png` 显示两个行动及“协同行动（还需 1）”；第二步 `Logs/Gameplay-Module72-Plan-GameView-R4.png` 显示参与者 `2/3`、灰化不可用的“开始”和仍可用的“取消”。逐图均无卡牌、回合 HUD、文字或按钮遮挡、裁切与调试标签泄漏。
- 视觉裁决：候选选择态 `PASS 92/100`，填槽缺员态 `PASS 91/100`，无硬失败项。该结论只证明模块 7.2 的地基测试投影和交互状态可读，不代表正式游戏 UI 设计已经完成。模块 7.2 当前范围完成。

## 2026-08-12：模块 7.3 角色卡状态投影

- 对照 StackCraft 后订正原“角色详情”范围。模板真实提供的是角色卡面当前生命和悬浮信息，并没有 CardLoop 设计稿中的职业、技能、经历侧栏；装备显示又依赖尚未吸收的装备领域。因此 7.3 只吸收角色卡可见状态，不让 UI 提前发明角色业务真相。
- `CharacterCard` 直接从唯一 EX-GAS `FightUnit/Health` 与 `MaxHealth` 提供当前值；没有复制生命、属性表、伤害结算或 Gameplay 事件包装。`TabletopCardView` 只投影角色对象，每帧比较 GAS 当前值，数值变化时才更新文本。
- 牌桌视图统一决定状态可见性：普通牌堆只显示顶牌状态，避免叠牌文字重叠；进入战斗阵型的角色继续显示自身状态。普通 `CardDefinition` 不显示角色状态。
- 测试预制体由正式“重建测试场景”作者入口生成深色生命状态条，没有手改 Prefab / Scene YAML。视觉审计先后淘汰了文字重叠、不可读和省略号三类失败图，最终 `Logs/Gameplay-Module73-CharacterHealth-GameView-R6.png` 清楚显示顶牌 `100/100` 与独立牌动态 `73/100`，裁决 `PASS 91/100`。
- RED 编译精确缺失 `TabletopCardView` 角色状态契约；GREEN 单条 `Logs/TestResults-Gameplay-Module73-CharacterHealth-GREEN-R4.xml` 为 `1/1`。最终 `Logs/TestResults-Gameplay-Module73-Foundation-Final-R1.xml` 为 `20/20`，`Logs/TestResults-Gameplay-Module73-CardView-EditMode-Final-R1.xml` 为 `2/2`，均零失败、零跳过。
- 历史裁决更新：StackCraft `CardInstance.CurrentHealth`、`CombatStats` 和卡牌自行扣血仍不进入正式链路；`EquipmentPanel` 的模板结构不进入正式链路，但“装备离桌后玩家仍能读到已装备物品”的效果已在 2026-08-16 由角色详情投影吸收。完整职业、技能、经历侧栏等待对应领域对象和作者源成立后再进入正式 UI。模块 7.3 当前范围完成，下一步进入 7.4 HUD 与交互反馈。

## 2026-08-12：模块 7.4 HUD 与交互反馈

- 对照 StackCraft UI 后裁决：模块 4 已吸收行动进度，模块 0.4 已承担转场，7.2 已承担行动选择 / 确认；本切片只补 `DayTimeUI` 对应的日内进度可读性，不建立第二个时间系统、弹窗系统或 HUD 状态真相。
- `ScenarioRun` 暴露每日回合数、当前推进模式和派生的归一化日内进度。回合制按“已确认日内回合 / 每日回合数”计算；即时制在同一回合事实上叠加当前回合已流逝秒数，不增加第二套时间配置。
- `ScenarioTurnPanel` 只投影活动单局：显示“第 N 天  x/y”和日内进度；回合制允许“推进回合”，即时制禁用该按钮并显示“即时推进中”。面板不拥有日期、回合或秒数状态。
- 视觉审计发现禁用按钮与可点击状态过于接近后，修正正式测试 UI 生成器的按钮禁用色并重建预制体与统一场景；没有手改生成 YAML，也没有改变玩法规则。
- 最终完整 Foundation PlayMode `Logs/TestResults-Gameplay-Module74-Foundation-PlayMode-Final.xml` 为 `21/21`，零失败、零跳过；`Logs/Unity-Gameplay-Module74-Foundation-PlayMode-Final.log` 未发现 `Leak Detected`、悬空引用、未处理异常或编译错误。
- 最终回合制与即时制真实入口截图分别为 `Logs/Gameplay-Module74-TurnBased-PASS.png`、`Logs/Gameplay-Module74-RealTime-MidProgress-PASS.png`。功能性视觉审计通过：HUD 位于底部中央、不遮挡牌桌；即时制进度可读，按钮禁用态与回合制可点击态有明确差异。该结论不代表正式美术、营养 / 货币 / 卡牌容量或原创剧本 UI 已完成。
- 模块 7.1-7.4 当前地基范围关闭。下一步进入模块 8 单局快照与存档恢复；整个 Gameplay 地基、StackCraft 完整吸收和阶段 C 仍未完成。

## 2026-08-13：模块 8.1-8.2 内容集合与整局领域快照

- 对照 StackCraft `GameData`、`SaveSystem`、`GameDirector` 和各 Manager 的保存回调后，保留“读档恢复场景、牌桌、行动、任务和时间”的玩家效果，不照搬多个 Manager 拼同一个 `GameData`。`ScenarioRun` 是整局快照唯一聚合入口，GameCore 文件槽位留给 8.4。
- 8.1 保存本局冻结内容索引的全部唯一 `ContentId`，稳定排序。读档允许当前内容集合增加，但缺少旧存档依赖时一次列出全部缺失 ID 并拒绝；包版本和 Mod 依赖等待模块 10 正式清单，不伪造来源包或版本。
- 8.2 新增 `ScenarioRunSnapshot`、`ScenarioRegionSnapshot`、`TabletopSnapshot` 和 `QuestLogSnapshot`。对象各自生成与恢复自己的状态，快照类只承载序列化事实，没有新增 `SnapshotSystem`、`SaveManager`、事件包装或第二运行真相。
- 整局只保存一份下一卡牌实例号；所有恢复地区继续共享同一 `TabletopCardIdSequence`。地区牌桌分别保存卡牌 / 牌堆、活动行动和当前权威随机状态。恢复会验证地区集合、跨地区卡牌 ID、内容引用、放置规则、行动作者源、任务状态和时间边界。
- 未提交的 `ActionPlan` 是可放弃的本地交互状态，不进入快照；活动战斗按既有产品裁决直接拒绝存档；正在切换地区的旅行事务也拒绝存档。战斗与旅行不保存半套中间状态。
- 任务子项自己创建和恢复多态状态；代码 Mod 派生任务无需中央类型 `switch`，但若没有实现自己的存档状态，会在存档时明确报出具体类型，不会静默丢进度。
- RED 编译证据为 `Logs/Unity-Gameplay-Module82-ScenarioSnapshot-RED.log`，精确缺少整局快照入口。最终整局 JSON 往返 `Logs/TestResults-Gameplay-Module82-ScenarioSnapshot-GREEN-R6.xml` 为 `1/1`；`ScenarioRun` 全组 `Logs/TestResults-Gameplay-Module82-ScenarioRun-R1.xml` 为 `13/13`；任务、行动快照、牌桌卡牌与内容集合相关回归 `Logs/TestResults-Gameplay-Module82-Related-R1.xml` 为 `42/42`。
- 8.3 前置审查确认 EX-GAS 2.0.4 没有运行时 ASC 快照 / 枚举公开 API。现有公开门面只能按已知码读写属性、标签和技能；插件文档明确禁止 Gameplay 直接读取 ECS Buffer。因此角色卡仍明确拒绝生成不完整快照，下一步必须先在 EX-GAS 正式门面补长期状态快照与恢复，不能在 Gameplay 复制生命、标签或技能列表。
- 最终回归：`Logs/TestResults-Gameplay-Module82-AllEditMode-R1.xml` 为 `441/442`、零失败、1 条既有条件忽略；`Logs/TestResults-Gameplay-Module82-Foundation-PlayMode-R1.xml` 为 `21/21`；活动战斗拒绝存档的独立边界 `Logs/TestResults-Gameplay-Module82-BattleSaveBoundary-R1.xml` 为 `8/8`。两份全量日志未发现内存泄漏、悬空引用、未处理异常或编译错误。

## 2026-08-13：模块 8.3-8.4 前置职责裁决

- EX-GAS 2.0.4 的正确扩展形状已锁定：插件 OOP 门面导出 ASC 等级、固有标签、属性基础值、Ability Code / 等级；恢复时由当前 ASC 作者配置提供属性结构与钳制，由 Gameplay 调用方按 Code 解析官方 `AbilityConfig`。瞬时 Ability、Cooldown、GameplayEffect、临时标签、Cue 和 Timeline 不保存。
- 恢复不能先创建普通 ASC 再覆盖残留状态；必须在新 Cell 初始化时按当前作者配置校验并应用快照，全部成功后才发布给角色卡，失败时释放 Cell 与原生容器。
- StackCraft 存档 UI 的真实玩家流程已对账：标题页存档列表、槽位读取 / 删除、删除确认、清空全部、关闭，以及局内保存并返回标题。`SavedGameSlot.prefab` 主要是尺寸、透明底色、TMP 排版和文本按钮，没有需单独搬运的位图素材。
- 8.4 正式边界：GameCore `SaveSystem/SaveKit` 负责文件槽位、元数据和模块容器；`ScenarioDirector` 负责 `ScenarioRunSnapshot` 的创建、内容校验、场景切换与原子发布。现有 GameCore `SaveDataBlock` 只能作为旧 RPG 可选模块，不能继续冒充整个 CardLoop 存档根，也不新增第二套 Gameplay 文件系统。
- 因角色卡快照尚需修改 EX-GAS 第三方源码，当前没有提前实现只能保存普通卡的假存档 UI；待当轮明确许可后先完成 8.3，再进入 8.4 真实端到端与模板等价截图验收。
- 8.4 文件层已继续实施：`SaveFileStorageRuntime` 现在只保存 / 读取 SaveKit `SaveData` 模块容器和 `SaveMeta`，不再固定注册 `SaveDataBlock`。现有 `SaveSystem.SaveToFile/LoadFromFile` 只是旧 RPG 世界模块的快捷行为，后续 `ScenarioDirector` 可在同一容器注册 `ScenarioRunSnapshot`，GameCore 不引用 Gameplay。
- 定向测试首次有效 RED 为 `Logs/Unity-Gameplay-Module84-SaveContainer-RED-R2.log`，精确缺少创建 / 保存 / 读取容器和元数据入口。第一次 GREEN 暴露 `SaveSlot003` 被文件名哈希映射到槽位 5 的旧债务；没有改测试掩盖，而是把全链改为整数槽位 ID，并删除文件名、字母后缀和 FNV 映射。最终 `Logs/TestResults-Gameplay-Module84-SaveContainer-GREEN-R2.xml` 为 `1/1`，GameCore 全组 `Logs/TestResults-Gameplay-Module84-GameCore-R1.xml` 为 `96/96`。
- 旧 `UISaveFile` 不再要求作者手填文件名，只配置整数槽位，并直接读取 SaveKit 元数据标题；读取按钮在槽位存在时恢复可用。模板等价的动态槽位列表会在后续替换该旧固定数组 UI，不保留两套正式菜单。

## 2026-08-13：模块 8.4 ScenarioDirector 文件接入

- `ScenarioDirector.SaveActiveRunToSlot` 直接让活动 `ScenarioRun` 生成唯一整局快照，并把 `ScenarioRunSnapshot` 注册到 GameCore 的 SaveKit 模块容器。重复保存同一槽位会保留其它领域模块，不创建第二套 Gameplay 文件格式或路径入口。
- 槽位标题由当前剧本显示名、地区显示名和派生游戏日自动生成；UI、策划和存档调用方不再维护另一份标题 key。
- `LoadRunFromSlotAsync` 先读取文件、加载当前 YooAsset 内容集合、校验内容依赖和剧本定义，并构造完整候选单局；目标地区场景切换成功后，才结束旧单局、接管新内容句柄并发布新活动单局。前置校验失败时旧单局保持可操作，场景失败时候选单局会明确结束。
- 新增 `ScenarioRunChangedEvent` 只表达导演已提交的活动单局生命周期事实。统一测试场景的牌桌表现 owner 收到后，一次性改绑 `TabletopView`、`TabletopInteraction` 和 `TabletopCardDragInput`；没有让每张卡或每个面板保存第二份活动单局状态。
- EditMode 证据：导演全组 `Logs/TestResults-Gameplay-Module84-ScenarioDirector-Final.xml` 为 `7/7`；整局快照与文件容器回归 `Logs/TestResults-Gameplay-Module84-SnapshotStorage-Regression.xml` 为 `14/14`。
- PlayMode 证据：真实普通卡槽位保存 / 读取与可见牌桌改绑 `Logs/TestResults-Gameplay-Module84-SaveLoad-PlayMode-R2.xml` 为 `1/1`；剧本内容、开始 / 结束、场景、旅行和资源句柄全组 `Logs/TestResults-Gameplay-Module84-ScenarioContent-Regression.xml` 为 `8/8`；统一地基全组 `Logs/TestResults-Gameplay-Module84-Foundation-Regression.xml` 为 `21/21`。日志没有脚本错误、未处理异常或原生弱引用泄漏。
- 当前仍未完成：角色卡 EX-GAS 长期状态快照、包含角色的完整单局保存、模板等价的动态存档列表 / 删除确认 / 保存返回标题 UI 和最终截图。普通卡端到端只证明导演、SaveKit、YooAsset、场景和牌桌改绑链路，不代表完整存档模块完成。
- 模板存档列表所需文件能力已补齐：`SaveSystem.GetAllSaveMetadata()` 返回按整数槽位升序排列的有效元数据，`DeleteSaveData()` 只在槽位真实存在且删除成功时返回 `true`，`DeleteAllSaveData()` 遍历同一有效槽位事实并返回实际删除数。没有手写目录扫描、文件名解析或第二槽位缓存。
- 槽位 RED `Logs/Unity-Gameplay-Module84-SaveSlots-RED.log` 精确缺少枚举 / 单删 / 全删入口；最终槽位合同 `Logs/TestResults-Gameplay-Module84-SaveSlots-Final.xml` 为 `2/2`。GameCore 全组 `Logs/TestResults-Gameplay-Module84-GameCore-Slots-Regression.xml` 为 `97/97`，真实存档读档回归 `Logs/TestResults-Gameplay-Module84-SaveLoad-SlotApi-Regression.xml` 为 `1/1`。

## 2026-08-13：模块 8.3 角色长期 GAS 状态订正与实现

- 先重新读取官方 `EX-GAS-2.0` README，并用本地 `2.0.4` 源码校准。此前“必须给插件增加完整 ASC 导出接口”的判断作废：UE GAS 同样把 ASC 作为运行时能力聚合，网络复制与长期 SaveGame 不是同一职责；当前产品又明确不保存战斗中状态，因此完整 ASC 导出既不是当前需求，也不是插件缺陷。
- 项目架构规范新增 UE GAS 校准门禁：使用 EX-GAS 时先比较 UE GAS 的 ASC、AttributeSet、Ability、GameplayEffect、Tag、复制与持久化职责；只有项目需求和 UE GAS 同职责都证明能力应归 GAS，且 EX-GAS 正式入口无法表达时，才提出修改插件源码。不得用反射、ECS Buffer、薄包装或第二套 GAS 状态遮住偏离。
- 当前角色卡的 ASC 预设继续作为属性集、属性、固有标签和基础技能结构来源。`CharacterAbilitySystemSnapshot` 只保存 ASC 等级和预设声明属性的 `BaseValue`；`CurrentValue` 继续由 EX-GAS GameplayEffect 推导。永久技能、永久标签和职业成长尚未形成正式角色领域入口，因此不提前保存未来集合。
- 角色恢复先按当前 `CharacterCardDefinition` 的 ASC 预设创建新 Cell，严格校验存档属性集 / 属性结构后覆盖基础值和等级。未知、重复或缺失结构立即拒绝；未发布 Cell 和后续牌桌恢复失败路径都会释放角色 ASC，不修改 EX-GAS，不读取 Watcher 或 ECS Buffer。
- `TabletopCardSnapshot` 增加自动生成的角色类型事实和可选角色状态。普通卡、角色卡都继续使用同一牌桌快照；Unity `JsonUtility` 会把空引用实例化为默认对象，因此恢复以明确类型事实与当前内容定义交叉校验，不用空引用猜类型。
- 验证：`Logs/TestResults-Module8.3-CharacterSnapshot-R2.xml` 为角色定向 EditMode `9/9`；`Logs/TestResults-Module8.3-AllGameplayEditMode-R2.xml` 为 Gameplay EditMode 全量 `124/124`；`Logs/TestResults-Module8.3-CharacterSaveLoadPlayMode.xml` 为真实角色槽位保存 / 读取 `1/1`；`Logs/TestResults-Module8.3-AllGameplayPlayMode.xml` 为 Gameplay PlayMode 全量 `29/29`。脚本编译无错误，未修改 EX-GAS 源码。
- 模块 8.3 完成。下一步继续 8.4 模板等价存档 UI：动态列表、删除确认、清空全部、局内保存返回标题；继续复用 GameCore SaveSystem / SaveKit 和 UIKit，不新增第二套槽位、文件或运行状态。

## 2026-08-13：模块 8.4 模板等价存档 UI 完成

- `ScenarioSavePanel` 和 `ScenarioSaveSlotView` 直接读取 GameCore `SaveSystem` 的有效槽位元数据；支持新建、覆盖、读取、单槽删除、清空全部、关闭和保存并退出。UI 不保存槽位字典、不扫描目录、不维护第二套文件入口。
- 删除确认复用 UIKit 的 `UIDialogPanel` 队列；新增 `GameCore.ConfirmationDialogPanel` 只是 UIKit 的项目皮肤，不复制模板 `ModalWindow` 的业务单例或回调管理。
- 保存并退出调用 `ScenarioDirector.EndScenarioAsync()`，按单局进入前记录的外层场景返回；没有硬编码标题场景。当前 Foundation 无外层场景时验证的是“保存成功并结束单局”，不是正式标题页视觉。
- `ScenarioSavePanelPlayModeTests` `2/2` 通过，覆盖动态列表、覆盖不新增、读取替换单局、删除确认、清空确认和保存并退出；Gameplay EditMode 全量 `124/124`，Gameplay PlayMode 全量 `31/31`。
- 资源句柄通用生命周期同时修复：主动释放导致的 YooAsset 实例化取消不再被完成回调误报，真实失败仍抛出。
- 当前功能测试是在 Unity batchmode 中通过，`-nographics` 不会生成 `ScreenCapture` 文件；因此 8.4 的真实 GameView 截图审计仍未完成，不能把功能测试当作视觉通过。截图门禁补齐后，模块 8 才完全关闭；下一步仍不进入模块 9。

## 2026-08-13：模块 8.4 真实 GameView 截图与 EX-GAS/UE GAS 复核

- 已通过项目内 AIBridge 的正式文件队列打开 `Assets/Scenes/FoundationTest.unity` 并进入 PlayMode；编辑器状态返回 `isPlaying=true`、`isCompiling=false`。没有使用 Unity batchmode 结果冒充 GameView。
- 真实截图 `Assets/Screenshots/Module84-SavePanel-GameView.png` 已生成并完成图面检查：存档窗口、空列表、标题、关闭、新建、清空全部、保存并退出均可见；窗口没有遮挡底部时间 HUD。该图只证明空列表状态 PASS，不代表全部存档 UI 状态通过。
- 尝试生成“已有槽位列表”第二张图时，AIBridge 命令本身返回成功但没有形成可验证 PNG 文件；因此没有把该命令结果当作截图证据，也没有修改截图链路或新增防重逻辑。带存档列表和删除确认仍是模块 8.4 的视觉未完成项。
- 复核 EX-GAS 2.0.4 与 UE GAS 职责后，当前实现不需要改第三方源码：`AbilitySystemCell` 继续负责运行时属性、Ability、GameplayEffect 和 GameplayTag 聚合；角色长期状态由角色快照保存并用当前 ASC 预设重建。没有发现项目侧复制 ASC、Tag、Effect 或从 ECS Buffer 旁路读取的同类实现。
- 当前仍不得关闭模块 8，也不得进入模块 9；先补齐带槽位列表和删除确认的真实 GameView 证据。

## 2026-08-13：模块 8.4 视觉门禁关闭

- 重新进入干净的 `FoundationTest` PlayMode，按现有端到端测试顺序执行“创建正式槽位 → 打开存档窗口 → 点击名为 `Delete` 的真实按钮 → 等待 `ConfirmationDialogPanel` 激活”，没有直接调用删除 API，也没有新增测试专用运行入口。
- 已有槽位状态 `Assets/Screenshots/Module84-SavePanel-Focused.png` 显示槽位编号、剧本摘要、保存时间、覆盖和删除操作；空列表状态继续由 `Assets/Screenshots/Module84-SavePanel-GameView.png` 覆盖。
- 删除确认状态 `Assets/Screenshots/Module84-SavePanel-DeleteConfirm-Final.png` 显示删除标题、不可逆提示、删除与取消按钮，并保持底层存档窗口和时间 HUD 层级清楚。三张图均无文字裁切、控件重叠或调试信息泄漏，功能性视觉审计通过。
- 前面的同步捕获全黑图和未显示弹窗的候选图已删除，不进入验收证据。模块 8 当前地基范围关闭；下一步才可进入模块 9 作者工具与关卡编辑支撑，仍不得提前实现原创荒岛业务。

## 2026-08-13：模块 9 作者工具与关卡编辑支撑

- 对照 StackCraft 全部 Editor 脚本后确认：模板没有独立关卡编辑器，实际作者能力是各类 SO Inspector、`RecipeDefinitionEditor` 的同材料提示、`StackingRulesMatrixEditor` 的枚举矩阵和 Console / Selection 定位。
- 9.1 复用现有正式内容校验入口，不新增校验窗口。`ContentValidator` 已覆盖唯一 ID、EX-GAS 标签、跨内容类型、行动槽位 / 结果、任务循环与剧本组成；每条问题携带 Unity 对象上下文，可由 Console 直接定位资产。本轮在当前 Unity 中扫描 11 个正式作者资产，结果为零错误、零警告。
- StackCraft 的同材料签名不适用于当前行动模型：同参与条件的多个行动必须作为玩家选项保留；随机概率只属于单个行动内部结果分支。现有校验器继续拒绝无效权重、断裂引用和重复隐藏 key，不恢复 `ACTION_CONDITION_SIGNATURE_SHARED` 或第二套配方规则。
- 9.2 保留现有 SO 作者入口：剧本只组合地区、任务、时间和阵型；地区唯一拥有 YooAsset 场景地址、牌桌放置规则和抵达位置。类型受限 `ContentIdReferenceDrawer` 与 GameCore `SceneAddressSelector` 已避免手填内容 ID 和场景地址，不新增重复 `ScenarioEditorWindow`。
- 9.3 发现地区内嵌牌桌规则缺少作者可读字段名，已在 `TabletopCardPlacementDefinition` 为牌桌边界、禁放区域、卡牌尺寸和堆叠步进补齐中文 Odin 标签；序列化字段、数据格式和运行规则均未改变。StackCraft 的分类堆叠矩阵依赖已排除的 `CardCategory`，不吸收。
- 9.4 现有扩展边界成立：YooAsset 过滤器收集所有 `ContentAsset` 派生资产；派生内容、行动结果和任务子项各自拥有校验入口，任务子项同时拥有运行状态与快照入口；中央索引没有按具体 Mod 类型 `switch`。本结论只覆盖代码扩展边界，不代表正式 Mod 包协议、游戏内关卡编辑器、构建发布或创意工坊已经实现。
- 修改后重新在 Unity 中执行全内容校验，仍为 11 个资产零错误零警告；编辑器日志没有脚本编译错误。AIBridge Test Runner 的程序集和测试类过滤均错误返回“找不到测试”，与现有 Unity XML 中 `Gameplay.EditModeTests.dll` 的 124 条测试证据矛盾，因此该工具结果只登记为测试调用限制，不作为实现失败或通过证据。

## 2026-08-13：阶段 C 当时选择范围关闭（已被 2026-08-14 完整效果审计取代）

- 对照模板 `QuestsView` 与 `RecipesView` 补齐最后的玩家可见缺口。没有新增任务系统或恢复独立 Recipe 系统；`ScenarioJournalPanel` 只读当前 `ScenarioRun`，任务页投影 `QuestLog`，已发现配方 / 行动页投影同一单局发现集合。
- `QuestLog.Quests` 按剧本作者声明顺序提供稳定只读任务对象；任务子项进度发生变化时直接通过 YokiFrame `EventKit` 发布 `QuestProgressChangedEvent`。`ScenarioRun.GetDiscoveredActions()` 只返回已发现 `ActionDefinition` 并按唯一内容 ID 稳定排序，成功发现后发布 `ContentDiscoveredEvent`。两种事件都只通知投影，不保存第二份状态。
- 初版截图正文过小且固定文本区无法承载增长内容，视觉审计判 `REVISE`。生成源已改为更大字号和 `ScrollRect + ContentSizeFitter`，重建同一预制体后重新运行、重拍、重审；旧截图不进入最终证据。
- 新鲜全量 EditMode 为 `452` 条：`451` 通过、`0` 失败、`1` 条环境条件跳过。返工后全量 PlayMode `42/42` 通过；`ScenarioJournalPanelPlayModeTests` `1/1` 覆盖任务初始进度、任务完成刷新、只显示已发现配方 / 行动、新行动发现刷新、标签切换与关闭。
- 最终真实 GameView：`Assets/Screenshots/ModuleC-Journal-Quests-Final.png`、`Assets/Screenshots/ModuleC-Journal-Actions-Final.png`。两页文字无裁切，内容区可滚动，关闭 / 标签入口清楚，底部日程 HUD 保留且无遮挡，功能地基视觉审计 `PASS`。测试皮肤和临时卡牌素材不代表正式美术。
- 阶段 C 三分类清单已收口：当前选择吸收功能均由新框架复现；模板经济、交易、固定枚举体系、旧战斗和 Manager 链等有明确排除理由；没有阻止 StackCraft 当前选择吸收范围通过的缺失项。正式主菜单、原创业务、游戏内编辑器、Mod 包协议与联机仍未实现，下一步只进入模块 10 边界审查。
# 2026-08-13：模块 10.1 内容包依赖与版本完成

- Mod 身份改为稳定 `modId`；启停状态不再使用会随版本变化的组合名称，删除 `FullName/fullName` 和 `loadOrder` 语义。删除 Mod 清单手填哈希，改为加载 YooAsset 官方 `.hash` 构建产物。
- `ModInfo.dependencies` 声明依赖 Mod ID 和包含式版本范围；`ModDependencyResolver` 统一验证缺失依赖、禁用依赖、版本不兼容、循环、重复 Mod ID、重复 YooAsset `packageName`、无效版本和缺少内容哈希，并输出依赖优先、同层按 `modId` 排序的确定顺序。
- `ModLoader` 改为先发现、统一解析、再加载；加载中途失败反向卸载本轮包。`ResourceSystem` 删除包加载优先级参数，包地址 / 资源路径 / 场景定位出现多包命中时直接报错，不静默覆盖。
- `ModAPI.CreateActivePackageSetSnapshot()` 只记录当前启用且已加载的 Mod：稳定 ID、语义版本、YooAsset 官方包哈希和生效清单版本。`ScenarioRunSnapshot` 冻结该集合，恢复前严格比较，版本、哈希、清单版本、缺失或额外 Mod 任一不同都会拒绝。
- 测试：Mod 依赖定向 `8/8`；包快照定向 `6` 个逻辑用例通过；ScenarioRun 定向 `14/14`；GameCore EditMode `111/111`；Gameplay EditMode `256/256`；Gameplay PlayMode `32/32`；GameCore PlayMode `9/9`；`spec-lint passed`。AIBridge 的 PlayMode 延迟请求只返回 Processing，最终以 Unity `TestResults.xml` 为准。
- 未实现：网络后端、RPC、联机权限 / 可见性协议、创意工坊、游戏内 Mod 编辑器和资源覆盖优先级。下一步先审查 10.2 的权限 / 命令 / 可见性职责，仍不提前实现具体联机后端。

## 2026-08-13：模块 10.4c Mod 删除事务订正

- 修复启动时先删除磁盘目录、后验证依赖的错误顺序。加载器现在先发现全部清单并解析启用依赖闭包，再预检全部待删除路径；只有所有前置条件成立才开始删除，每个配置状态只在对应目录删除成功后消费。
- 越界目录、缺失目录和无效删除不再只写日志后伪装成功，而是直接抛出明确异常并保留目录与待删除状态。公开磁盘删除旁路和 `force` 状态抹除旁路已删除。
- 运行中启停状态明确为下次启动意图。标记删除不再把仍加载的 Mod 从本次运行清单移除；单局 Mod 包快照改为查询 `ResourceSystem` 的当前已加载包事实，避免配置意图与运行事实形成两套真相。
- 新增加载器级回归覆盖：启用依赖阻止删除且目录不变、任一路径越界时所有目录与状态不变、成功删除后才消费状态、运行中标记删除仍保留当前清单。`GameCore` 与 `GameCore.EditModeTests` 已按依赖顺序用 Unity 响应文件独立编译通过，`spec-lint passed`。
- 用户已重新打开 Unity，当前 CardLoop 主窗口正常响应，`FoundationTest` 已打开，Unity 生成的 `GameCore` 与 `GameCore.EditModeTests` 程序集时间已更新，当前编辑器日志没有脚本编译错误。本环境没有暴露 Unity Test Runner 或项目 AIBridge CLI 调用入口，因此新增删除事务用例仍没有新鲜运行结果，不能声称测试已经通过。

## 2026-08-13：模块 10.2 权限与可见性复审收口

- 重新横向搜索 Gameplay 后，当前没有玩家 ID、网络所有权、客户端权威、可见性副本或牌桌权限表等已写死实现；输入只提交拖拽意图，`ScenarioRun` 与 `Tabletop` 仍是正式状态写入口。因此没有需要现在迁移或删除的假联机层。
- 当前产品资料仍未定义玩家席位、每个席位可控制的角色集合、队长授权变化、陌生人与友好单位的控制关系，以及叛徒秘密信息具体归属。缺少这些领域事实时新增权限对象、可见性过滤或可序列化网络请求，会把猜测固化为第二套真相。
- 结论保持不实施：不安装 FishNet，不新增 `PlayerId`、权限表、RPC、同步副本或可见性缓存。等上述玩法事实形成后，以 `ScenarioRun`、`Tabletop`、角色卡和玩家席位这些领域对象接入 FishNet；旧 `GameCommandContext.RemotePlayer` 继续只属于 GameCore 2D 实体命令链，不跨接牌桌。

## 2026-08-13：Mod 删除中断恢复补齐

- 横向复审发现真实 I/O 失败路径：目录删除成功后，如果后续资源加载、配置保存或进程退出中断，磁盘已删除但配置文件仍可能保留待删除状态。
- 没有新增事务表或影子状态。加载器扫描完成后，以本次实际发现的稳定 Mod ID 集合核对配置：只有状态为删除且安装目录已不存在的记录会被消费；普通启用或禁用的暂时缺失 Mod 状态继续保留。
- 新增回归覆盖“上次删除已落盘但配置未保存”的恢复语义。`GameCore` 与 `GameCore.EditModeTests` 再次按程序集依赖顺序独立编译通过；警告均为既有 Unity 过时 API / 序列化分析警告。

## 2026-08-14：StackCraft 完整效果补充审计与战斗增援

- 用户明确订正验收目标：UI 不需要复刻 StackCraft 外观，后续以新框架实现相同玩家可见游戏效果、规则结算和状态变化。完整读取模板 41 页文档后，阶段 C 从“当前选择范围通过”重新打开为补充效果审计，避免旧选择清单漏掉通用能力。
- 源码对证确认第一个真实缺口：`Battle` 只有创建和移除参与者，没有把角色加入既有战斗方的正式命令；StackCraft 的 `CombatTask.AddCombatants` / 战斗合并具备增援效果。
- 通过 RED 行为测试确认 `Tabletop.JoinBattle` 缺失后，在现有牌桌唯一写入口补齐。调用方负责按剧本 / 阵营规则选择战斗方；牌桌校验卡牌归属、角色类型、战斗方和重复参战，`Battle` 只提交成员变化，不复制 GAS 阵营，不新增战斗管理器。
- 统一 `FoundationTest` 的战斗用例升级为“开战 -> 指定战斗方增援 -> 阵型立即更新 -> 敌方主动离开 -> 战斗结束 -> 所有卡牌恢复牌堆表现”。UI 只验证效果可读，不比较模板布局和皮肤。
- 新鲜验证：`BattleEditModeTests` `10/10` 通过；增援单条 PlayMode `1/1` 通过；完整 `FoundationTestScenePlayModeTests` `21/21` 通过；Unity 编译完成且 Console 无脚本错误。
- 行动材料 `Keep / Destroy / Consume` 已完成对证和实现。`Keep` 由不声明修改结果表达，`Destroy` 继续使用 `RemoveCardsResultIntent`，`Consume` 使用新的 `UseCardsResultIntent`；没有新增三态枚举或第二套配方系统。
- `CardDefinition` 只增加所有卡牌实例共有的初始使用次数，默认 `1`；`TabletopCard` 直接拥有剩余次数。最后一次使用由 `Tabletop.UseCard` 走正式移除链，其余使用只递减同一状态，不建立耐久组件、管理器、GAS 属性或影子状态。
- 行动开始时把使用目标冻结进 `ActionResultPlan`，活动行动快照保存该计划；牌桌快照保存每张卡的剩余次数。产物空间预检会把本次耗尽卡牌计入最终移除集合，避免狭窄牌桌误报空间不足。剩余次数的正式卡面 / 详情表现等待 CardLoop UI 设计；不照搬旧 UI 结构，但若模板剩余次数表面承载玩家效果，仍需进入表面 / 动画审计。
- 新鲜验证：行动结算 `13/13`、牌桌卡牌 `11/11`、行动实例 `16/16`、剧本快照 `14/14`、统一 Foundation `21/21`；全量 EditMode `491/492` 通过、`0` 失败、`1` 条环境条件跳过；全量 PlayMode `45/45` 通过。
- 全量 EditMode 首轮另外暴露 3 条此前未实际运行的 Mod 配置测试失败。两条是测试未规范化 Windows 路径，一条是实现吞掉了“重复 Mod 状态”的具体原因；测试和错误消息分别订正后，Mod 配置定向 `11/11`、全量 EditMode 如上转绿。
- 下一项继续对证 StackCraft 的完整日结阶段：先判断当前 `ScenarioRun.ConfirmTurn` 是否只有时间推进事实，还是已经具备按剧本扩展“日终规则 -> 新日 -> 自动保存”的正式执行入口。
- 日结审计订正：当前单一 `AdvanceWorldTurn` 只覆盖行动推进、跨日、按天任务刷新和回合事实，不能证明模板完整日终。临时策划不是排除模板进食、超限处理、遭遇、新日确认或自动保存的真相；后续必须用新框架复现该顺序供试玩，同时不恢复空 `DayCycleManager`、事件壳或写死数值。
- 下一项转入战斗实时调度审计：核对当前手动 Ability 激活链与模板自动攻击进度、目标选择、和平生产暂停之间的差距，只有已有 GAS 作者配置和明确战斗规则能支撑的部分才实施。
- 战斗实时调度审计结果：当前参战、阵型、增援 / 离开、权威随机和 EX-GAS Ability 激活链成立，但没有角色默认自动 Ability、玩家 / NPC 控制策略、目标选择或和平行动暂停规则。测试常量 `XAbility.ABILITY_Attack` 不能升格为所有角色正式普攻，模板随机目标也不直接吸收；实时自动战斗登记为阶段 C 缺口，等待角色战斗指令 / AI 策略作者源形成后由 `Battle` 持有进度并调用现有 GAS 链。
- 行动 / 战斗占用审计发现现有公开入口允许同一角色同时参与普通行动和活动战斗。没有新增占用表或角色忙碌状态；`Tabletop.StartBattle`、`JoinBattle` 与行动启动入口直接读取现有活动行动 / 战斗集合，冲突时抛出明确错误，调用方必须先完成、取消或离开。战斗合同 `11/11`、行动实例 `16/16`、统一 Foundation `21/21` 通过。
- StackCraft 的拖拽暂停 / 放回恢复依赖“拖拽开始即拆堆”的旧实现。Gameplay 拖拽期间只移动视图并形成释放意图，不修改牌桌权威状态，因此不复制临时暂停字段；参与卡真正被移除时，现有链会在下一次推进前以 `ParticipantInvalidated` 取消且不结算结果。
- 特殊配方复审订正：Growth 可由保留 / 使用 / 生成结果组合表达；Exploration 可由地点参与条件、权威随机分支和生成结果表达，不恢复专用 Recipe 子类。Research 按模板效果接入通用行动结果：作者声明候选行动及对应配方卡，完成时从 `ScenarioRun` 尚未发现集合中使用牌桌权威随机流选择并写回唯一发现事实。模板其它自动发现入口继续按实际效果验证，不再以临时蓝图设想直接排除。
- 当时用户锁定阶段 C 口径：当前先复现 StackCraft 游戏效果用于实验，不能让《卡牌生存：无限》临时策划抢模板吸收真相。2026-08-18 已补充纠偏：旧 UI 结构不照搬，但表面 / 动画如果承载模板玩家效果，必须专项对账。
- 本轮最终全量 EditMode `492/493` 通过、`0` 失败、`1` 条环境条件跳过；最终全量 PlayMode `45/45` 通过。PlayMode 首轮曾出现一次真实鼠标点击行动候选超时并级联留下两个面板状态失败；原失败用例单独重跑 `1/1`、完整套件重跑 `45/45`，当前未复现，也没有证据声称根因已修复，继续作为测试输入稳定性观察项。

## 2026-08-14：模板效果口径订正与 Research 随机解锁

- 用户锁定阶段 C 的真相来源是 StackCraft 实际源码与玩家效果；《卡牌生存：无限》当前策划只提供扩展性约束，不能用于提前排除 Research、完整日终、自动保存或其它模板效果。2026-08-18 已补充纠偏：旧 UI 结构不照搬，但卡面、反馈和动画等玩家可见效果必须专项对账。
- Research 没有恢复 `ResearchRecipe` 子类、全局配方扫描或 `ResearchManager`。行动结果作者源显式声明“待解锁行动 + 对应配方卡”；行动开始冻结候选池和生成锚点，完成时根据 `ScenarioRun` 唯一发现集合过滤，再使用牌桌权威随机流选择。
- 成功结算先完成空间预检，再生成配方卡并把对应行动写入 `ScenarioRun`；全部候选已发现时，行动仍完成但不重复生成。活动行动快照保存冻结候选，作者资产后续变化不会改写运行中行动。
- Research 行为单条 `1/1`、行动结算 `14/14`、行动实例 `16/16`、剧本单局 `14/14`、战斗合同 `11/11`；全量 EditMode `493/494`（零失败、1 条环境条件跳过），全量 PlayMode `45/45`。
- `spec-lint` 当前仍被工作区既有规范宿主问题阻塞：`.agents/skills` 不是要求的 symlink、`.claude/skills` 与 `.claude/agents` 未登记、`.codex/skills` 仍有索引项、两个迁入 skill 缺 frontmatter。本轮未修改这些无关入口，不能声称规范检查通过。
- 下一项按 StackCraft `DayCycleManager` 的真实顺序复现完整日终，再接跨日自动保存；不以临时策划缺少最终数值为由延期。

## 2026-08-14：StackCraft 完整日终闭环

- 日终继续由 `ScenarioRun` 所属单局持有，没有恢复 `DayCycleManager`、交易管理器或第二套经济状态。日终阶段依次等待进食确认、处理超限卡牌、反馈遭遇和确认新日；新日自动保存继续复用 `ScenarioDirector` 与 `SaveSystem`。
- 食物恢复生命正式使用 EX-GAS GameplayEffect `2005`。恢复量在日终规则中计算后写入 `GameplayEffectSpec`，由 GAS 提交生命属性变化；测试角色在正式 PlayMode 中从 `20` 恢复到 `70`，没有直接写生命值。
- 售卡使用零回合行动和现有行动结果结算：行动开始冻结被售卡、售价与货币生成计划，完成时由牌桌原子移除卡牌并生成货币。货币通过 `CountsTowardCardLimit = false` 不占卡牌上限；设施仍可通过 `CardLimitBonus` 提高上限，没有额外布尔开关或平行容量表。
- 超限阶段在售卡行动成功结算后自动检查实际牌桌数量；超限归零时进入遭遇与新日确认。遭遇结果保存在当前日终运行对象中，并由现有回合 HUD 显示“遭遇：夜间来客 x1”，开始新日后清空。
- 统一 `FoundationTest` 真实链路已经覆盖：切换日终测试剧本、创建受伤角色和食物、确认进食、拖拽待售卡到收购点、通过现有行动选择 UI 售卡、生成 2 枚货币、生成遭遇卡、显示 HUD 摘要、确认第 2 天并自动保存。定向 PlayMode 在 HUD 布局返工后重新通过 `1/1`。
- 全量验证为 EditMode `500/501`（零失败、1 条环境条件跳过）和 PlayMode `46/46`。过程诊断截图曾发现两行遭遇摘要与进度条、按钮重叠，已经在 `FoundationTestSceneMenu` 正式生成源中扩充 HUD 高度并重建预制体；该修改只保证测试入口和必要反馈可读，不构成正式视觉验收，也不作为日终模块完成门禁。
- 收口复核：Unity Console 当前 `0` 条错误，未再出现 `Renderer2D is missing RendererFeatures` 警告；本轮源码与文档 `git diff --check` 通过，HUD 预制体只保留 Unity 对空 `m_Name` 字段的既有尾空格格式，忽略行尾空格后的实际差异只有 4 处预期布局参数；一次性 EX-GAS 作者命令不存在；`spec-lint` 及其测试 `2/2` 均通过。

## 2026-08-14：地基阶段视觉验收门禁订正

- 用户明确裁决：视觉验收是整体阶段或产品交付的完成宣告，不是会持续变化的地基模块过程门禁。地基过程只验证正式功能链、规则结果、状态变化、可操作入口和必要反馈。
- 最初误把跨项目通用问题写成 CardLoop 项目硬规则与项目工作流覆盖；现已删除这两份重复正文，改为修正系统 AGENTS 的触发路由和系统 `ui-audit-loop` 的唯一方法正文。
- 权威计划和 StackCraft 矩阵中的旧 GameView 门禁已降级为历史诊断证据。旧进度日志保留当时实际发生过的截图与返工记录，但不再代表现行推进方式。

## 2026-08-14：StackCraft 两场战斗合并效果吸收

- 对证 StackCraft `CombatManager.CheckAndMergeCombats / MergeCombats` 后，保留“两场独立战斗变为一场并重新排阵”的玩家效果，排除矩形碰撞总管、固定 Player / Mob 重分组和销毁旧战斗后创建第三场的实现方式。
- `Tabletop.MergeBattles` 由牌桌唯一拥有活动战斗关系：调用方明确提交来源方到目标方的映射；所有映射和参与关系先校验，随后来源成员按原顺序加入目标，来源战斗结束并移除。目标战斗对象、ID 和权威随机流不变，没有新增 Manager、事件包装、阵营缓存或第二套状态。
- 统一 `FoundationTest` 原战斗增援用例升级为真实组合验收：创建两场 1 对 1 战斗，合并为一场 2 对 2 战斗，四张卡只投影一套阵型，结束后全部恢复原牌堆表现。原 `JoinBattle` 行为继续由领域合同覆盖。
- 有效 RED 是 Unity 编译明确缺少 `MergeBattles`；第一次 Test Runner `0` 条是编辑器仍处于 Play Mode 导致引导场景创建失败，已排除为无效证据。最终合并合同各 `1/1`，战斗全组 `13/13`，阵型 `3/3`，合并 PlayMode `1/1`，Foundation `22/22`，全量 EditMode `502/503`（零失败、1 条环境条件跳过），全量 PlayMode `46/46`。

### 自动区域触发订正

- 上述旧结论只完成了显式合并命令，不能证明 StackCraft“战斗区域重叠后自动合并”的完整玩家效果；用户指出后重新打开该项，验收范围补回触发条件、可见区域、合并结果和区域清理。
- `Battle` 现在拥有唯一权威区域中心，区域尺寸由当前战斗方、参战人数、卡牌尺寸和阵型边距派生。`Tabletop` 在新战斗创建前检查潜在区域，也在参战者加入导致区域扩张前检查；重叠后按战斗方索引确定性并入保留战斗，显式 `MergeBattles` 继续服务剧情和特殊规则。
- `TabletopBattleAreaView` 只读显示派生区域，不参与碰撞判断或保存第二份状态。统一 `FoundationTest` 真实显示两个区域，连续增援使其中一个区域扩张并与另一块重叠，随后自动合并为一个区域；战斗结束后区域视图清空，卡牌恢复原牌堆表现。
- 两条自动触发合同分别从 `0/1` RED 转为 `1/1` GREEN；最终战斗合同 `15/15`、阵型 `3/3`、完整 Foundation `22/22`、全量 EditMode `504/505`（零失败、1 条环境条件跳过）、全量 PlayMode `46/46`。

## 2026-08-14：StackCraft 实时自动战斗效果吸收

- 对证 `CombatTask.Update` 后，吸收“所有参战者按攻速持续积累进度、每秒由进度最高者随机攻击对方、攻击完成后重置该角色进度、生命归零后离场并结束战斗”的玩家效果；不吸收 `CombatManager`、全局协程宿主、直接伤害、Unity 全局随机或固定 Player / Mob 结构。
- `CharacterCardDefinition` 新增必要的“自动战斗 Ability”引用，只能指向该角色 ASC 已授予的 EX-GAS Ability；0 表示不自动行动。它不复制 Ability 配置、Timeline、Cost、Cooldown 或伤害数据。测试角色正式引用 `TabletopBasicAttack(20005)`。
- `Battle` 直接拥有每名参战者的行动进度、每秒选择窗口和当前自动 Ability 生命周期；`Tabletop` 读取唯一 ASC 的 `AttackSpeed`，用战斗权威随机选择其它战斗方目标，并继续调用现有 Ability 激活入口。Ability 的结束 / 取消回调重置进度，没有第二份技能时长。
- `ScenarioRun` 在平时回合制下仍把真实秒数交给活动战斗；普通牌桌行动是否按秒推进仍由原 `ProgressionMode` 决定。由此落实“战斗始终即时，平时默认回合制且可切即时制”，没有增加第二份时间配置。
- 严格 RED 为自动攻击单条 `0/1`，现实症状是等待 5 秒目标生命仍未变化；GREEN 为 `1/1`。统一 Foundation `23/23` 进一步验证攻速较高者先行动、正式 GAS 伤害、生命归零后目标卡移除和战斗结束。定向战斗 `15/15`、剧本单局 `19/19`，全量 EditMode `504/505`（零失败、1 条环境条件跳过），全量 PlayMode `47/47`。

## 2026-08-14：StackCraft 卡包逐槽打开效果吸收

- 新增 `CardPackDefinition` 卡牌子类：按顺序保存抽取槽位，每槽配置普通卡权重和可选未发现配方；使用次数由槽位数量自动派生，不要求作者同步填写。
- 新增 `OpenCardPackResultIntent`，但没有新增卡包管理器。点击后仍走新输入系统、行动候选、UIKit 选择与牌桌原子结算；权重、配方概率和候选选择全部使用牌桌权威随机，配方写回 `ScenarioRun` 既有发现集合。
- `ActionDefinition` 只增加显式“允许点击启动”作者开关。普通卡牌点击继续选择和显示详情，只有卡包打开行动参与点击候选；拖拽行动不受影响。
- RED 证据包括缺少卡包定义 / 结果意图的编译失败，以及真实点击后候选数为 0。GREEN 为卡包领域 `3/3`、玩家点击 `1/1`、行动结算 `15/15`、Foundation `24/24`。
- 最终全量 EditMode `507/508`，零失败、1 条环境条件跳过；全量 PlayMode `48/48`。下一子模块仍是卡包商贩的任务解锁、分批付费和购买，不提前扩展原创经济规则。

## 2026-08-14：StackCraft 卡包商贩购买效果吸收

- 对证 StackCraft `PackVendor`、`TradeZone`、`TradeManager` 和 `QuestManager` 的真实职责后，没有搬入全局交易管理器。售卖关系由 `PackVendorDefinition` 定义，牌桌实例由 `PackVendorCard : TabletopCard` 持有付款进度；价格属于售卖关系，不写入卡包商品定义。
- 新增 `PackVendorUnlockedCondition`，候选展示和 `ScenarioRun.StartAction` 都读取现有 `QuestLog.CompletedQuestCount` 与商贩门槛；没有保存解锁布尔值。新增 `PurchaseCardPackResultIntent`，由现有牌桌行动原子结算分批扣除货币，满价时生成出售卡包并清零付款。
- 购买任务复用现有 `QuestLog`，新增指定卡包购买事实和任务子项；没有新增 TradeManager、全局经济状态或第二事件入口。卡包收藏进度读取 `ScenarioRun` 内容发现集合，行动首次生成的普通卡 / 配方卡一并提交发现事实。
- 卡牌运行状态快照改为 `TabletopCardRuntimeStateSnapshot` 多态入口，角色能力状态和商贩付款状态由各自派生卡牌定义创建 / 恢复，牌桌不再中央硬编码角色卡类型。
- 统一 `FoundationTest` 新增真实玩家链：创建商贩和两枚货币 -> 拖拽货币到商贩 -> 通过行动选择面板提交两次 -> 第一次只保留付款进度 -> 第二次生成卡包并归零 -> 详情面板显示剩余价格和收藏进度。
- RED：Unity 编译明确缺少 `PackVendorDefinition`；GREEN：商贩领域 / 快照 / 任务 / 单局购买 `8/8`，行动结算回归 `15/15`，统一 Foundation 购买 `1/1`。最终全量 EditMode `515/516`（零失败、1 条既有环境条件跳过），全量 PlayMode `49/49`；`spec-lint` 通过，规范测试 `2/2`。Unity Console 无项目脚本错误；空角色测试过滤任务曾卡在启动，已取消，不计入验收。

## 2026-08-15：StackCraft 箱子存币与付款效果吸收

- 箱子按 StackCraft 玩家效果吸收为 `ChestCardDefinition` / `ChestCard`：箱子是牌桌卡牌派生对象，只拥有本局存币数量与容量，不扩展为通用库存、背包或局外仓库。
- 存币、取币和用箱子付款都走现有行动候选、UIKit 行动选择和牌桌原子结算。存币移除货币卡并增加箱子存币；取币从箱子生成货币卡；购买卡包时箱子可作为付款来源且不会被移除。
- 卡牌详情面板只读展示“存币：当前/容量”。它读取 `ChestCard` 当前状态，不保存第二份 UI 状态，也不引入交易管理器或经济系统。
- 候选条件订正：商贩 / 箱子条件遇到“必需槽位还没填或对象不匹配”返回不可用；只有引用了不存在的牌桌卡才抛出异常。这样保持填槽和拖拽候选可探测，同时不让无关购买行动在货币拖向箱子时污染玩家链。
- 新鲜验证：`ChestCardEditModeTests` `4/4`、`PackVendorEditModeTests` `8/8`、新增 Foundation 玩家链 `1/1`、完整 `FoundationTestScenePlayModeTests` `26/26` 通过。源码定向 `git diff --check` 通过；Unity 生成器已重建箱子、存币行动、取币行动和 Foundation 测试场景。

## 2026-08-15：StackCraft 装备效果吸收

- 对证 StackCraft 装备玩家效果后，吸收“装备卡离开牌桌、占用角色槽位、同槽替换、卸下回桌、装备效果影响角色”的结果；不迁入 `CardEquipper`、`CardEquipment`、旧装备面板或 `StatModifier` 结构。
- 新增 `EquipmentSlotDefinition` 和 `EquipmentCardDefinition`：装备槽位是正式内容 ID，可由内容包扩展，不使用枚举；装备后的属性、标签和持续效果只引用 EX-GAS `GameplayEffect`，卸装和替换通过 GAS 正式移除入口撤销。
- `CharacterCard` 直接拥有装备状态和装备快照；`Tabletop` / `TabletopCards` 支持装备卡离桌、替换旧装备回桌、卸下回桌，以及从完整单局快照恢复后重新施加装备 GameplayEffect。
- 装备 / 卸装仍通过现有行动候选、行动计划、结算和快照链执行，没有新增装备管理器、事件包装、第二套角色属性或第二套资源加载入口。
- 新鲜验证：Unity 编译完成且 Console `0` 错误；`EquipmentCardEditModeTests` `4/4`、`ActionResultSettlementEditModeTests` `15/15`、`ScenarioRunEditModeTests` `19/19`、`ChestCardEditModeTests` `4/4`、`PackVendorEditModeTests` `8/8`、完整 `FoundationTestScenePlayModeTests` `26/26` 通过。阶段 C 仍是补充审计中，不能据此宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft 装备任务进度事实吸收

- 对证 StackCraft `QuestType.Equip` 和 `QuestManager.HandleCardEquipped` 后，吸收“装备指定装备卡后推进对应任务”的玩家效果；不恢复 `QuestManager`、`QuestType` 枚举、全局装备事件链或装备专用任务管理器。
- 新增 `CardEquippedQuestTaskFact` 和 `CardEquipQuestTaskDefinition`。装备行动成功提交后，`ActionResultSettlement` 返回已装备内容 ID，`ScenarioRun` 把该事实交给当前单局 `QuestLog`，由任务子项自己累计进度。
- 装备任务只引用正式 `EquipmentCardDefinition` 内容 ID，并支持次数目标与任务快照恢复；校验错误仍定位到作者任务资产，不建立第二套内容 ID、事件总线或卡牌类型枚举。
- 新鲜验证：Unity 编译完成且 Console `0` 错误；`EquipmentCardEditModeTests` `5/5`、`QuestLogEditModeTests` `11/11`、`ScenarioRunEditModeTests` `19/19`、`ActionResultSettlementEditModeTests` `15/15`、完整 `FoundationTestScenePlayModeTests` `26/26` 通过。阶段 C 仍是补充审计中，不能据此宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft 生成卡牌任务事实吸收

- 对证 StackCraft `QuestType.Obtain` / `QuestType.Craft`、`CardManager.OnCardCreated` 和 `CraftingManager.OnCraftingFinished` 后，吸收“行动成功产出指定卡牌后推进对应任务”的玩家效果；不恢复 `QuestManager`、固定 `QuestType` 枚举、全局卡牌创建事件或制作完成单例。
- 新增 `CardsCreatedQuestTaskFact` 和 `CardCreationQuestTaskDefinition`。行动结算真正创建卡牌后，`ActionResultSettlement` 返回本次创建的内容 ID；`ScenarioRun` 只在行动成功提交后把事实交给当前单局 `QuestLog`，由任务子项统计指定产物数量。
- 该事实当前只覆盖行动结果提交产生的卡牌，等价承接模板 Craft 产物和通过行动表达的 Obtain 产物；购买、遭遇、日终、手动创建等其它来源不会被全局混算，后续若需要作为任务事实必须按来源单独裁决。
- 新鲜验证：Unity 编译状态空闲；`QuestLogEditModeTests` `11/11`、`ScenarioRunEditModeTests` `19/19`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity Console `0` 错误。阶段 C 仍是补充审计中，不能据此宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft 击败卡牌任务事实吸收

- 对证 StackCraft `QuestType.Defeat` 和 `QuestManager.HandleCardKilled` 后，吸收“战斗中击败指定卡牌后推进对应任务”的玩家效果；不恢复 `QuestManager`、固定 `QuestType` 枚举、全局死亡事件或把出售 / 旅行 / 普通移除误算为击败。
- 新增 `CardsDefeatedQuestTaskFact` 和 `CardDefeatQuestTaskDefinition`。`Tabletop` 只在战斗死亡清理正式移除角色卡后提交被击败卡牌内容 ID，`ScenarioRun` 把该事实交给当前单局 `QuestLog`，由任务子项按指定卡牌内容 ID 和数量累计。
- `ScenarioRegion` 和 `Tabletop` 的构造入口现在显式要求击败回调，没有可选空兜底；这样击败事实必须接回单局剧本，不会变成静默丢失的第二事件链。
- 新鲜验证：`QuestLogEditModeTests` `11/11`、`ScenarioRunEditModeTests` `19/19`、`BattleEditModeTests` `15/15`、`BattleFormationEditModeTests` `3/3`、`ActionCandidateEditModeTests` `8/8`、`ActionInstanceEditModeTests` `16/16`、`TabletopCardsEditModeTests` `12/12`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity 编译空闲且 Console `0` 错误。阶段 C 仍是补充审计中，不能据此宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft 状态型任务事实吸收

- 对证 StackCraft `QuestType.Have / Food / Coins / Capacity` 和 `QuestManager.HandleStatsChanged` 后，吸收“当前牌桌状态达到拥有数量、总食物营养、货币数量或卡牌容量要求后推进任务”的玩家效果；不恢复 `StatsSnapshot` 管理器、`CardCategory.Currency` 枚举、全局统计事件或任务中央分支。
- 新增 `TabletopStateQuestTaskFact`，由 `ScenarioRun.RefreshQuestState` 从全部地区牌桌实时生成：重复记录卡牌内容 ID，统计食物卡剩余使用次数对应的总营养，读取箱内存币和牌桌货币卡，并按剧本日终基础容量加卡牌上限加成计算当前容量。
- 新增 `CardPossessionQuestTaskDefinition`、`FoodNutritionQuestTaskDefinition`、`CurrencyAmountQuestTaskDefinition` 和 `CardCapacityQuestTaskDefinition`。货币任务必须指定具体货币卡内容 ID，避免把“金币”写成全局真相；箱子存币通过 `ChestCardDefinition.CurrencyCardId` 归入同一货币。
- 这些任务是状态型进度：未完成前会随当前事实更新进度，完成后不回退；新激活的后继任务会在同一次 `RefreshQuestState` 循环里重新读取当前状态，不需要第二事件总线。
- RED 证据：新增测试首次编译失败，缺少 `TabletopStateQuestTaskFact` 和四个任务子项类型。GREEN 验证：`QuestLogEditModeTests.TabletopStateQuestTasks_SetProgressFromCurrentStateFact` `1/1`、`ScenarioRunEditModeTests.ActivateInitialQuests_EvaluatesCurrentTabletopStateTasks` `1/1`、既有 `QuestLogEditModeTests` `11/11`、既有 `ScenarioRunEditModeTests` `19/19` 通过；Unity 编译空闲且 Console `0` 错误。阶段 C 仍是补充审计中，不能据此宣称 StackCraft 完整吸收完成。

## 2026-08-15：Foundation PlayMode 存档隔离修复

- 复跑完整 `FoundationTestScenePlayModeTests` 时出现 `9/26` 失败，所有失败的现实症状都是新开剧本时报错“没有可用于新剧本单局的空存档槽位”，并非 UI 点击或装备任务链路失败。
- 根因是该测试类没有像存档面板和标题测试一样配置独立临时 SaveKit 目录，导致它默认读写真正 `Application.persistentDataPath` 下的历史运行槽位；槽位被填满后，任何新剧本启动都会失败。
- 修复：`FoundationTestScenePlayModeTests` 在 `UnitySetUp` 中配置每条用例独立临时存档目录，在 `UnityTearDown` 中重置 SaveKit 配置并删除本轮临时目录；没有清空用户真实持久数据，也没有修改生产存档逻辑。
- 测试规范已补充 PlayMode 持久状态隔离规则，避免 SaveSystem / SaveKit / persistentDataPath 类用例再次污染真实目录。
- 新鲜验证：修复后 Unity 编译完成且 Console `0` 错误，完整 `FoundationTestScenePlayModeTests` `26/26` 通过。

## 2026-08-15：StackCraft 探索与时间任务事实吸收

- 对证 StackCraft `QuestType.Explore` 后，吸收“指定区域 / 地点卡被探索后推进任务”的玩家效果；不恢复 `QuestManager`、`CraftingManager.NotifyExplorationFinished`、`ExplorationRecipe.Execute` 里的全局回调或固定 `QuestType` 枚举。
- 新增探索结果意图后，行动开始时从指定参与槽位冻结被探索卡牌内容 ID；行动成功提交后，`ScenarioRun` 才把 `CardsExploredQuestTaskFact` 交给当前 `QuestLog`。失败、取消和参与对象失效不会发布探索事实。
- 探索事实也写入 `ActionResultPlanSnapshot`。未完成探索行动读档时恢复冻结内容 ID，不重新读取可能已经变化的行动作者资产；旧快照没有该字段时按“没有探索事实”兼容读取。
- 对证 StackCraft `QuestType.Time` / `TimeManager.CycleTimePace` 后，不复制 `Paused / Normal / Fast` 速度枚举。CardLoop 正式语义是普通行动推进模式切换，因此吸收为 `ActionProgressionMode.TurnBased / RealTime` 切换事实；初始默认回合制和读档恢复不会自动完成该任务。
- 新鲜验证：Unity 编译空闲且 Console `0` 错误；`QuestLogEditModeTests` `16/16`、`ScenarioRunEditModeTests` `24/24`、`ActionResultSettlementEditModeTests` `15/15` 通过；`.spec` lint 通过，规范测试 `2/2` 通过。阶段 C 仍是补充审计中，不能据此宣称 StackCraft 完整吸收完成。

## 2026-08-15：标题入口友好模式与阶段 C 回归复验

- 标题入口测试场景的运行时根引用已由生成器保存后回读校验，避免 `FoundationTitleRuntimeEntry` 保存成空预制体引用。`FoundationTestSceneHarness` 现在等待回合 HUD 与卡牌详情 HUD 真实打开后才进入就绪，场景卸载导致的 UIKit 异步取消只按生命周期退出处理，不吞业务异常。
- `ScenarioTitleScreenPlayModeTests` 在本测试生命周期内临时开启 `Application.runInBackground`，结束后还原原值；这只解决 Unity 自动化失焦导致 PlayerLoop 停帧，不修改 ProjectSettings，也不把后台运行作为项目正式设置。
- 新鲜验证：标题入口 PlayMode `ScenarioTitleScreenPlayModeTests` `4/4` 通过，覆盖标题四个命令和友好模式开局；`ScenarioRunEditModeTests` `25/25`、`QuestLogEditModeTests` `16/16`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity 编译空闲且 Console `0` 错误。
- 阶段 C 仍是补充效果审计中：上述结果只证明标题入口、友好模式开局、探索 / 时间任务事实和核心回归链路当前成立，不能宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft Buy 任意卡包任务事实补齐

- 对证 StackCraft `QuestType.Buy` 后，确认模板购买任务支持两种玩家效果：指定卡包购买计数，以及目标为空时任意卡包购买计数。此前 Gameplay 只覆盖指定卡包，属于阶段 C 任务事实缺口。
- `CardPackPurchaseQuestTaskDefinition` 保持现有任务子项职责，不新增任务系统或交易总管。购买卡包目标 ID 留空时表示任意卡包；填写时仍只统计指定 `CardPackDefinition`。
- 作者源校验和运行时计数使用同一口径：空目标合法，非空目标必须能解析为卡包内容；`CardPackPurchasedQuestTaskFact` 继续只由正式购买结算提交。
- 新鲜验证：Unity 编译空闲且 Console `0` 错误；新增任意卡包运行时测试 `1/1`、新增作者源校验测试 `1/1`、`PackVendorEditModeTests` `10/10`、`QuestLogEditModeTests` `16/16`、`ScenarioRunEditModeTests` `25/25` 通过。阶段 C 仍是补充效果审计中，不能宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft 日终遭遇生成任务事实补齐

- 对证 StackCraft `QuestType.Obtain` 监听 `CardManager.OnCardCreated` 后，补齐“日终遭遇真实生成指定卡牌后推进 Obtain / Craft 对应任务”的玩家效果；不恢复全局卡牌创建事件，也不把测试夹具、读档恢复或手动布景的 `Tabletop.CreateCard` 混算为任务事实。
- `ScenarioRun.ResolveDayEncounter` 在遭遇卡牌真实创建后，把本次生成的内容 ID 作为 `CardsCreatedQuestTaskFact` 提交给当前单局 `QuestLog`，随后刷新状态型任务；日终生命周期仍由 `ScenarioRun` 持有，没有新增日终 Manager、事件包装或第二任务系统。
- 新鲜验证：刷新 AssetDatabase 后新增测试进入 Unity TestRunner；`ScenarioRunEditModeTests.DayCycle_CreatedEncounterCardsAdvanceCardCreationQuest` `1/1`、`ScenarioRunEditModeTests` `26/26`、`QuestLogEditModeTests` `16/16`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity 编译空闲且 Console `0` 错误。第一次单条执行曾因新增测试尚未被发现而卡在 `starting` 90 秒，刷新后已恢复。

## 2026-08-15：StackCraft 参战卡拖出战斗区离战补齐

- 对证 StackCraft `CardController.HandleCombatDrop` 与 `CombatTask.Flee` 后，补齐“参战卡牌拖出战斗区域后离开战斗并回到牌桌放置”的玩家效果；不恢复 `CombatManager`、旧 `CombatRect` 权威、固定 Player / Mob 阵营、旧输入系统或旧逃跑权限结构。
- `Tabletop.TryDropBattleParticipant` 成为牌桌唯一提交入口：释放点在战斗区内时保持参战，释放到战斗区外时先按牌桌放置规则预演，再离战、必要时结束战斗，最后提交牌堆落桌。空间不足会整体拒绝，不留下半离战或半放置状态。
- `TabletopInteraction` 对参战卡拖拽优先解释为战斗释放语义，避免被普通行动候选抢走；普通卡牌拖拽仍走既有行动候选和空白落桌链路。`TabletopCardDragInput` 的命中选择改为最高视觉排序优先，同排序再按射线距离，保证玩家看到在上层的卡牌先被拖中。
- 新鲜验证：Unity 编译空闲且 Console `0` 错误；定向 `BattleEditModeTests.DropBattleParticipant_InsideAreaStaysAndOutsideAreaLeavesThenPlacesStack` `1/1`、完整 `BattleEditModeTests` `15/15`、定向 `FoundationTestScenePlayModeTests.FoundationTabletop_DraggingBattleParticipantOutsideAreaLeavesBattle` `1/1`、完整 `FoundationTestScenePlayModeTests` `26/26` 通过。阶段 C 仍是补充审计中，不能据此宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft Sell 任务事实补齐

- 对证 StackCraft `QuestType.Sell` 与 `QuestManager.HandleCardsSold` 后，确认模板出售任务支持两种玩家效果：指定卡牌出售计数，以及目标为空时任意卡牌出售计数。
- Gameplay 已由现有出售行动链覆盖该效果：`SellCardsResultIntent` 只声明出售槽位、货币卡和货币生成锚点；`ActionResultSettlement` 在成功结算后移除被售卡牌、生成对应货币，并返回已售内容 ID。
- `ScenarioRun` 只在正式行动成功提交后把 `CardsSoldQuestTaskFact` 交给当前单局 `QuestLog`；`CardSaleQuestTaskDefinition` 自己按指定卡牌或空目标累计进度。不恢复 `QuestManager`、`QuestType` 枚举、`TradeManager`、全局售卡事件或第二经济系统。
- 新鲜验证：刷新 AssetDatabase 后新增测试进入 Unity TestRunner；`ScenarioRunEditModeTests.CompletedSaleAction_AdvancesCardSaleQuest` `1/1`、完整 `ScenarioRunEditModeTests` `27/27` 通过；Unity 编译空闲且 Console `0` 错误。阶段 C 仍是补充效果审计中，不能宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft Discover 与 Day 任务事实补齐

- 对证 StackCraft `QuestType.Discover` 与 `QuestManager.HandleRecipeDiscovered` 后，确认模板效果是“配方被发现后推进发现任务”。Gameplay 不恢复 Recipe 枚举、`CraftingManager` 全局事件或独立配方系统；研究行动成功后由 `ScenarioRun` 将发现内容事实交给当前 `QuestLog`。
- `ContentDiscoveryQuestTaskDefinition` 继续作为现有任务子项解释唯一内容 ID。研究结果由 `ResearchDiscoveryResultIntent` 在行动开始时冻结候选，完成时用牌桌权威随机选择，并发现对应行动 / 配方卡；任务只读取 `ContentDiscoveredQuestTaskFact`。
- 对证 StackCraft `QuestType.Day` 与 `QuestManager.HandleDayChanged` 后，确认模板效果是“当前天数达到目标后推进任务”。Gameplay 已由 `ScenarioRun.RefreshQuestState` 在同一单局日期事实上提交 `DayReachedQuestTaskFact`，后继任务激活时会立即读取当前天数。
- 新鲜验证：Unity 资产刷新后脚本编译成功，Console 无错误；`ScenarioRunEditModeTests.CompletedResearchAction_AdvancesContentDiscoveryQuest` `1/1`、完整 `ScenarioRunEditModeTests` `27/27` 通过。阶段 C 仍是补充效果审计中，不能宣称 StackCraft 完整吸收完成。

## 2026-08-15：StackCraft HitUI 命中反馈吸收

- 对证 StackCraft `HitUI.Initialize` 后，吸收 Miss / Normal / Critical 命中类型图标、Advantage / Disadvantage 克制图标，以及 `0.15` 幅度、`1s` 的 punch 缩放反馈；不恢复独立 `HitUI` 预制体生命周期、`CombatManager.SpawnHitUI` 或 DOTween 运行时依赖。
- `TabletopCardView` 作为目标卡牌的表现对象直接投影命中反馈：Miss 有图标且不显示伤害文字，普通命中 / 暴击显示伤害数字和对应图标，优势 / 劣势使用独立图标表达。`NoFloatingText` 仍由 `TabletopView` 屏蔽整段卡牌命中反馈。
- 地基测试场景生成器现在把已迁入 `Assets/Art/Sprites` 的 5 张命中反馈图片序列化到测试卡牌视图，不在运行时代码按 StackCraft 路径加载，也不新增第二套资源系统。
- 新鲜验证：源码定向 `git diff --check` 通过，`.spec` lint 通过，规范测试 `2/2` 通过，静态搜索确认没有恢复 `HitUI`、`CombatManager`、`AudioManager`、`AudioId`、`CombatType` 或 DOTween 调用。Unity 自动化被当前同工程两个 Unity Editor 和两个 ShaderCompiler 进程阻塞，待编辑器环境空闲后补跑场景重建与 PlayMode。

## 2026-08-16：StackCraft 标题日长滑条吸收

- 对证 StackCraft `GameplayPrefsUI`、`Title.unity` 和 `TimeManager` 后，确认模板玩家效果是“新局前选择整天持续秒数”，默认 120 秒、范围 60-180 秒；它不是独立时间系统，也不是原创剧本规则。
- `ScenarioStartOptions` 新增本局日长秒数覆盖值，并写入 `ScenarioRunSnapshot`；`ScenarioRun` 创建和恢复时统一换算为 `SecondsPerTurn = 日长覆盖值 / TurnsPerDay`。剧本作者源默认 `ScenarioDefinition.SecondsPerTurn` 仍保留，标题选择只覆盖当前单局。
- `ScenarioTitlePanel` 新增日长滑条和中文标签，测试标题场景生成器按模板参数生成 60-180 / 默认 120 秒；标题新局把滑条值传给 `ScenarioDirector.StartScenarioAsync`，不恢复模板 `GameplayPrefs`、`TimeManager` 或固定标题场景。
- 新增回归：`ScenarioRunEditModeTests.StartOptions_DayDurationOverrideDefinesPerTurnSeconds`、`ScenarioRunEditModeTests.Snapshot_PersistsDayDurationOverrideAndRestoresRealtimeProgressAgainstIt`、`ScenarioTitleScreenPlayModeTests.TitlePanel_DayDurationSliderStartsScenarioWithSelectedDayLength`。当前只完成源码接入和静态检查；Unity guard 仍显示同项目两个 Unity Editor、两个 ShaderCompiler 和 `Temp/UnityLockfile`，因此 PlayMode / 编译需等 Unity 独占后补跑。

## 2026-08-16：StackCraft InfoPanel / MenuToggle 菜单焦点吸收

- 对证 StackCraft `InfoPanel`、`MenuView` 和 `MenuToggle` 后，确认全局信息优先级队列不是 CardLoop 正式 owner：悬浮卡牌信息、任务 / 配方日志、日终提示和确认按钮已经分别由当前正式 UI 面板承接，不恢复 `InfoPanel` 单例、请求字典、`InfoPriority` 或 `TextButton`。
- 吸收的玩家效果是“日终流程开始时关闭菜单 / 日志，避免遮挡日终处理”。`ScenarioJournalPanel` 现在监听当前单局的日终阶段事件，进入非 `Inactive` 阶段时关闭自身；它不保存第二份菜单状态，也不接管日终规则。
- 新增回归：`ScenarioJournalPanelPlayModeTests.JournalClosesWhenDayCycleTakesOver`。当前只完成源码接入和静态检查；Unity guard 仍显示同项目两个 Unity Editor、两个 ShaderCompiler 和 `Temp/UnityLockfile`，因此 PlayMode / 编译需等 Unity 独占后补跑。

## 2026-08-16：StackCraft TitleScreen / SavedGamesUI 标题入口复核

- 对证 StackCraft `TitleScreen` 后，确认它只承载标题页四个按钮：新游戏、读取、设置、退出确认。当前对应 `ScenarioTitleScreen` + `ScenarioTitlePanel`，请求继续交给 `ScenarioDirector`、`ScenarioSavePanel`、`UISettings` 和 `ConfirmationDialogPanel`，不恢复旧 `TextButton` 或模板面板引用链。
- `SavedGamesUI`、`SavedGameSlot` 和 `ModalWindow` 的玩家流程已经由 `ScenarioSavePanel`、`ScenarioSaveSlotView`、GameCore `SaveSystem` / SaveKit 与 `ConfirmationDialogPanel` 接管：动态槽位、读取、删除单槽、清空全部、关闭和确认弹窗都不保存第二份 UI 状态。
- 现有回归已经覆盖标题四命令、设置 / 退出确认、标题新局、友好模式、日长滑条、动态存档列表、读取、删除、清空全部和保存退出。2026-08-16 后补切片仍需等 Unity 独占后补跑对应 PlayMode。

## 2026-08-16：StackCraft 嵌套值对象与局部类型收口

- 对证 StackCraft 剩余未点名类型后，确认 `LootEntry`、`AnimatedEquipment`、`HitType`、`CombatTypeAdvantage`、`CombatState`、`QuestGroup`、`QuestData`、`VendorData`、`TimeData`、`ShadowPreset`、`CustomPass` 和 `Styles` 都是旧实现内部条目、DTO、枚举或 GUI 缓存，不是独立玩家模块。
- 它们已分别归入当前正式 owner：掉落 / 产出归行动、遭遇和卡包链；装备漂浮缓存被角色装备事实和详情面板替代；命中反馈归 GNS / EX-GAS 战斗链与 `TabletopCardView`；任务进度和商贩付款归 `ScenarioRunSnapshot` 与派生卡牌状态；设置与后处理归 `DisplaySettingsSystem` 和 `ScenarioScreenEffectView`。
- 本轮不新增代码，不恢复旧 DTO 或旧枚举。后续若要做怪物掉落或其它面板的已读 / 未读状态，必须按当前作者源和单局快照重新裁决，不能把这些旧局部类型作为第二套真相迁回。
