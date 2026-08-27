---
name: stackcraft-system-reference-matrix
description: StackCraft 架构吸收审查表：按依赖顺序重排模块，先建立 Gameplay 数据定义与内容契约，再逐块重构吸收 StackCraft，并纳入 UI 框架与联机约束。
metadata:
  type: feature
  status: 设计中
---

# StackCraft 模块吸收审查表

## 2026-08-27 当前重审口径

- 用户已确认干净 StackCraft 模板开卡包和拖拽手感更好，且不存在当前 CardLoop 的合堆层级异常；因此当前问题按“CardLoop 吸收偏离”处理。
- 本文 2026-08-26 及更早的“已通过 / 已完成 / 已接管”只能证明当时对应脚本、测试或字段直接覆盖的事实，不能继续证明当前已经完整复刻，也不能证明可以删除模板。
- 继续实施前必须先收窄到 StackCraft 模板业务闭包，删除或隔离原创策划、临时扩展、演示 UI 和非模板规则，再逐项对照输入、命中、堆叠、动画、资源和卡面参数。

## 2026-08-25 静态预检调用与对象引用级收紧

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补静态预检证据质量，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- 牌桌放置解算必须证明每一次正式 `TabletopCardStackPlacementSolver.Solve(...)` 调用都把地区规则里的重叠解算迭代次数作为第三个实参传入；不再允许用正则截断调用文本后查找片段来证明。
- 卡包商贩资产必须解析到 Unity YAML 中唯一 `Gameplay.Runtime::Gameplay.Content.PackVendorDefinition` 对象块，并在该对象块内证明不计入卡牌上限；不再允许整文件字符串出现类型名就当成资产类型命中。
- 剧本日志 HUD 的玩家文案必须落在对应父子对象和目标组件字段上；不再保留“任意 TMP 文本对象里出现目标字符串”的重复弱证据。
- TMP 字体材质必须从 `MeshRenderer.m_Materials` 引用列表读取并对账；不再允许整块 Renderer 文本包含目标 GUID 即通过。
- 新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据只证明静态守卫闭包，不证明 Unity 编译、玩家画面、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 静态预检弱证据字段级收紧

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补静态预检证据质量，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- 统一测试场景装配器的牌桌视图、拖拽输入和交互依赖，必须在 `FoundationTestSceneHarness` 类声明和 `Awake()` 方法体中成立；不再允许同文件其它位置出现同名 token 就误判为依赖已自动装配。
- StackCraft 同态打开初始卡包任务的描述，必须由 `m_description` 字段完整多行值对账；不再允许两段文本散落命中冒充任务文案已对齐。
- `剧本屏幕效果配置.asset` 必须由唯一 `VolumeProfile` 根对象引用唯一 `Vignette` 与 `ColorAdjustments` 对象块，并字段级检查 `intensity` / `saturation` 初始 override；不再允许 URP 后处理字段散落命中冒充 Profile 已对齐。
- 新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/spec-lint.mjs`、`node --test .spec/tools/spec-lint.test.mjs`、`node .spec/tools/unity-yaml-guard.mjs` 已通过；该证据只证明静态守卫闭包，不证明 Unity 编译、玩家画面、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 YooAsset 内容作者源收集器分组静态对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补 Gameplay 内容作者源进入 ResourceSystem / YooAsset 收集配置的静态字段闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- 参考裁决链：正式内容作者源必须通过项目既有 `GameCore.ResourceSystem` / YooAsset 收集规则进入加载链；不能新增第二套资源地址真相，也不能只靠全文件出现过分组名、标签或路径来证明配置正确。
- CardLoop 正式 owner 仍是 `Assets/BundleCollectorSetting.asset` 中的 YooAsset 收集配置与现有 `ResourceSystem`。本轮不新增内容加载包装、不新增 `ContentCatalog`，也不恢复 StackCraft `Resources.LoadAll`。
- `gameplay-static-preflight` 已把 `CollectPath: Assets` 所属收集器分组升级为字段级对账：该路径所在分组必须同时满足 `GroupName = Gameplay内容定义` 与 `AssetTags = gameplay-content`，避免分组名、标签和路径散落在不同块里仍误判通过。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs` 已通过；该证据只证明收集器分组静态闭包，不证明 YooAsset 构建、Unity 编译、运行加载、玩家画面、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 普通卡面文字全量静态对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补普通卡面文字节点的 Prefab YAML 静态字段闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链：12 类卡牌 Prefab 分别提供 `Title`、`Price`、`Nutrition`、`Health` 文字节点；这些节点的 Transform、RectTransform、TMP 字号、颜色、对齐、边距、样式、字距和换行 / 溢出语义共同构成普通卡面文字效果。
- CardLoop 不恢复 StackCraft 的旧 `CardInstance` 文字投影脚本或按类别复制 12 套卡牌 Prefab。正式 owner 仍是 `Assets/Art/Prefabs/牌桌/卡牌视图.prefab` 与 `TabletopCardView`：所有普通卡面文字统一由 `标题`、`价格`、`营养`、`生命` 四个正式节点承接。
- `gameplay-static-preflight` 已把 12 类卡牌 Prefab 的对应文字节点全量纳入对象级守卫，避免只用 `Card_Character` / `Card_Consumable` 作为代表样本。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据只证明源码 / Prefab YAML 字段层闭包，不证明 Unity 编译、Prefab 回读、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 GameplayPrefsUI 标题新局偏好静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补标题新局偏好链脚本的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链：`GameplayPrefsUI` 负责日长滑条、友好模式开关、取消、确认、友好模式切换文案和点击音效；确认时读取 `durationSlider.value` 与 `isFriendlyToggle.isOn`，构造 `GameplayPrefs` 并调用旧 `GameDirector.NewGame(prefs)`。
- StackCraft 存档 DTO 来源链：`GameData.GameplayPrefs` 中的 `DayDuration` 与 `IsFriendlyMode` 是旧开局偏好保存事实；后续旧 `EncounterManager` 会通过 `GameDirector.Instance.GameData.GameplayPrefs.IsFriendlyMode` 过滤敌对遭遇。
- CardLoop 不恢复 `GameplayPrefsUI`、`GameplayPrefs`、`GameDirector.Instance.NewGame(prefs)`、旧 `AudioManager` 点击音效或旧 `TimeManager` 日长真相。正式 owner 是 `ScenarioTitlePanel` 的 UIKit 开局面板、`ScenarioStartOptions` 的开局选项、`ScenarioRun` 的日长换算 / 友好模式消费、`ScenarioRunSnapshot` 的持久化事实，以及 `FoundationTitleTestSceneMenu` 生成的标题测试入口。
- `gameplay-static-preflight` 已新增直接来源脚本守卫、当前 owner 守卫、测试场景生成器守卫和回归测试守卫，并禁止 `GameplayPrefs` 旧 DTO 名称回流到正式标题 / 存档 UI 链路。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据不证明 Unity 编译、PlayMode、玩家画面、完整标题 UI 视觉或模板可删。

## 2026-08-25 GameData / SaveSystem / SavedGamesUI 存档链静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补存档链脚本的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 原始来源链：`GameData` / `SceneData` 保存槽位、当前场景、发现内容、已读项、牌堆、战斗、任务、商贩、遭遇、时间和开局偏好；`SaveSystem` 直接把 `GameData` 作为 `persistentDataPath` JSON 写入、读取、全目录扫档和删除；`GameDirector` 负责新局找槽、保存、读档、删除、返回标题、GameOver 和场景数据事件。
- 参考模板在 CardLoop 内的可运行性补丁：`LoadAllValidData<GameData>()` 只读取 `SaveSlot*.json` 并跳过缺少当前场景的旧存档对象，避免误读 `GameCoreModConfig.json` 等 CardLoop 自有配置；`GameDirector` 在 Editor 下按 `Assets/StackCraft/Scenes/{sceneName}.unity` 加载参考场景，避免依赖空的 Build Scene List。该补丁只让参考模板继续作为对照样本运行，不进入正式 Gameplay 存档 / 场景链路。
- StackCraft 标题存档 UI 来源链：`TitleScreen` 打开存档列表，`SavedGamesUI` 从 `GameDirector.Instance.SavedGames.Values` 动态生成槽位、支持清空全部和关闭，`SavedGameSlot` 显示 `[Slot] / CurrentScene / QuestProgress / LastSaved`，并调用旧 `GameDirector.LoadGame/DeleteGame`。
- CardLoop 不恢复 `GameData`、`SceneData`、`StackData`、`CardData`、`QuestData`、`VendorData`、`TimeData`、`GameplayPrefs`、`LoadAllValidData`、旧 JSON 全目录扫档、`SavedGamesUI`、`SavedGameSlot` 或旧 `GameDirector` 存档事件。正式 owner 是 GameCore `SaveSystem` / `SaveFileStorageRuntime` 的 SaveKit 整数槽位容器，`ScenarioRunSnapshot` 的整局事实，`ScenarioDirector` 的保存 / 原子读档 / 删除 / 自动保存，以及 `ScenarioTitlePanel`、`ScenarioSavePanel`、`ScenarioSaveSlotView` 的 UIKit 存档流程。
- `gameplay-static-preflight` 已新增直接来源脚本守卫、当前 owner 守卫和回归测试守卫，并禁止旧存档 DTO / UI / JSON 槽位结构 token 回流到正式 Gameplay 代码。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据不证明 Unity 编译、PlayMode、玩家画面、完整存档 UI 视觉或模板可删。

## 2026-08-25 Quest / QuestInstance / QuestManager 任务链静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补任务系统脚本的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链：`Quest` 用 `QuestType`、目标卡牌、目标配方、目标数量、时间速度、前置任务和后继任务声明任务；`QuestInstance` 保存运行状态和单个整数进度；`QuestManager` 持有任务分组、活动任务、完成任务、ID 查重、存档恢复、任务激活、完成和后继解锁。
- StackCraft `QuestManager` 通过旧单例事件消费 `Have / Obtain / Discover / Defeat / Craft / Sell / Buy / Equip / Explore / Time / Day / Food / Coins / Capacity` 分支；这些分支证明玩家可见任务事实需要吸收，但不证明应保留 `QuestType` 枚举、跨 Manager 订阅或双向 `QuestsToUnlock`。
- CardLoop 不恢复 `QuestManager`、`QuestInstance`、`QuestType`、`GameData.SaveQuests`、旧跨 Manager 事件订阅或任务单例。正式 owner 是 `QuestDefinition`、`QuestProgress`、`QuestLog`、`QuestTaskDefinition`、`QuestTaskRuntimeState`、`QuestLogSnapshot` 和 `ScenarioRun`：任务作者源只声明前置任务和子项，当前单局任务日志拥有状态和事实分发，子项解释已提交事实，剧本单局负责把牌桌、行动、战斗、发现、日期和商贩购买事实交给任务日志。
- `gameplay-static-preflight` 已新增直接来源脚本守卫、当前 owner 守卫、任务分支回归测试守卫，并禁止旧任务结构 token 回流到正式 Gameplay 代码。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据不证明 Unity 编译、玩家画面、完整任务 UI、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 DayCycleManager 日终五阶段静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补日终流程脚本的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链：`DayCycleManager` 通过 `TimeManager.Instance.OnDayEnded` 进入日终，先用 `InfoPanel` 提示并等待 `Feed People`，再调用 `CardManager.Instance.FeedCharacters()` 分配食物；若角色全灭则 `GameOver`，否则进入超限卖卡阶段。
- StackCraft 超限阶段会解除输入锁并监听卡牌统计，只有 `ExcessCards <= 0` 才进入遭遇阶段；遭遇阶段最多执行一个 `EncounterManager.GetBestEncounter / ExecuteEncounter`，最后进入新日确认，点击 `Start Day` 后 `TimeManager.Instance.StartNewDay()` 并 `GameDirector.Instance.SaveGame()`。
- CardLoop 不恢复 `DayCycleManager`、`TimeManager.Instance`、`InputManager.Instance`、`InfoPanel.Instance`、`CardManager.Instance`、`EncounterManager.Instance` 或 `GameDirector.Instance`。正式 owner 是 `ScenarioRun`、`ScenarioDayCycle`、`ScenarioDirector` 和 `ScenarioTurnPanel`：日终阶段、进食、超限阻断、遭遇解析、新日确认、全员死亡和自动保存都走当前单局 / 剧本导演 / HUD 主按钮链路。
- `gameplay-static-preflight` 已新增直接来源脚本守卫和当前 owner 守卫，并禁止旧日终结构 token 回流到正式 Gameplay 代码。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 EncounterDefinition / EncounterManager 日终遭遇静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补日终遭遇脚本的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链：`EncounterDefinition` 声明遭遇 ID、提示文本、生成卡牌、数量、一次性、日期类型、日期值、优先级、概率和牌桌卡牌上限；`IsValidForDay` 依次过滤牌桌上限、一次性记录、友好模式、日期条件和概率。
- StackCraft `EncounterManager` 负责旧候选集合、一次性记录读写、按优先级和日期类型选择最多一个遭遇，并在执行时显示 `InfoPanel`、随机生成卡牌、移动镜头、播放烟雾和清理提示。
- CardLoop 不恢复 `EncounterManager`、`EncounterDefinition`、`EncounterType`、旧单例、`InfoPanel`、`CardManager.Instance`、`Board.Instance`、`Camera.main` 或 Unity `Random`。正式 owner 是 `ScenarioDayCycleRules`、`ScenarioRun`、`ScenarioDayCycle`、`ScenarioRunSnapshot`、`QuestLog` 和 `Tabletop` 表现提示：遭遇是剧本日终规则的一部分，不是独立顶级系统。
- `gameplay-static-preflight` 已新增直接来源脚本守卫和当前 owner 守卫，并禁止旧遭遇结构 token 回流到正式 Gameplay 代码。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 CraftingManager / CraftingTask 制作运行链静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补制作运行链本体的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链：`CraftingManager.Awake` 用 `Resources.LoadAll<RecipeDefinition>("Recipes")` 扫描全部配方并把 `DiscoveredRecipes` 接入旧 `GameData`；`CheckForRecipe` 自动匹配整堆材料，按 `RandomWeight` 选择配方；`StartCraftingTask` 把 `CraftingTask` 加入活动列表、锁定牌堆、实例化 `ProgressUI`，并在启动时标记配方发现；`Update` 每帧推进、刷新 UI、完成后执行配方并清理；`PerformCraftingAction` 执行结果、通知统计、按连续制作或消耗后剩余材料尝试重复制作；`ValidateAndResumeTask` 在堆内容变化后恢复或停止制作。
- StackCraft `CraftingTask` 本身只保存 `Recipe`、`TargetStack`、`Progress`、`IsCanceled`、`IsPaused`，以秒推进，暂停 / 取消 / 完成时不推进，并支持 `SetProgress` 恢复。
- CardLoop 不恢复 `CraftingManager`、`CraftingTask`、`Resources.LoadAll`、旧 `ProgressUI` 字典、旧发现集合或旧每帧自动制作链。正式 owner 是 `ActionCandidateResolver`、`ActionPlan`、`ActionInstance`、`Tabletop.ActiveActions`、`ActionResultPlanSnapshot`、`ActionResultSettlement`、`TabletopActionProgressView` 和 `ScenarioRun` 发现 / 任务事实链：候选生成替代自动配方匹配，行动计划替代活动制作堆的填充阶段，行动实例替代制作任务进度，牌桌权威随机替代旧随机，结果计划替代 `RecipeDefinition.Execute` 直接副作用。
- `gameplay-static-preflight` 已新增直接来源脚本守卫和当前 owner 守卫，并禁止 `CraftingTask` / `CraftingManager` 旧运行时结构回流到正式 Gameplay 代码。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 Grower / Research / Recipe 特殊配方静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补特殊卡 / 特殊配方脚本的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源分层：`GrowerDefinition` / `ResearchDefinition` 是空派生类型标记；`RecipeDefinition` 把材料、分类、展示名、产物、连续制作、允许额外材料、耗时、随机权重和消耗模式放在旧配方 SO 上，并由 `Execute` 直接消费材料、播放反馈、生成卡牌和通知旧 Manager。
- 特殊子类来源链：`GrowthRecipe` 以 `GrowerDefinition` 类型识别种植器、保留种子堆并生成成长结果；`ExplorationRecipe` 从区域卡随机 loot 生成探索产物并通知探索事实；`ResearchRecipe` 扫描旧 `CraftingManager.AllRecipes`，过滤未发现普通配方并随机生成配方卡；`TravelRecipe` 在消费后把目标场景名和旅行者交给 `GameDirector`。
- CardLoop 不恢复上述旧类型标记、旧配方基类、旧全局配方扫描、旧直接副作用或固定场景名。正式 owner 分别是 `ContentAsset` / `DisplayableContentAsset`、`CardDefinition`、`ActionDefinition`、`ActionSlotDefinition`、`ActionResultIntent`、`ActionResultSettlement`、`Tabletop.SelectResultBranch` 和 `ScenarioDirector.TravelAsync`；旅行效果继续由剧本地区和正式 `SceneSystem` 承接。
- `gameplay-static-preflight` 已新增直接来源脚本守卫和当前 owner 守卫，并禁止特殊卡 / 特殊配方旧结构 token 回流到正式 Gameplay 代码。新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过；该证据不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-25 PackEntry / PackSlot / PackInstance 静态来源闭包

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补卡包槽位脚本的源码级静态闭包，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链是 `PackInstance.OnClick` 点击锁定当前堆并调用 `PullFromNextSlot`，`PullFromNextSlot` 通过 `Definition.Slots[Definition.Slots.Count - UsesLeft]` 逐槽抽取，生成卡牌后 `Use()`，用尽后 `Kill()`；`PackSlot.GetRandomCard` 先按 `RecipeChance` 尝试尚未发现配方并创建配方卡，失败或无可用配方时回退 `PackEntry.Weight` 加权普通卡池。
- CardLoop 不恢复 `PackInstance` 点击脚本、`CraftingManager` 发现集合、`CardManager.CreateRecipeCardDefinition` 或旧 `Random.Range`。正式 owner 是 `CardPackDefinition`、`OpenCardPackResultIntent`、`ActionResultSettlement.AddCardPackDraw` 和 `Tabletop.UseCard`：槽位和权重是内容作者源，打开是即时行动结果，配方发现和普通产物在行动开始时冻结，随机归当前牌桌权威随机流，最后一次使用通过牌桌移除链提交。
- `gameplay-static-preflight` 已新增直接来源脚本守卫和当前 owner 守卫：`PackEntry`、`PackSlot`、`PackInstance`、`PackDefinition` 的源码语义必须能映射到当前 `CardPackEntry` / `CardPackSlotDefinition` / `OpenCardPackResultIntent` / `ActionResultSettlement.AddCardPackDraw` / `Tabletop.UseCard`，并要求 `CardPackEditModeTests` 覆盖逐槽移除、同种子权重抽取和未发现配方优先。
- 新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 已通过，仅提示没有 `.sln / .csproj`，C# 编译必须留到 Unity 阶段。该证据不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 Unity 编译与当前场景状态补证

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮补的是“静态对账之后，Unity 编译和当前编辑器状态是否可用”的证据，不进入原创业务、不做最终视觉验收，也未删除 `Assets/StackCraft`。
- 新鲜 CLI 静态验证：`gameplay-static-preflight --strict-auxiliary-parity` 通过，仅提示缺少 `.sln / .csproj`，C# 编译必须以 Unity 为准；`stackcraft-business-representative-audit` 通过，统计为 cards=103、packs=11、recipes=90、quests=66、encounters=3；`unity-yaml-guard` 通过，扫描 15785 个 Unity 文件；`.spec` lint 通过。
- UnitySkills 正式链路：`unity-verify status` 确认当前项目有一个主编辑器和同项目导入 / Shader 子进程；`unityskills-ensure` 返回端口 8090、队列 0、未编译、未导入；`unity-verify preflight --mode editor-automation --tool unityskills` 通过。
- Unity 编译和控制台：`/compile/status` 显示最近一次脚本编译成功，C# 错误数为 0，警告数为 84；控制台唯一错误的现实含义是 Unity 服务登录 / 网络令牌交换失败，原始错误为 `UnityConnectWebRequestException: Token Exchange failed...`，不能用它否定 Gameplay 编译。
- 场景回读：`Assets/Scenes/FoundationTest.unity` 与 `Assets/Scenes/FoundationStackCraftParityTest.unity` 均可加载，活动场景 `isDirty=false`，缺失引用数为 0；健康检查各有 1 条 `MissingLight` 警告，现实含义是场景内没有 Light 组件，不是缺失引用或脚本编译错误。检查后已恢复到 `Assets/Scenes/FoundationTitleTest.unity`，恢复后的标题场景 `isDirty=false`、根对象 3 个。该证据只覆盖场景可加载和引用完整，不证明玩家画面、连续动画、StackCraft 全量效果或模板可删除。

## 2026-08-23 Main / Island 场景字段级对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补代码级静态对账，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft Main 场景的 Prefab 实例来源必须只有三个：`Board01.prefab`、`UIRoot.prefab` 和 `CameraController.prefab`。这为后续场地、HUD 和镜头参数对账提供场景级入口证据。
- StackCraft 来源链是 `Assets/StackCraft/Scenes/Main.unity` 里 `CardManager.defaultSpawnCards`。该列表当前只有一项，指向 `Assets/StackCraft/Resources/Packs/00_Pack_Starter.asset`；`defaultSpawnPosition = {-3.5, 0, 0.6}` 与 `defaultSpawnRadius = 1.2` 已继续作为默认出生位置和半径来源。
- 同一场景对象还必须精确命中这些参考字段：`packPrefab` 指向 `Assets/StackCraft/Prefabs/PackInstance.prefab`；`cardPrefabs` 11 个类别分别指向 `Card_Resource`、`Card_Character`、`Card_Consumable`、`Card_Material`、`Card_Equipment`、`Card_Structure`、`Card_Currency`、`Card_Recipe`、`Card_Mob`、`Card_Area`、`Card_Valuable`；`aggressiveMobPrefab` 指向主动敌人卡；`recipeCardTemplate` 指向 `Card_Recipe.asset`；`stackingMatrix` 指向 `SRM_Default.asset`；`cardSettings` 指向 `Default_Card_Settings.asset`。
- StackCraft Main 的其它局内核心 Manager 场景字段也纳入守卫：`TradeManager` 必须指向 `CardBuyer.prefab`、`Card_Coin.asset`、`PackVendor.prefab` 和 01-08 八个可售卡包；`EncounterManager.allEncounters` 必须指向 Villager / Weekly Slime / Weekly Goblin 三个遭遇；`CombatManager` 必须指向 `CombatRect.prefab`、`HitUI.prefab`、`Projectile_Arrow.prefab`、`Projectile_Magic.prefab`；`CraftingManager.progressUIPrefab` 必须指向 `ProgressUI.prefab`。这只证明模板真实场景配置，不代表恢复模板 Manager 结构。
- StackCraft Main 的 `QuestManager` 同样进入守卫：第一组必须是 `Introduction`，并按顺序引用 `Assets/StackCraft/Resources/Quests/01_Introduction/introduction_01.asset` 到 `introduction_15.asset`。CardLoop 的 Starter 同态场景只启用第一条 `Open Starter Pack`，用当前 `QuestDefinition` + `ActionCompletionQuestTaskDefinition` 表达打开卡包完成一次，不恢复旧 `QuestManager`、旧 `QuestType` 枚举或双向 `QuestsToUnlock`。
- StackCraft Island 场景也进入同一守卫：Prefab 实例来源必须只有 `Board02.prefab`、`UIRoot.prefab` 和 `CameraController.prefab`；默认出生卡包必须是 `10_Pack_Island.asset`；交易货币必须是 `Card_Coral.asset`；可售卡包必须只有 `11_Pack_Survival.asset`；遭遇列表必须为空；战斗 / 制作 UI 引用继续沿用同一组 StackCraft Prefab；`QuestManager` 必须只登记 `The Basics` 三条任务来源。该记录只是模板旅行场景来源对账，不是原创岛屿剧本设计。
- CardLoop 不恢复 `CardManager`、`Resources` 扫描或场景内默认卡牌列表。正式同态入口仍是 `FoundationTestSceneHarness`：同态布局只生成 `TestCardPackContentId = "test.foundation.pack"`，并通过 `CreateCardAtAuthoritativeRandomSpawnPosition` 使用同一位置和半径。
- 新鲜 CLI 验证口径：`gameplay-static-preflight --strict-auxiliary-parity` 已从 StackCraft Main / Island 场景对象读取上述序列化引用并逐项反查 GUID 路径，同时要求 Harness 的同态开局内容 ID 指向 CardLoop Starter 作者源、同态剧本任务指向 `test.foundation.quest.stackcraft-parity.open-starter-pack`、同态任务标题 / 分组 / 打开卡包行动子项与 Main `introduction_01.asset` 对齐。该证据只证明模板核心场景字段来源闭包，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 拖拽可堆叠目标底牌高亮对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只做源码 / 文档静态对账，不启动 Unity、不进入原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 来源链是 `CardController.OnPointerDown` 调用 `CardManager.HighlightStackableStacks(_card)`；它在按下拿起卡牌时立刻遍历全部已登记 `CardStack`，对满足 `CardManager.CanStack(liftedCard.Definition, stack.BottomCard.Definition)`、不是同卡、不是同堆、不是制作中的目标牌堆，直接高亮该牌堆底牌。释放或取消时由 `TurnOffHighlightedCards()` 清掉全部候选高亮。
- CardLoop 不恢复 `CardManager`、全局高亮列表或 `CardCategory` 矩阵。正式 owner 是 `TabletopCardDragInput` 和 `TabletopView`：拖拽中输入层只读当前牌桌所有牌堆，用 `Tabletop.CanStackOnto` / 行动候选高亮谓词生成可接受目标底牌列表；视图层只维护本地 `SetHighlighted` 表现集合，不保存规则状态、不进入存档、不影响释放目标。
- 同步订正 `AttachRadius` 目标选择：StackCraft 是先过滤可堆叠牌堆，再在有效目标里选择最近牌堆；CardLoop 现在也把 `AttachRadius` 查找限制在本轮可高亮底牌集合内，避免一个更近但不可合堆的卡牌挡住有效吸附目标。
- 新鲜 CLI 验证口径：`gameplay-static-preflight` 新增守卫，要求拖拽中刷新全部候选底牌高亮、释放 / 取消清理集合，并要求 `AttachRadius` 只在当前可执行候选集合内选最近目标。该证据不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 普通拖放 AttachRadius 提交链对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只补源码 / 文档静态对账，不启动 Unity、不切换原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 普通拖放来源链是 `CardController.HandleStandardDrop`：释放后先尝试交易、装备、加入战斗和开战这类终止行为；若都没有命中，则按 `Board.EnforcePlacementRules` 放置牌堆，再调用 `_card.TryAttachToNearbyStack(_card.Settings.AttachRadius, stackToIgnore: null)`。`CardInstance.TryAttachToNearbyStack` 会在半径内找最近可合堆牌堆，并通过 `CardManager.CanStack` / `StackingRulesMatrix` 判断是否允许合堆。
- CardLoop 不恢复 `CardManager`、物理全局 OverlapSphere、`CardCategory` 矩阵或拖拽时真实拆堆。正式 owner 是 `TabletopCardDragInput` 的输入意图、`TabletopInteraction` 的释放解释和 `Tabletop` / `TabletopCards` 的权威牌堆提交：拖拽中直接命中优先，未命中时按 `TabletopViewSettings.AttachRadius` 从当前可见卡面找候选；释放时把该候选写入 `TabletopCardPointerReleaseIntent.TargetCardId`。
- 释放解释顺序保持为：先查当前行动候选并展示；没有行动候选时再走 `Tabletop.TryDropStackOnto` 普通合堆；仍不成立才 `Tabletop.TryPlaceStack` 普通放置。普通合堆规则使用自有放置规则中的 GAS 标签堆叠矩阵，不引入 `CardCategory` 枚举。
- 新鲜 CLI 验证已通过：`gameplay-static-preflight` 新增守卫，要求释放阶段必须把直接命中或 `AttachRadius` 吸附目标提交给正式交互入口；同时既有守卫覆盖“行动候选 -> 普通合堆 -> 普通放置”的释放顺序。本证据不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 日终规则字段对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只对齐场地配置和代码静态证据，不启动 Unity、不改原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 的 `Default_Card_Settings.baseCardLimit = 24` 和 `hungerPerCharacter = 2` 不属于牌桌几何参数，而是日终经济 / 生存规则。来源消费链是 `CardManager.GetStatsSnapshot()` 计算 `CardLimit = BaseCardLimit + TotalBoost`、`NutritionNeed = CharacterCount * HungerPerCharacter`，以及 `CardManager.FeedCharacters()` 按每名角色需求进食、恢复最多 50% 最大生命，食物不足时角色死亡。
- CardLoop 不恢复 `CardSettings` 大一统设置、`CardManager` 或 `DayCycleManager`。正式 owner 是剧本作者源 `ScenarioDayCycleRules` 和单局聚合 `ScenarioRun`：日终进食读取 `HungerPerCharacter`，超限处理读取 `BaseCardLimit + tabletop.CardLimitBonus`，HUD 统计读取同一组运行时规则。
- `地基日终测试剧本` 当前故意使用 `m_hungerPerCharacter: 1`、`m_baseCardLimit: 3`，目的是在统一测试场景快速触发“进食 -> 超限 -> 遭遇 -> 新日”流程。它是测试夹具参数，不是 StackCraft 默认配置；不能把它反向解释为模板默认值，也不能为了场地配置对齐直接改成 `2 / 24`。
- 新鲜 CLI 验证口径：`node .spec/tools/gameplay-static-preflight.mjs` 已新增方法体和资产字段守卫，证明规则语义由 `ScenarioDayCycleRules` / `ScenarioRun` 承接，并证明快速测试资产没有冒充 StackCraft 默认规则值。该证据只证明源码 / 作者源参数闭包，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 移动缓动字段对账

- StackCraft 的 `Default_Card_Settings.moveEase = 6` 对应 DOTween `Ease.OutQuad`；当前 Gameplay 只按这个来源值承接卡牌移动补间。
- `TabletopCardView.ApplyPose` 使用 `DOLocalMove(...).SetEase(Ease.OutQuad).SetUpdate(true)`，并在暂停时同步物理；这是对 StackCraft `SetTargetAnimated` 玩家可见移动手感的代码级复刻。
- StackCraft 的 `swaySharpness = 100` 是拖拽时尾随卡牌追赶目标位置的表现参数，不是规则速度。CardLoop 用 `TabletopViewSettings.DragFollowSharpness` 作为唯一作者入口，由 `TabletopView.ApplyCardPose()` 传给 `TabletopCardView.ApplyDragPose()`，再在视图 `Update()` 中按未缩放时间推进指数跟随。
- 静态守卫现在会先读取 `moveEase` 源字段；如果参考配置不再是 `6`，预检会失败并要求重新裁决缓动映射，不能让代码硬编码和来源配置分离。

## 2026-08-23 默认烟雾与轮廓资源引用对账

- StackCraft 的 `Default_Card_Settings.puffParticle` 指向 `PuffParticle.prefab`，`outlineMaterial` 指向 `CardOutline.shadergraph` 的外轮廓子资源；它们是默认卡牌表现资源，不是 Gameplay 要恢复的大一统设置对象。
- CardLoop 的正式 owner 分别是牌桌视图设置和卡牌视图 Prefab：烟雾由 `TabletopPresentationCueKind.CardSmoke` 请求、`TabletopViewSettings` 配置自有 `卡牌烟雾粒子` 和 `卡牌烟雾反馈`，再由 `TabletopView` 通过 `ResourceSystem` 实例化；候选高亮由 `卡牌视图.prefab` 里的 `候选高亮` 子对象承接，材质 / ShaderGraph 来自自有 `卡牌轮廓.shadergraph`。
- 本轮守卫补强为字段级：先证明 `Default_Card_Settings` 的两个资源引用命中 StackCraft 来源 GUID / fileID，再证明自有 Prefab、材质、贴图、音效和视图代码承接同一玩家可见反馈。该证据不代表最终画面和连续动画已经通过 Unity 视觉验收。

## 2026-08-23 自动移动默认字段对账

- StackCraft 的 `Default_Card_Settings.moveInterval = 5`、`moveRadius = 1`、`maxAttemptsPerMove = 5` 被 `CardAI.AutoMove()` / `MoveRandomly()` 消费：达到间隔后，如果卡牌没有被拖拽、制作或战斗占用，就按固定半径随机找候选点；候选点无效时继续尝试，不把无效点夹回有效区。
- CardLoop 不恢复 `CardAI` 协程、`Board.Instance`、`CardManager.ResolveOverlaps` 或新的 AI 总管。正式 owner 是卡牌作者源 `CardDefinition` 和当前牌桌聚合 `Tabletop`：`CardDefinition` 声明自动移动间隔、半径、尝试次数和围栏式留存容量，`Tabletop.AdvanceRealTime()` 统一推进周期产出和自动移动。
- 生成器和代表性测试卡牌只负责把 StackCraft 默认字段写入自有作者源；真正运行消费链必须落在 `Tabletop.AdvanceAutomaticMovement()`、`TryMoveCardRandomly()`、`TryMoveCardTowards()` 和候选点有效性检查里。
- 本轮静态守卫已经补到方法体级：同时检查 StackCraft 源字段、测试生成器常量、测试资产字段、`CardDefinition` 只读出口 / 校验，以及 `Tabletop` 对 `moveInterval / moveRadius / maxAttemptsPerMove` 的正式消费链。该证据只证明代码 / 配置静态闭包，不证明 Unity 编译、玩家画面、连续动画或模板可删。

## 2026-08-23 出生产物吸附半径对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只对齐场地配置和代码静态证据，不启动 Unity、不改原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 的 `Default_Card_Settings.spawnAttachRadius = 1` 是卡牌创建后用于寻找附近可合堆目标的半径；默认开局、遭遇、存档恢复等入口通过 `CardStack.RefuseAll` 明确禁用创建时吸附，制作结果和周期产出才允许走该吸附链。
- CardLoop 不恢复 `StackingRulesMatrix`、`CardCategory` 或 `CardManager` 全局单例；正式 owner 是地区 / 剧本牌桌放置作者源和 `Tabletop` 牌桌聚合。当前只吸收安全子集：运行时产物出生后，在 `m_spawnAttachRadius: 1` 范围内合入最近的同内容牌堆，并可忽略原行动锚点牌堆。
- 固定场景摆放、开局随机出生、存档恢复和测试夹具显式摆放默认不启用出生吸附，避免把场地配置误改成自动堆叠布局。
- 新鲜 CLI 验证口径：`node --check .spec/tools/gameplay-static-preflight.mjs` 与 `node .spec/tools/gameplay-static-preflight.mjs` 只证明源码 / 作者源参数闭包；不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 牌桌放置迭代参数对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只对齐场地配置和代码静态证据，不启动 Unity、不改原创业务，也未移除 `Assets/StackCraft`。
- StackCraft 的 `Default_Card_Settings.maxIterations = 8` 是牌堆重叠解算每次最多迭代次数，来源消费链为 `CardManager.ResolveOverlaps()` / `EnforceBoardLimits()` 传给 `CardPhysicsSolver.ResolveOverlaps(..., cardSettings.MaxIterations)`。
- CardLoop 不恢复 `CardManager` 或全局物理解算单例；正式 owner 是地区 / 剧本牌桌放置作者源。`TabletopCardPlacementDefinition` 暴露“重叠解算迭代次数”，默认值来自 `TabletopCardPlacementRules.DefaultOverlapResolveMaxIterations = 8`，`TabletopCardStackPlacementSolver.Solve(...)` 只消费当前 `placementRules.OverlapResolveMaxIterations`。
- 测试地区生成器和现有地基测试地区资产均写入 `m_overlapResolveMaxIterations: 8`；旧 `MaxIterations = 64` 硬编码已删除，避免场地配置和运行解算出现两套真相。
- 新鲜 CLI 验证已通过：`node --check .spec/tools/gameplay-static-preflight.mjs` 和 `node .spec/tools/gameplay-static-preflight.mjs`。该证据只证明源码 / 作者源参数闭包，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 订正卡牌中文字体与 StackCraft 文字效果对账

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只订正 CLI 静态对账、测试场景生成源和已落盘 Prefab 字体引用，不改 Gameplay 运行时代码，不启动 Unity，也未移除 `Assets/StackCraft`。
- 订正上一轮错误口径：StackCraft 的 `LiberationSans SDF` 只证明参考模板自身英文 TMP 字体来源，不能作为 CardLoop 卡牌内容字体真相；CardLoop 卡牌标题、生命、价格、营养和命中结果伤害文本必须使用项目中文 TMP 字体。
- StackCraft 继续作为文字效果参数来源：`Title / Health / Price / Nutrition / DamageLabel` 的 Transform、尺寸、旋转、字号基准、颜色、对齐、边距、样式 hash、字距和换行/溢出语义仍由来源 Prefab / YAML 对账；字体资产和材质引用改为独立检查项目中文 TMP 字体 GUID / fileID。
- 当前 owner 对账口径保持不变：商贩、卡包、收购点和卡面文字仍由 `TabletopCardView` 的正式文字组件承接，命中数字仍由 `TabletopHitResultView` 的正式命中 Prefab 承接；本次不新增 UI 结构、不改变运行时表面实现。
- 新鲜 CLI 验证已通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/unity-yaml-guard.mjs`、`node .spec/tools/spec-lint.mjs`、`node --test .spec/tools/spec-lint.test.mjs` 和定向 `git diff --check`。`gameplay-static-preflight` 仍提示未发现 `.sln / .csproj`，C# 编译必须留到 Unity 编译阶段验证；该证据不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移或模板可删。

## 2026-08-23 文本锚点多来源静态对账补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 新增共享文本锚点守卫：`TabletopCardView.StackCraftTextAnchoredPosition` 必须从 StackCraft `PackVendor.prefab` 的 `Title / Tracker / Price`、`PackInstance.prefab` 的 `Title`、`CardBuyer.prefab` 的 `Title` 五个文本 `RectTransform.m_AnchoredPosition` 反查；来源值必须全部一致，才允许当前视图共用一个运行时常量。
- 当前 owner 对账口径保持不变：商贩、卡包和收购点表面仍归 `TabletopCardView`；本次只把文字锚点从手写常量升级为来源 Prefab 字段反查，不新增 UI 结构、不改变表面实现。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/unity-yaml-guard.mjs`、`node .spec/tools/spec-lint.mjs`、`node --test .spec/tools/spec-lint.test.mjs` 和定向 `git diff --check`。该条只证明文本锚点静态守卫更精确，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 TMP 文本样式 hash 多来源静态对账补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改 Gameplay 运行时代码，不改 Unity 场景 / Prefab / 材质 / 素材，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 新增共享整数常量守卫：`FoundationTestSceneMenu.StackCraftConvertedTextStyleHashCode` 必须从 StackCraft `HitUI.prefab` 的 `DamageLabel`、`PackVendor.prefab` 的 `Title / Tracker / Price`、`PackInstance.prefab` 的 `Title`、`CardBuyer.prefab` 的 `Title` 六个 TMP 文本组件 `m_TextStyleHashCode` 反查；来源值必须全部一致，才允许测试场景生成器共用该文字样式 hash。
- 当前 owner 对账口径保持不变：TMP 字体和生成器参数仍归 `FoundationTestSceneMenu` 的 StackCraft 表面资源生成链；本次只把 `-1183493901` 从无来源手写数字升级为来源 Prefab 字段反查，不新增 UI 结构、不改变表面实现。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/unity-yaml-guard.mjs`、`node .spec/tools/spec-lint.mjs`、`node --test .spec/tools/spec-lint.test.mjs` 和定向 `git diff --check`。该条只证明 TMP 文本样式 hash 静态守卫更精确，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 静态守卫类体锚点继续补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 继续把整文件正向 token 检查升级为类体 / 方法体 / 字段初始化断言：视图设置拖拽手感、剧本导演序列队列、货币查询、镜头控制、投射物、卡牌表面、卡牌可见尺寸、命中结果、HUD 统计、行动进度、Foundation 生成器资源常量、StackCraft 候选高亮参考构造语义，以及从 StackCraft `ProgressUI.prefab` 派生反查的行动进度根尺寸、背景 / 填充颜色、初始填充值和显示偏移，都必须由当前正式 owner 的具体类体、方法体、字段初始化或生成器参数写入承接。
- 当前 owner 对账口径保持不变：拖拽手感归 `TabletopViewSettings`，序列播放归 `ScenarioDirector`，货币身份归 `CurrencyCardQuery`，镜头 / 投射物 / 卡牌表面 / 命中 / 行动进度归牌桌视图对象，HUD 统计归 `ScenarioTurnPanel`，卡牌尺寸真相归 `CardDefinition` 作者源并由视图链消费，测试资源常量和 StackCraft 表面字体 / 卡包网格 / 装备面板网格与材质路径常量归 `FoundationTestSceneMenu`；行动进度 Prefab 的生成器默认参数必须由 StackCraft `ProgressUI.prefab` 回读派生，不能成为手写第二真相。
- 商贩口径同步收窄：Starter 商贩的 `Price: 2 → 1 → 2` 是地基测试夹具，用来验证 StackCraft 分次付款、交易高亮、烟雾和卡包生成偏移语义；Beginning 商贩才按 StackCraft `01_Pack_Beginning.asset` 对账原始 `buyPrice` 和 `minQuests`。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 和 `node .spec/tools/stackcraft-business-representative-audit.mjs`。该条只证明静态守卫更精确，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 静态守卫继续方法体化

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 继续把整文件正向 token 检查升级为类体 / 方法体断言：卡牌详情流程提示、货币卡查询、行动启动占用拒绝、烟雾粒子、烟雾音效、拖拽点击阈值、命中结果 punch 和命中镜头震动必须由当前正式 owner 的具体类或方法承接。
- 当前 owner 对账口径保持不变：流程提示归 `TabletopCardInfoPanel`，货币身份归 `CurrencyCardQuery`，行动占用归 `Tabletop.StartActionInstance`，烟雾和命中表现归牌桌视图链，镜头震动归 `GameCore.CameraShake` 的正式表现事件消费链。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs` 和 `node .spec/tools/gameplay-static-preflight.mjs`。该条只证明静态守卫更精确，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 表面与交易链方法 / 字段级对账继续补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 继续删除正向弱证据：收购点 / 卡包商贩测试作者源、交易区布局、镜头控制、投射物、放置几何、测试地区生成器、卡牌视图、HUD、行动进度、剧本货币查询、序列队列和卡牌可见尺寸现在都以具体方法体、类结构或字段承接为准。
- 当前 owner 对账口径保持不变：作者源生成归 `FoundationTestSceneMenu`，统一测试运行状态归 `FoundationTestSceneHarness`，镜头 / 投射物 / 卡牌表面 / 行动进度归牌桌视图对象，HUD 统计归 `ScenarioTurnPanel`，货币身份归 `CurrencyCardQuery`，卡牌尺寸真相归 `CardDefinition` 作者源并由 `TabletopView` 实例化时应用。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs` 和 `node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`。该条只证明静态守卫更精确，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 输入暂停与商贩作者源方法级对账补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 已把商贩解锁序列依赖的 GameCore 输入锁 / 外部暂停锁从整文件 token 检查升级为 `InputSystem`、`GameStateSystem` 类结构和申请 / 释放方法体断言，防止只在无关代码里出现锁字段时误判为已替代 StackCraft `InputManager` / `TimeManager`。
- `CardBuyerDefinition` 与 `PackVendorDefinition` 也升级为作者源方法级对账：收购点必须创建普通牌桌卡并校验货币图标 / 生成偏移；卡包商贩必须创建 / 恢复 `PackVendorCard`，并校验出售卡包、价格、解锁任务数和生成偏移。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/spec-lint.mjs`、`node --test .spec/tools/spec-lint.test.mjs` 和 `node .spec/tools/unity-yaml-guard.mjs`。该条只证明静态守卫更精确，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。
## 2026-08-23 商贩交易链方法级对账补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 继续把商贩交易相关弱证据升级为方法体 / 分支级断言：卡包商贩表面、收购点货币图标、商贩解锁序列、InfoPanel 提示覆盖、货币卡推导、付款 / 出售候选、卡包购买结算和 CardBuyer 出售结算不能再靠源码其它位置出现相同 token 通过。
- 当前 owner 对账口径保持不变：商贩和收购点表面归 `TabletopView` / `TabletopCardView`，解锁序列归 `ScenarioRun` 与 `ScenarioDirector`，流程提示归 `TabletopCardInfoPanel`，货币身份归 `CurrencyCardQuery`，交易候选归行动条件，最终买卖事实归 `ActionResultSettlement`。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 和 `node .spec/tools/stackcraft-business-representative-audit.mjs`。该条只证明静态守卫更精确，不证明 Unity 编译、最终画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 静态守卫方法级对账补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 新增方法体顺序断言，要求关键玩家可见语义落在具体方法内部：牌桌坐标映射、卡牌布局、战斗区域投影、拖拽会话和拖拽输入不再只靠整文件 token 证明。
- 该守卫覆盖 StackCraft XZ 桌面、按下即拿起、牌桌世界距离阈值、正式主相机射线投影、可见卡牌射线命中和牌堆段锚点；它只提升静态证据质量，不替代 Unity 编译、GameView 截图、连续动画或试玩确认。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/unity-yaml-guard.mjs` 和 `node .spec/tools/spec-lint.mjs`。

## 2026-08-23 静态守卫对象块漏检订正

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 已订正 FoundationTest 镜头场景参数对账，避免数组切片漏掉首个 StackCraft 派生字段；当前 `TabletopCameraController` 场景组件的平移、平滑、边距、缩放、距离和聚焦时长都按字段级对账。
- 四个测试地区作者源的 Board01 规则边界、页眉禁放区和卡牌几何已从散落 token 检查升级为 YAML 字段块检查；`m_bounds`、唯一 `m_restrictedAreas[0]`、`m_cardSize / m_cardMargin / m_stackStep` 必须分别命中正确字段。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/unity-yaml-guard.mjs`、`node .spec/tools/spec-lint.mjs` 和 `node --test .spec/tools/spec-lint.test.mjs`。
- 该条只证明静态守卫漏检已订正，不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 静态守卫赋值级对账补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 新增 C# 赋值级断言，避免继续用“源码里出现过某段参考参数文本”证明对齐。
- 已升级为指定字段 / 常量 / 赋值目标对账的范围：牌桌放置几何，商贩解锁提示 / 高亮时长，牌桌镜头参数，测试地区生成器，StackCraft Main 默认出生点，以及桌面背景 / 牌桌底板生成器。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity` 和 `node .spec/tools/stackcraft-business-representative-audit.mjs`。
- 该条只证明静态守卫证据更精确，不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 E2E 过程截图链刷新

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只刷新统一测试场景过程截图链，不切换原创业务，也未移除 `Assets/StackCraft`。
- 进入 Unity 前 `unity-verify status`、`unityskills-ensure` 与 `unity-verify preflight --mode editor-automation --tool unityskills` 通过；编译状态为空闲，Console Error 为 `0`。
- 单条 PlayMode `Gameplay.Tests.FoundationTestScenePlayModeTests.FoundationTabletop_CapturesE2EVisualEvidenceSequence` job `bf626b8b` 返回 `1/1 passed`，耗时 `35s`。
- 过程图已刷新：`foundation-e2e-sequence-01-ready.png` 到 `foundation-e2e-sequence-06-action-completed-product.png`，写入时间 `2026-08-23 04:42:30-31 +08:00`；过程拼图 `_contactsheet-foundation-e2e-sequence-latest.png` 已刷新，尺寸 `1504×582`，写入时间 `2026-08-23 04:43:24 +08:00`。
- 测试后 UnitySkills 队列仍为空、未编译、未导入，Console Error 仍为 `0`。该条只证明过程截图链和代表画面证据可生成，不证明最终视觉 PASS、连续动画手感、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 表现参数静态守卫去弱证据

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只强化 CLI 静态对账，不改玩法运行代码，不切换原创业务，也未移除 `Assets/StackCraft`。
- `gameplay-static-preflight` 已删除无调用的旧 token 辅助函数，避免继续用“源码里出现过某段参考参数文本”证明对齐。
- 下列玩家可见表现参数现在必须命中真实承载字段：`TabletopViewSettings` 的堆叠 / 拖拽 / 移动默认值，`CardBuyerDefinition` 与 `PackVendorDefinition` 的生成偏移，Foundation 交易区布局常量，`Tabletop` 的投射物表现前摇，`TabletopCardView` 的受击闪白 / 摇晃默认值，`TabletopHitResultView` 的弹跳默认值，以及行动进度 Prefab / 视图的显示偏移和运行颜色。
- 新鲜验证通过：`node --check .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs` 和 `node .spec/tools/unity-yaml-guard.mjs`（扫描 `15785` 个 Unity 文件）。
- 该条只证明静态守卫证据更精确，不证明 Unity 编译、玩家画面、连续动画、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 UnitySkills 队列恢复与代表链复跑口径

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只收口验证基础设施恢复、截图链诊断和三条代表 PlayMode 链路，不切换到原创业务，也未移除 `Assets/StackCraft`。
- UnitySkills 卡住的现实入口是 Unity 场景磁盘变更重载确认框阻断编辑器主线程。已通过 `.spec/tools/unity-confirm-scene-reload.mjs` 定向确认场景重载，恢复后 `unityskills-ensure` 显示队列为空、未编译、未导入。
- 截图链 `FoundationTabletop_CapturesE2EVisualEvidenceSequence` 初次 job `1e84dfa1` 失败在“拖拽释放事实未形成”；同一正式拖拽相邻用例 job `35f94c3d` 通过，说明失败不能归因成生产拖拽链坏。当前改动只增强测试辅助断言，让输入锁、点击动作阻挡、正式指针、拖拽会话和拖拽状态在失败点直接暴露。
- 资源刷新与编译后，Unity 编译 `errorCount=0`；截图链 job `a55f01e6` 复跑通过。随后本轮再次刷新截图链 job `d1c4dec2`、Starter 卡包逐槽打开 job `a55870ea`、卡包商贩两次付款 job `5f84ee2c`，三者均为 `1/1 passed`。
- 新鲜收口：`unity-verify preflight --mode editor-automation --tool unityskills`、`gameplay-static-preflight`、`stackcraft-business-representative-audit`、`spec-lint`、规范测试 `2/2` 和 Unity Console 错误检查均通过。当前两个 Console Warning 分别来自 YooAsset 退出阶段中止 `DownloadSchedulerOperation` 和 Unity Entities 包自身 `UpdateAfterAttribute` 排序提示，不构成 Gameplay 复刻代码错误。
- 该条只证明当前代表链路恢复和输入诊断增强；最新六张过程截图已合成为 `Assets/Screenshots/FoundationE2E/_contactsheet-foundation-e2e-sequence-latest.png`（`1504×582`），但它仍不是最终视觉 PASS，不证明连续动画手感、StackCraft 全量业务迁移、完整复刻或模板可删。

## 2026-08-23 地图测试场景主相机层级静态订正

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只修正 `FoundationMapTest` / `FoundationSecondMapTest` 的场景相机结构，不切换到原创业务，也未移除 `Assets/StackCraft`。
- 两个地图测试场景曾保留旧生成结果：`Main Camera` 直接作为场景根对象，虽然相机组件参数正确，但缺少 StackCraft 同形的 `CameraController` 根，且主相机本地 Transform 没有对齐参考 Prefab。
- 当前场景已订正为 `CameraController` 根对象 + `Main Camera` 子对象：根对象保存 StackCraft 视角位置与旋转，子相机只保存参考 Prefab 的本地零位姿；`Camera` 和 `AudioListener` 仍在 `Main Camera` 上。
- 新鲜 CLI 验证：`node .spec/tools/unity-yaml-guard.mjs`、`node .spec/tools/gameplay-static-preflight.mjs`、`node .spec/tools/gameplay-static-preflight.mjs --strict-auxiliary-parity`、`node .spec/tools/stackcraft-business-representative-audit.mjs`、`node .spec/tools/spec-lint.mjs` 和 `node --test .spec/tools/spec-lint.test.mjs` 均通过。
- UnitySkills 当前仍有 1 个重请求卡住，按项目规范不能继续调用编辑器主线程接口。本条只证明场景 YAML 和来源参数的静态对账，不证明 Unity 运行态、场景健康检查、最终画面或模板可删。

## 2026-08-23 测试场景 StackCraft 表面参数落盘订正

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮目标是让测试场景 / Prefab 生成源产出的序列化字段与 StackCraft 参考对象对齐，不切换到原创业务，也未移除 `Assets/StackCraft`。
- `FoundationTestSceneMenu` 已把桌面背景 Transform 的 `m_LocalRotation` 与 `m_LocalEulerAnglesHint` 按 StackCraft `Main.unity` 的 `Background` 精确落盘；生成器仍保留来源派生 token，但实际序列化字段不再受 Unity 欧拉角浮点尾差影响。
- 卡牌视图、命中结果视图和 HUD 统计标签共用 StackCraft TMP 参数写入入口：`m_TextStyleHashCode = -1183493901`、`m_enableKerning = 1`，并保持原有颜色、字号、对齐、溢出和 margin 对象级对账。
- 测试运行根 Prefab 引用统一改为 Prefab 根对象；运行入口实例化根对象后再校验其中包含 `GameManager`。三张测试场景主相机继续显式包含 `AudioListener`，防止 Unity 场景健康检查回流到无监听器状态。
- 新鲜验证：UnitySkills 端口 `8090` 可用，`Assets/Refresh` 后 Unity 编译 `success=true`、`errorCount=0`；`Gameplay/地基/重建测试场景` 与 `Gameplay/地基/重建标题入口测试场景` 执行成功；`gameplay-static-preflight` 与 `--strict-auxiliary-parity` 均通过；`stackcraft-business-representative-audit` 通过；`spec-lint` 与规范测试 `2/2` 通过；`unity-yaml-guard` 扫描 `15785` 个 Unity YAML 文件通过；`git diff --check` 无行尾空白错误，仅保留既有换行转换 warning。
- Unity 只读健康检查：编译未进行、Console Error 为 `0`；活动场景 `FoundationTitleTest` 的 `scene_health_check` 无 Error，仅有无 Light 的 Warning。该结论只证明当前对象级字段和编辑器健康状态，不证明最终视觉 PASS、连续动画手感、全量业务数据迁移或模板可删。
## 2026-08-23 旧输入与固定场景入口守卫补强

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只做 CLI 静态对账，不启动 Unity，不把历史截图或历史 PlayMode 结果当作新鲜证据。
- `gameplay-static-preflight` 已把正式 GameCore / Gameplay / 测试支撑源码纳入依赖入口扫描：旧 `UnityEngine.Input`、UGUI `StandaloneInputModule`、写死 `Main / Island / Title` 的直接切场景入口、全局对象查找和 `Camera.main` 都会直接失败。
- 同步清理旧辅助哈希对账函数，并把 `Transform.Find` 守卫收窄为真实 Transform 层级查找，避免把 `Shader.Find` 调试材质 fallback 误报为依赖入口问题。
- 新鲜验证：`node --check .spec/tools/gameplay-static-preflight.mjs` 通过，`node .spec/tools/gameplay-static-preflight.mjs` 与 `--strict-auxiliary-parity` 均通过，`node .spec/tools/spec-lint.mjs` 通过，`node --test .spec/tools/spec-lint.test.mjs` `2/2` 通过，`node .spec/tools/stackcraft-business-representative-audit.mjs` 通过，`node .spec/tools/unity-yaml-guard.mjs` 扫描 `15785` 个 Unity YAML 文件通过。
- 该结论只证明旧输入 / 固定场景名 / 正式依赖入口回流会在 CLI 阶段被拦截，不等于 Unity 编译、最终画面、连续动画手感或模板可删。

## 2026-08-23 HUD 统计守卫升级

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮只做 CLI 静态对账，不启动 Unity，不把截图或历史 PlayMode 结果当作新鲜证据。
- `CardStatsUI` 三组 HUD 图标 / 数字统计已从辅助表现警告升级为默认阻塞项。`gameplay-static-preflight` 现在要求 StackCraft HUD 三张源图与自有副本 hash 一致、Unity 导入视觉参数一致，`ScenarioTurnPanel` 源码承接图标和数字标签，`FoundationTestSceneMenu` 稳定生成对应 Prefab，`ScenarioTurnPanel.prefab` 的具体对象 / 组件 / 字段引用与 `UIRoot.prefab` 来源对象级对齐。
- 新鲜验证：`node --check .spec/tools/gameplay-static-preflight.mjs` 通过，`node .spec/tools/spec-lint.mjs` 通过，`node --test .spec/tools/spec-lint.test.mjs` `2/2` 通过，`node .spec/tools/gameplay-static-preflight.mjs` 与 `--strict-auxiliary-parity` 均通过，`node .spec/tools/stackcraft-business-representative-audit.mjs` 通过，`node .spec/tools/unity-yaml-guard.mjs` 扫描 `15785` 个 Unity YAML 文件通过。
- 该结论只证明 HUD 统计表面已进入默认静态防回流，不等于 Unity 编译、最终画面、连续动画手感或模板可删。

## 2026-08-22 静态材质与贴图导入闭包对账口径

- 当前任务仍是 StackCraft 玩家可见效果复刻；本轮不启动 Unity，不用截图或测试冒充源码 / Prefab / Material / YAML 等价。
- 新鲜验证：`node --check .spec/tools/gameplay-static-preflight.mjs` 通过，`node .spec/tools/gameplay-static-preflight.mjs` 与 `--strict-auxiliary-parity` 均通过，`node .spec/tools/stackcraft-business-representative-audit.mjs` 通过，`node .spec/tools/spec-lint.mjs` 通过，`node --test .spec/tools/spec-lint.test.mjs` `2/2` 通过，`node .spec/tools/unity-yaml-guard.mjs` 扫描 `15776` 个 Unity YAML 文件通过。
- 卡牌表面材质对账已经从“存在同名材质 / 固定 Placeholder 覆盖图”升级为来源逐槽闭包：`_BaseTex`、`_MainTex`、`_OverlayTex` 都从 StackCraft 来源材质读取 GUID，反查来源贴图，再映射到 `Assets/Art/Sprites/StackCraft` 自有复制素材 GUID；自有贴图必须 hash 等同来源且不能复用来源 GUID。
- 已据此修正 7 个自有材质 `_MainTex` 误指向自身类别贴图的问题：生物、主动敌人、消耗品、装备、配方、贵重物、地区。
- YooAsset 收集器守卫已经从全文件 token 升级为 Collector 块字段级对账：`Assets/Art/Sprites/StackCraft` 自身块必须使用 `AddressByFolderAndFileName`；正式表现 Prefab、正式 UI Prefab、卡牌表面材质、卡包材质、交易区材质和测试占位卡图自身块必须使用 `AddressByFileName`、`PackDirectory`、`CollectAll` 和 `test` 标签。
- 复制贴图对账已经从“图片字节 hash 一致”升级为“图片字节 + Unity 导入视觉参数”闭包：`Assets/StackCraft/Textures` 下 133 张图片复制到 `Assets/Art/Sprites/StackCraft` 后，必须保持来源的 mipmap、边缘采样、非 2 次幂缩放、透明通道和压缩参数；项目自有 `.meta` 仍保留新 GUID、Sprite 导入类型和 Sprite fileID。
- 19 张已中文化并被测试内容直接引用的 StackCraft 卡图副本同步进入导入参数守卫：`Assets/Art/Sprites/CardArts/*.png` 必须保持来源图片 hash 和玩家可见导入参数一致，同时保留项目自有 GUID、`textureType: 8`、`spriteMode: 1` 和 Sprite 引用能力。
- 独立测试占位卡图同步进入导入参数守卫：`Assets/Art/Sprites/卡牌占位图.png` 不在 `Assets/Art/Sprites/StackCraft/**` 目录扫描内，但测试场景生成器直接消费它；当前按 StackCraft `Assets/StackCraft/Sprites/Square.png` 对账图片 hash、Unity 导入视觉参数、YooAsset 收集路径和生成器消费入口。
- 材质贴图副本同步进入导入参数守卫：`Assets/StackCraft/Textures/Backgrounds/Grass.png`、`Water.png` 和 `Puff.png` 分别迁入 `Assets/Art/Textures/草地背景.png`、`水面背景.png`、`卡牌烟雾.png` 后，必须同时保持文件 hash、mipmap / wrap / 压缩等视觉参数，以及来源同款非 Sprite 导入类型。
- 4 个 StackCraft ShaderGraph 副本同步进入元数据守卫：`Card`、`CardOutline`、`EquipmentPanel` 和 `SimpleLit` 必须保持 `.shadergraph` 文件 hash 一致、`ScriptedImporter` 脚本一致，同时使用项目自有 GUID；这只证明 Unity 按同类 ShaderGraph 导入，不证明最终材质画面。
- 材质、模型、Prefab 和 SO 引用继续从“GUID token 存在”升级为“字段 / 对象命中”。`m_Shader`、材质贴图槽、`Board.fbx.meta` 的 Body / Header 外部材质映射、`DamageLabel` 字体 / 材质、烟雾粒子 Renderer 材质、视图设置 `SoftAssetReference` 和音效资产 `m_audioClips` 都按具体字段或具体组件验证，防止同一文件里其它位置出现 GUID 时误判为已对齐。
- 组件级守卫已经删除通用 token 搜索入口，改为读取 Unity 组件字段：`m_Materials` 引用列表、`m_Mesh`、`m_Sprite`、`m_RenderMode`、`m_SortingOrder`、`playOnAwake`、卡牌视图 label / renderer 字段、镜头震动 `m_amplitude` / `m_duration` 等都必须命中目标对象的目标字段。源码派生的序列化标量和 YooAsset Collector 块也按字段值解析，不再用整文件或整块 `includes` 作为正向完成证据。
- 同态测试场景、打开卡包行动和战斗区域 Prefab 也已收口到字段 / 对象级：`FoundationStackCraftParityTest` 的剧本 ID 与 Starter 卡包开局读取 `FoundationTestSceneHarness` 组件字段；`地基打开卡包行动.asset` 检查内容 ID、点击启动、`pack` 槽、允许卡包内容、SerializeReference 结果意图和 `m_packSlotKey`；战斗区域 Prefab 检查目标对象上的视图脚本引用和 SpriteRenderer 颜色。
- 19 个 StackCraft 音频副本同步进入导入参数守卫：当前音频 `.meta` 已与来源一致，后续必须保持加载类型、采样率、压缩、预加载、后台加载和 3D 音频参数一致。
- 4 个 StackCraft fbx 模型同步进入导入参数守卫：当前模型文件 hash 和导入参数已与来源一致；`Board.fbx` 的内置 Body / Header 材质 remap 指向项目自有牌桌材质，这是必要适配，不视为偏差。
- 该结论只覆盖 CLI 静态材质 / GUID / hash / 导入参数对账，不等于 Unity 编译、场景回读、最终截图或连续动画手感完成。

## 2026-08-21 E2E 截图链恢复口径

- 当前任务仍是 StackCraft 玩家可见效果复刻，不切换到原创策划，也不直接复制模板旧结构。
- 本轮新鲜验证：`spec-lint` 通过，规范测试 `2/2` 通过，`gameplay-static-preflight` 与 `--strict-auxiliary-parity` 均通过，`unity-yaml-guard` 扫描 `15775` 个 Unity YAML 文件通过；UnitySkills editor-automation 预检通过。
- 单条 PlayMode `FoundationTabletop_CapturesE2EVisualEvidenceSequence` 已通过，UnitySkills job `20011c56` 返回 `1/1`；测试后 Unity 编译空闲，Console 错误数为 `0`。
- 该结果只证明统一测试场景的截图过程链可运行，并重新生成 6 张过程图；它不证明最终视觉 / 连续动画手感已与 StackCraft 完全一致，也不满足删除 `Assets/StackCraft` 的门槛。

## 2026-08-20 当前复刻口径

- 当前任务仍是用 CardLoop 自有 Gameplay 框架复刻 StackCraft 玩家可见业务效果；不得切换到原创策划，也不得用“类似实现”冒充一比一参数复刻。
- 本轮新鲜验证：`node .spec/tools/spec-lint.mjs` 通过，`node --test .spec/tools/spec-lint.test.mjs` `2/2` 通过，`node .spec/tools/gameplay-static-preflight.mjs` 通过，`node .spec/tools/unity-yaml-guard.mjs` 通过并扫描 `15749` 个 Unity YAML 文件；UnitySkills editor-automation 预检通过，Unity 编译 `0` 错误，Console `0` 错误 / `69` 警告，8 个关键复刻资产均可由 Unity 识别。
- 当前表面 / 动画状态：卡牌表面、HUD 图标、行动进度、命中 UI、投射物、烟雾、受击、镜头、收购点、商贩 / 卡包表面和解锁序列的源码 / Prefab / Material / 资源参数闭包已进入新框架；最终卡图比例、文字落点、连续动画手感和玩家试玩仍未验收。
- `Assets/Art/Sprites/箭矢投射物.png`、`Assets/Art/Sprites/魔法投射物.png` 已由正式 `Assets/Art/Prefabs/牌桌/投射物.prefab` 和测试 `Assets/Gameplay/Tests/牌桌/牌桌测试投射物.prefab` 引用，并进入 YooAsset 收集配置；此前“投射物素材未接入”的历史记录不得再作为当前事实。
- 删除 `Assets/StackCraft` 的门槛未满足：仍需要最终玩家观感确认、用户当轮删除授权，以及删除后的 Unity 编译、Console 和 PlayMode 验证。

## 2026-08-18 表面 / 动画一致性纠偏

- 当前“机制效果已通过”和“代表性业务竖切已通过”只覆盖规则、状态、参数、触发和自动化验证范围；它不覆盖卡面形状、材质分类、卡图比例、文字落点、进度条、命中 UI、拖拽手感、移动补间、受击闪烁 / 摇晃、投射物、烟雾粒子和镜头动画。
- 阶段 C 新增专项表面 / 动画审计入口：[`stackcraft-visual-animation-parity.md`](stackcraft-visual-animation-parity.md)。该文档按参考 Prefab / 材质 / 脚本证据和当前 Gameplay 视图证据逐项标记 `已对齐 / 部分 / 缺失 / 明确排除`。
- 在最终玩家观感未验收前，不得再用“和模板一致”“完整复刻”“模板可以删除”描述当前状态。正确说法是：机制和代表性业务已有证据，表面 / 动画参数闭包已完成当前登记项，但最终画面与连续手感仍需截图 / 录像或试玩确认。

## 2026-08-17 当前一致性审计口径

- 当前不能判定“StackCraft 模板全部业务内容已经完整迁移”。可判定的是：已纳入统一 Foundation / PlayMode 覆盖的 StackCraft 机制效果，已经由 Gameplay 自有框架接管并通过新鲜验证；Starter / Beginning 代表性业务竖切也已通过只读审计。
- 新鲜代码级预检：`node .spec/tools/gameplay-static-preflight.mjs` 通过；该预检扫描 `Assets`、`ProjectSettings`、`Packages` 的正式文本配置，确认正式链路没有回流 `Assets/StackCraft` 旧路径、`CryingSnow` 命名空间、`Resources.LoadAll` 入口、旧 Manager / DTO / UI 名称和旧模板资产 GUID。
- 新鲜 Unity 验证：`FoundationTestScenePlayModeTests` `26/26` 通过；全量 PlayMode `59/59` 通过；Unity 编译 `0` 错误，Console 在清理预期负向测试日志后 `0` 错误。
- 当前保留 `Assets/StackCraft` 的意义仍是参考对照，不是正式运行依赖。正式素材使用 `Assets/Art/Sprites/StackCraft` 及其它自有资源目录；这些复制素材使用项目新 GUID，不复用模板 GUID。
- 删除模板前已形成三张机制清单当前版，见本文“阶段 C 三张清单（当前版）”。清单只说明当前已登记的模板机制效果如何裁决；第一轮非破坏性静态删前审计已经由 `gameplay-static-preflight` 覆盖并通过。按当前“代表性业务验收”口径，剩余 StackCraft 原业务 `.asset` 是后续可选迁移范围，不阻塞当前阶段收口；真正删除 `Assets/StackCraft` 仍必须取得用户当轮授权，删除后必须重新跑 Unity 编译 / PlayMode 验证。

## 2026-08-17 StackCraft 业务数据全量对账订正

- 这次对账把“机制效果复现”“代表性业务验收”和“模板业务数据全量迁移”拆开：机制效果已有自动化覆盖，代表性业务已有只读审计，全量业务迁移不属于当前完成条件。
- StackCraft 原业务数据仍集中在 `Assets/StackCraft/Resources`：卡牌 `103`、卡包 `11`、配方 `90`、任务 `66`、遭遇 `3` 个 `.asset`。
- CardLoop 当前明确转换的是 Starter / Beginning 卡包竖切、相关卡牌 / 配方卡 / 商贩和若干地基测试内容；它们证明新框架能承接模板业务，不代表 273 个 StackCraft 原业务资产已经全量变成 CardLoop 作者源。
- 新鲜代表性业务审计：`node .spec/tools/stackcraft-business-representative-audit.mjs` 通过，覆盖 Starter 固定槽位、Beginning 三次打开槽位、权重、5 个配方候选、10% 配方概率和 Beginning 商贩价格 / 解锁任务数；该脚本只读参考资产和当前作者源，不启动 Unity，也不证明全量业务迁移。
- 因此当前正确结论是：机制地基已可继续承接模板业务，代表性业务验收已成立；模板业务尚未全量一致，但它已被明确降为后续可选迁移，不再阻塞当前阶段。下一步如果继续吸收更多 StackCraft 业务，应按业务数据迁移清单逐项转换，而不是继续扩原创《卡牌生存：无限》内容。

## 2026-08-17 StackCraft 基础图片素材迁入结论

- 本轮确认之前“完全复刻 / 模板可删”的口径过度：CardLoop 只是把少量 StackCraft 图片迁成地基测试卡图，并没有把 StackCraft 全部业务图片作为自有素材库接入。
- 已把 StackCraft `Assets/StackCraft/Textures` 下 133 张图片复制到 CardLoop 自有素材目录 `Assets/Art/Sprites/StackCraft`，补齐目录 `.meta`，并把图片导入设置统一改成 Unity Sprite；复制出的 `.meta` 使用新 GUID，不复用参考模板 GUID。
- YooAsset 当前配置新增 `Assets/Art/Sprites/StackCraft` 文件夹收集器，地址规则为 `AddressByFolderAndFileName`，避免 `Recipe.png`、`Grass.png` 等同名图片在不同子目录下发生地址冲突。
- 该迁入只说明“基础图片素材已进入 CardLoop 自有资源目录并可被 ResourceSystem / YooAsset 收集”。它不说明 StackCraft 的 Cards / Recipes / Packs / Quests 业务数据已经转换完成，也不允许 Gameplay 正式链路直接读取 `Assets/StackCraft`。
- 后续吸收 StackCraft 业务时，应把参考业务数据转换成 CardLoop 自己的 `CardDefinition`、`ActionDefinition`、`QuestDefinition`、`CardPackDefinition` 等作者源，并用这些迁入素材作为表现资源；不能继续把 Foundation 测试卡或模板原 SO 当成正式业务内容。

## 2026-08-17 Starter 卡包业务竖切吸收结论

- 本轮按“CardLoop 框架承接 StackCraft 业务”订正 Foundation 卡包竖切：运行时仍使用 `CardPackDefinition`、`OpenCardPackResultIntent`、`Tabletop`、`ScenarioRun`、`ResourceSystem` 和 YooAsset，不恢复 StackCraft `PackDefinition`、`CardManager`、`Resources.LoadAll` 或原 SO 直接读取。
- StackCraft `00_Pack_Starter.asset` 的业务事实已映射到当前 CardLoop 作者源：显示名 `Starter`，描述 `A Starter card pack.`，4 个固定槽位依次为 `Villager`、`Berry Bush`、`Rock`、`Wood`，每槽权重 1 / 100%。
- 对应卡牌从 StackCraft 原业务资产迁成 CardLoop 自有 `CardDefinition` / `FoodCardDefinition` / `CharacterCardDefinition`：`Villager`、`Wood`、`Berry`、`Berry Bush`、`Rock`、`Stone`、`Coin` 的显示名、描述、使用次数、营养和售价按参考资产写入当前测试作者源；模板本地战斗数值仍不回流，角色能力继续由 EX-GAS 接管。
- 新增中文自有卡图 `Assets/Art/Sprites/CardArts/浆果丛.png` 和 `Assets/Art/Sprites/CardArts/岩石.png`，内容字节分别等同 StackCraft `BerryBush.png` 与 `Rock.png`，但 `.meta` 使用项目新 GUID，并已加入 YooAsset 精确收集项。
- PlayMode 断言已从“两个测试奖励”改成四次打开 Starter：第一次生成 Villager，第二次生成 Berry Bush，第三次生成 Rock，第四次生成 Wood 并移除卡包。该改动只覆盖 Starter 包竖切，不代表 Beginning / 后续卡包、全部配方、任务链和商贩解锁序列已转换完成。

## 2026-08-17 Beginning 卡包业务竖切吸收结论

- 本轮继续按“CardLoop 自己框架承接 StackCraft 业务”吸收 `01_Pack_Beginning.asset`：正式运行链仍是 `CardPackDefinition`、`OpenCardPackResultIntent`、`PackVendorDefinition`、`Tabletop`、`ScenarioRun`、`ResourceSystem` / YooAsset，不恢复 StackCraft `PackDefinition`、`PackSlot.GetRandomCard`、`CraftingManager`、`CardManager.CreateRecipeCardDefinition` 或 `Resources.LoadAll`。
- StackCraft Beginning 包业务事实已映射到 CardLoop 作者源生成器：显示名 `Beginning`，描述 `A Beginning card pack.`，3 个抽取槽位；每个槽位普通卡权重为 Stone 16、Wood 16、Berry Bush 14、Rock 14、Soil 14、Tree 14、Chicken 4、Slime 4、Golden Key 4；每个槽位另有 10% 概率从尚未发现的配方候选中抽取配方卡。
- 5 个配方候选按模板动态配方卡规则静态化为 CardLoop 自有内容：`Growing Berry` / `Recipe: Berry Bush`、`Building House` / `Recipe: House`、`Making Love` / `Recipe: Baby`、`Making Timber` / `Recipe: Timber`、`Crafting Stick` / `Recipe: Wooden Stick`。配方卡描述按模板 `GetFormattedIngredients` 口径保留材料列表；真正配方执行仍留给后续行动 / 配方执行切片，不能因为出现配方卡就恢复旧制作系统。
- StackCraft `buyPrice = 3`、`minQuests = 3` 在 CardLoop 中不污染卡包商品本体，而是映射为 `PackVendorDefinition` 的 Beginning 卡包商贩，售价 3，解锁所需完成任务数 3；同一个购买行动槽位允许 Starter 商贩和 Beginning 商贩，不新增第二套购买逻辑。
- 新增中文自有素材 `开端卡包`、`土壤`、`树`、`鸡`、`史莱姆`、`金钥匙`、`鸡蛋`、`配方卡`，图片字节等同 StackCraft 对应素材，但 `.meta` 使用项目新 GUID，并由地基场景生成器加入 YooAsset 精确收集项。
- Beginning 生物业务改由 `CharacterCardDefinition` 接管，不恢复 StackCraft `CombatStats`。StackCraft `Chicken` 映射为中立生物 ASC 预设 `1005`：内容静态标签只写 `Faction`，不写 `Faction.Enemy`；保留 `produceCard = Egg`、`produceInterval = 30`、自动移动间隔 5 秒和半径 1。StackCraft `Slime` 映射为敌对生物 ASC 预设 `1006`：写入 `Faction.Enemy`，保留自动移动、`aggroRadius = 5`、`attackRadius = 1.5` 和主动敌对行为。
- StackCraft `Egg` 的 `nutrition = 0` 且描述为 `Cook it first before eating.`，因此在 CardLoop 中映射为普通 `CardDefinition`：保留售价 1、使用次数 1、卡图和容量计数，但不写成 `FoodCardDefinition`，避免把“不可直接吃的鸡蛋”纳入日终进食规则。
- `1005` / `1006` 是 EX-GAS ASC 作者源 `#exgas.asc.xlsx` 的正式预设，均只包含 FightUnit 属性集和基础攻击 Ability `20005`；两者都不带 `Combat.Melee` / `Combat.Ranged` / `Combat.Magic` 标签，以对齐模板 `combatType = None`，避免把训练假人或战斗克制类型误当 StackCraft 生物业务。
- StackCraft 生物战斗参数通过 `CharacterAttributeOverride` 覆盖 EX-GAS FightUnit 属性集：鸡为 Health/MaxHealth 5、Attack 1、Defense 1、AttackSpeed 100、Accuracy 95、Dodge 20、CriticalChance 5、CriticalMultiplier 150；史莱姆为 Health/MaxHealth 7、Attack 3、Defense 0、AttackSpeed 60、Accuracy 75、Dodge 5、CriticalChance 5、CriticalMultiplier 150。属性身份、钳制和运行时结算仍归 EX-GAS / GNS。
- 该切片只覆盖 Beginning 卡包打开、可购买定义、配方卡发现候选、鸡周期产蛋 / 移动、史莱姆敌对索敌和模板生物基础数值映射；不代表 Beginning 配方真实执行、全部卡牌行为、完整怪物 AI、任务解锁提示和最终玩家画面已经完成。

## 2026-08-16 卡牌悬浮信息 / 堆叠摘要吸收结论

- StackCraft 的 `CardInstance.GetInfo` 证明：玩家悬浮卡牌时，信息面板不只显示单卡名称和描述；如果整堆正在制作 / 行动，会显示当前配方名与剩余时间；如果同一堆有多张卡，会按卡牌定义聚合显示每种卡的数量；如果是角色卡，会追加当前生命、战斗类型和模板本地战斗数值。
- CardLoop 由现有 `TabletopCardInfoPanel` 接管上下文型悬浮信息，不恢复 StackCraft `InfoPanel` 优先级队列、`CraftingManager` 或旧 `CombatStats`。面板继续读取当前可读卡牌，并从所属 `TabletopCardStack` 追加牌堆聚合摘要，从 `Tabletop.ActiveActions` 与行动作者源追加进行中行动名、剩余回合和约合秒数。
- `ActionInstance` 仅提供参与卡和剩余回合的只读查询；`TabletopView` 的行动进度锚点与详情面板复用该入口，避免表现层各自扫描绑定并形成重复算法。行动状态、牌堆成员和结算仍归 `Tabletop` / `ActionInstance`，UI 不保存第二份状态。
- 角色生命仍由 `TabletopCardView` 直接显示。角色战斗明细如果后续要进详情文本，只能读取 GNS / EX-GAS 的正式公开属性，不能把 StackCraft 本地战斗数值系统迁回。
- 本轮已完成源码接入与静态校验；Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待独占后重建 `FoundationTest` 并补跑相关 PlayMode。

## 2026-08-16 卡包商贩解锁提示序列静态审计

- StackCraft 的 `TradeManager.PlayActivationSequence` 证明：卡包商贩因完成任务数达标而解锁时，模板会临时锁输入、暂停时间、显示 `Pack Unlocked` 提示、镜头移动到商贩位置，并让商贩高亮约 2 秒。这是玩家“知道新商贩已可用”的反馈序列，不只是购买结算。
- CardLoop 现有 `PackVendorDefinition` / `PackVendorCard` / `PurchaseCardPackResultIntent` 已覆盖解锁门槛、分批付款、满价生成卡包、付款归零、收藏进度和购买任务事实；`TabletopCardInfoPanel` 也能在玩家查看商贩时显示未解锁进度或已解锁后的售价 / 收藏状态。
- 当前已吸收可在正式 owner 内表达的玩家反馈：`ScenarioRun` 在任务事实刷新后比较完成任务数是否跨过 `PackVendorDefinition.MinimumCompletedQuests`，对刚解锁的当前牌桌商贩发布 `ScenarioSequencePresentationRequestEvent`，请求中包含 `Pack Unlocked` 文本、商贩牌桌坐标和局内卡牌 ID。
- 解锁序列由 `ScenarioDirector` 按队列串行播放，而不是由 `ScenarioRun` 直接操作 UI / 输入 / 时间；播放期间通过 `GameCore.InputSystem.AddGameplayInputLock` 和 `GameCore.GameStateSystem.AddExternalPauseLock` 接管 Gameplay 输入与世界时间，2 秒后释放。
- 文本提示由 `ScenarioSequenceMessageEvent` 发布给 `TabletopCardInfoPanel`，详情面板在提示持续期间用 `Pack Unlocked` 覆盖悬浮 / 选中卡牌详情，到期后恢复当前可读卡牌；正文按模板 `You can buy {PackName} card pack here.` 从出售卡包 `CardPackDefinition.DisplayName` 生成。不恢复模板 `InfoPanel` 全局单例 / 请求字典，也不保存第二份 `isUnlocked`。
- 镜头聚焦继续由 `TabletopPresentationCueKind.CameraFocus` 和 `TabletopCameraController` 承接；商贩高亮由 `CardHighlight` 和 `TabletopCardView.ShowPresentationHighlight(2f)` 承接。牌桌拖拽输入和镜头只读取正式 Gameplay 输入锁，不新增 Gameplay 自己的锁状态。
- 本轮静态守卫覆盖 `ScenarioRun` 请求、`ScenarioDirector` 队列 / 输入锁 / 暂停锁 / UI 消息 / 镜头 / 高亮、`TabletopCardInfoPanel` 流程提示优先级、`GameCore.InputSystem` 正式 Gameplay 输入锁和 `GameCore.GameStateSystem` 正式外部暂停锁；Unity 自动化仍需等待同工程独占后补跑相关编译和 PlayMode。

## 2026-08-16 菜单新内容标记吸收结论

- StackCraft 的 `MenuView` / `RecipesView` / `QuestsView` 证明：任务和配方列表除了“可查看并刷新”外，还提供新内容红点、首次悬浮后标记已读、任务分组折叠、配方分类折叠和已完成任务勾选。
- CardLoop 由 `ScenarioRun` 拥有日志条目已读事实：任务和已发现配方 / 行动使用各自唯一内容 ID 标记已读，并写入 `ScenarioRunSnapshot` 随单局保存 / 恢复。
- `ScenarioJournalPanel` 只做 UI 投影：任务页读取 `QuestLog`，配方 / 行动页读取本局发现集合；未读条目在正文和隐藏页签上显示红点，当前可见页刷新后标记本页条目已读。
- 当前不恢复 `QuestGroup`、`RecipeCategory`、`MenuView`、旧 `TextButton` 或旧 `GameData.SeenItems`。任务分组和配方分类折叠依赖旧枚举 / 旧 Manager 结构，已经被项目任务作者源与行动作者源替代，不能作为正式职责回流。
- 已补单局快照回归和日志面板 PlayMode 断言；Unity 自动化仍需按 guard 补跑新鲜编译 / 测试后，才能把该切片标为验证完成。

## 2026-08-16 GameplayPrefsUI 日长滑条吸收结论

- StackCraft 的 `GameplayPrefsUI` 与 `Title.unity` 证明：标题页新局前有整天持续秒数滑条，范围 `60-180` 秒、默认 `120` 秒；开始新游戏时把滑条整数值写入 `GameplayPrefs.DayDuration`，`TimeManager` 再把它作为整天时长使用。
- CardLoop 不恢复模板 `GameplayPrefs` 数据类、`TimeManager`、固定 `Title` 场景或旧标题 UI 结构。正式接管方式是 `ScenarioStartOptions.DayDurationSecondsOverride`：它是本局启动选项，随单局快照保存，不替代 `ScenarioDefinition.SecondsPerTurn` 这个剧本作者源默认值。
- `ScenarioRun` 在创建 / 读档时把“整天秒数覆盖值”除以 `ScenarioDefinition.TurnsPerDay`，得到本局 `SecondsPerTurn`；这样标题入口仍给玩家设置整天长度，而单局内部继续只维护每回合秒数，不产生第二套时间真相。
- 标题测试场景生成器已按模板参数生成中文“日长”滑条；新增 EditMode 回归覆盖日长换算和快照恢复，新增 PlayMode 回归覆盖标题滑条新局传参。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑。

## 2026-08-16 InfoPanel / MenuToggle 菜单焦点吸收结论

- StackCraft 的 `InfoPanel` 证明模板有“悬浮信息、流程提示、强制确认按钮”三类信息请求，并用全局单例、请求者字典和优先级决定最终显示；它证明的是玩家需要可读反馈，不证明 CardLoop 需要恢复一个全局信息仲裁面板。
- CardLoop 不恢复 `InfoPanel`、`InfoPriority`、`InfoRequest`、`TextButton` 操作按钮或 `MenuView` 里的悬浮信息转发。卡牌悬浮信息和非强制流程提示已由 `TabletopCardInfoPanel` 接管，且流程提示优先级高于悬浮 / 选中卡牌；任务 / 配方已由 `ScenarioJournalPanel` 接管，日终 / 确认类提示由 `ScenarioTurnPanel` 和现有确认弹窗接管；这些 UI 都只读各自正式 owner，不保存第二份信息状态。
- StackCraft 的 `MenuToggle` 证明日终开始时左侧菜单会自动收起，避免菜单遮挡日终流程。CardLoop 不恢复 DOTween 抽屉动画、`targetRect` 手填位移或 `TimeManager.OnDayEnded`；等价效果是 `ScenarioJournalPanel` 订阅当前单局日终阶段事件，进入非空闲日终阶段时关闭自身，把焦点交还给日终 HUD。
- 已新增 PlayMode 回归 `ScenarioJournalPanelPlayModeTests.JournalClosesWhenDayCycleTakesOver`，用正式 `ScenarioDayCycleChangedEvent` 验证日志面板在日终阶段关闭。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑。

## 2026-08-16 TitleScreen / SavedGamesUI 标题入口吸收结论

- StackCraft 的 `TitleScreen` 只是标题页四个玩家命令的粘合层：新游戏打开开局偏好，读取打开存档列表，设置打开选项面板，退出打开确认弹窗。CardLoop 不恢复旧标题 MonoBehaviour、旧 `TextButton` 或旧 UI 面板引用；正式入口是 `ScenarioTitleScreen` 打开 UIKit `ScenarioTitlePanel`，再把请求交给 `ScenarioDirector`、`ScenarioSavePanel`、`UISettings` 和通用确认弹窗。
- `SavedGamesUI` / `SavedGameSlot` / `ModalWindow` 证明模板有动态存档槽位、读取、删除单槽、删除全部、关闭和确认弹窗。该玩家流程已由 `ScenarioSavePanel`、`ScenarioSaveSlotView`、GameCore `SaveSystem` / SaveKit 和 `ConfirmationDialogPanel` 接管；槽位事实直接来自正式存档容器，不从 UI 列表保存第二份槽位状态。
- 退出确认和删除确认都继续走 UIKit 对话框队列，不恢复模板 `ModalWindow` 的回调单例；标题页设置入口继续走当前 `UISettings` 和 `DisplaySettingsSystem` / `AudioSystem`，不接回模板 `GameOptionsUI` 结构。
- 已有 PlayMode 回归覆盖标题四命令、设置 / 退出确认、标题新局、友好模式、日长滑条、动态存档列表、读取、删除、清空全部和保存退出。2026-08-16 后的日长滑条与日志焦点切片仍需等待 Unity 独占后补跑对应 PlayMode。

## 2026-08-16 嵌套值对象与局部类型收口结论

- `LootEntry` 是 `CardDefinition` 内部的加权掉落条目；玩家效果已经由当前行动 / 遭遇 / 卡包等正式产出链分开承接，不把旧卡牌定义上的通用掉落表迁成第二套产出系统。后续若需要怪物掉落，应作为行动结果或剧本规则作者源单独裁决。
- `AnimatedEquipment` 只是旧 `EquipmentPanel` 内部的装备卡漂浮动画缓存；装备事实已由 `CharacterCard` 拥有，装备可读反馈由 `TabletopCardInfoPanel` 投影，不恢复漂浮装备卡状态。
- `HitType`、`CombatTypeAdvantage`、`CombatState` 和 `HitResult` 是模板战斗任务内部的命中反馈 / 回合执行值对象。命中、暴击、优势 / 劣势反馈已经映射到 `TabletopCardView` 与 GNS / EX-GAS 战斗链，不新增同名枚举作为正式数值真相。
- `QuestGroup` 只是旧 `QuestManager` 的 Inspector 分组；当前任务作者源和运行时进度由 `QuestTaskDefinition`、`QuestLog` 和单局 `ScenarioRun` 承担，不恢复进程级任务分组 Manager。
- `QuestData`、`VendorData`、`TimeData` 和 `GameplayPrefs` 是旧 `GameData` 的保存 DTO；当前对应事实分别在整局 `ScenarioRunSnapshot`、商贩卡派生状态、回合 / 日程快照和 `ScenarioStartOptions`。这些 DTO 不单独迁入。
- `ShadowPreset` 是模板 `GraphicsManager` 内部设置枚举，已由当前 `DisplaySettingsSystem` 接管；`CustomPass` 是模板 RendererFeature 的内部 URP pass，已由 `ScenarioScreenEffectView` + Volume 接管；`Styles` 只是旧堆叠矩阵编辑器 GUI 样式，随旧矩阵编辑器一起排除。

## 2026-08-16 未登记辅助类与作者工具吸收结论

- 本轮静态复扫了 StackCraft 脚本名覆盖情况，未在矩阵中点名的类主要是旧模板作者工具、空类型标记、局部值对象和接口，不是新的玩家运行效果；因此不能因为“没点名”就默认新增 Gameplay 模块。
- `ChestDefinition` 的现实含义是箱子存币容量，已由 `ChestCardDefinition` / `ChestCard` 的容量和当前存币状态接管；不迁入旧 `CardDefinition` 派生类。
- `GrowerDefinition` / `ResearchDefinition` 只是空类型标记，模板用 C# 类型判断行为。CardLoop 不吸收这种“空类即规则”做法；种植 / 研究效果必须通过正式 `CardDefinition` 字段、`ActionDefinition` 作者源和行动结算链表达。
- `PackEntry` 的现实含义是卡包普通卡池的加权条目，已由 `CardPackEntry` 接管。权重在新框架中是相对权重，不要求归一化到 100；模板 `Normalize (100%)` 按钮只作为作者体验参考，不成为第二套权重真相。
- `StatType`、`Stat`、`IStatModifier`、`StatModifier` 是 StackCraft 本地战斗数值和装备修正系统，已明确排除；正式属性、能力、持续效果和装备修正归 GNS / EX-GAS 与角色卡唯一 ASC。
- `IClickable` / `IOnStackable` 只是模板旧输入直接调用卡牌组件的接口。CardLoop 正式入口是 `TabletopInteraction`、行动候选、行动请求和牌桌原子提交，不恢复点击 / 堆叠接口绕过行动链。
- `InputManager` 的现实含义是全局输入锁，已由 `GameCore.InputSystem`、`GameStateSystem.Menu` 和正式 UI / 过场状态承担；不新增第二个输入锁集合。
- `VectorExtensions.Flatten` 只是把 `Vector3.y` 置零的局部数学 helper，不拥有领域职责；需要时在具体算法内显式处理，不为它建立项目公共工具入口。
- StackCraft 的 `AudioDataDrawer`、`CategoryEntryDrawer`、`CardDefinitionEditor`、`ChestDefinitionEditor`、`EnclosureDefinitionEditor`、`GrowerDefinitionEditor`、`LimitBoosterDefinitionEditor`、`ResearchDefinitionEditor`、`QuestEditor`、`QuestManagerEditor`、`RecipeDefinitionEditor`、`PackDefinitionEditor`、`StackingRulesMatrixEditor` 和 `EncounterDefinitionEditor` 大多服务旧枚举、旧 SO 字段和旧 Manager 结构。CardLoop 不迁入这些 Editor 脚本；可吸收的作者体验已经落到 Odin 中文标签、类型受限 `ContentIdReference`、内容校验、自动局部 key 和现有 SO Inspector。若后续需要卡包权重归一化或配方冲突可视化，应在当前作者源上做专用工具，而不是复活旧编辑器。
- `RenderPipelineSwitcher` 直接改 `GraphicsSettings` 和全部 Quality 渲染管线，属于模板工具按钮，不是 Gameplay 地基职责；CardLoop 渲染管线以项目设置和 URP 正式资产为准。

## StackCraft 源脚本显式覆盖索引（2026-08-23）

以下 marker 是静态预检的精确来源覆盖表。它只证明每个 StackCraft 源脚本已进入本矩阵裁决视野，不证明对应玩家效果已经最终视觉验收；具体吸收 / 排除理由仍以前文各模块结论为准。

```text
stackcraft-script:Assets/StackCraft/Scripts/Card/Behaviors/ChestLogic.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Behaviors/EnclosureLogic.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/CardInstance.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/CardManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/CardPhysicsSolver.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/CardSettings.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/CardStack.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Components/CardAI.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Components/CardCombatant.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Components/CardController.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Components/CardEquipment.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Components/CardEquipper.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/CardDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/ChestDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/Editor/CardDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/Editor/ChestDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/Editor/EnclosureDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/Editor/GrowerDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/Editor/LimitBoosterDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/Editor/ResearchDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/EnclosureDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/GrowerDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/LimitBoosterDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Definitions/ResearchDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Interfaces/IClickable.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/Interfaces/IOnStackable.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/StackingRulesMatrix.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/UI/EquipmentPanel.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/VFX/Highlight.cs
stackcraft-script:Assets/StackCraft/Scripts/Card/VFX/PuffParticle.cs
stackcraft-script:Assets/StackCraft/Scripts/Combat/CombatManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Combat/CombatStats.cs
stackcraft-script:Assets/StackCraft/Scripts/Combat/CombatTask.cs
stackcraft-script:Assets/StackCraft/Scripts/Combat/HitResult.cs
stackcraft-script:Assets/StackCraft/Scripts/Combat/UI/CombatProjectile.cs
stackcraft-script:Assets/StackCraft/Scripts/Combat/UI/CombatRect.cs
stackcraft-script:Assets/StackCraft/Scripts/Combat/UI/HitUI.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/AudioManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/Board.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/CameraController.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/DayCycleManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/GameDirector.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/GraphicsManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/InputManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/StackCraftInput.cs
stackcraft-script:Assets/StackCraft/Scripts/Core/TimeManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Crafting/CraftingManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Crafting/CraftingTask.cs
stackcraft-script:Assets/StackCraft/Scripts/Crafting/Definitions/ExplorationRecipe.cs
stackcraft-script:Assets/StackCraft/Scripts/Crafting/Definitions/GrowthRecipe.cs
stackcraft-script:Assets/StackCraft/Scripts/Crafting/Definitions/RecipeDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Crafting/Definitions/ResearchRecipe.cs
stackcraft-script:Assets/StackCraft/Scripts/Crafting/Definitions/TravelRecipe.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/AudioDataDrawer.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/CategoryEntryDrawer.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/PackDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/QuestEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/QuestManagerEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/RecipeDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/RenderPipelineSwitcher.cs
stackcraft-script:Assets/StackCraft/Scripts/Editor/StackingRulesMatrixEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Encounter/Editor/EncounterDefinitionEditor.cs
stackcraft-script:Assets/StackCraft/Scripts/Encounter/EncounterDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Encounter/EncounterManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Extensions/VectorExtensions.cs
stackcraft-script:Assets/StackCraft/Scripts/Pack/PackDefinition.cs
stackcraft-script:Assets/StackCraft/Scripts/Pack/PackEntry.cs
stackcraft-script:Assets/StackCraft/Scripts/Pack/PackInstance.cs
stackcraft-script:Assets/StackCraft/Scripts/Pack/PackSlot.cs
stackcraft-script:Assets/StackCraft/Scripts/PostProcess/BuiltInPostProcess.cs
stackcraft-script:Assets/StackCraft/Scripts/PostProcess/CustomPostProcessFeature.cs
stackcraft-script:Assets/StackCraft/Scripts/Quest/Quest.cs
stackcraft-script:Assets/StackCraft/Scripts/Quest/QuestInstance.cs
stackcraft-script:Assets/StackCraft/Scripts/Quest/QuestManager.cs
stackcraft-script:Assets/StackCraft/Scripts/SaveSystem/GameData.cs
stackcraft-script:Assets/StackCraft/Scripts/SaveSystem/SaveSystem.cs
stackcraft-script:Assets/StackCraft/Scripts/Stats/IStatModifier.cs
stackcraft-script:Assets/StackCraft/Scripts/Stats/Stat.cs
stackcraft-script:Assets/StackCraft/Scripts/Stats/StatModifier.cs
stackcraft-script:Assets/StackCraft/Scripts/Stats/StatType.cs
stackcraft-script:Assets/StackCraft/Scripts/Trading/CardBuyer.cs
stackcraft-script:Assets/StackCraft/Scripts/Trading/PackVendor.cs
stackcraft-script:Assets/StackCraft/Scripts/Trading/TradeManager.cs
stackcraft-script:Assets/StackCraft/Scripts/Trading/TradeZone.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/CardStatsUI.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/DayTimeUI.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/GameOptionsUI.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/InfoPanel.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Menu/MenuToggle.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Menu/MenuView.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Menu/QuestsView.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Menu/RecipesView.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/ModalWindow.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/PauseMenu.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/ProgressUI.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/ScreenFader.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/TextButton.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Title/GameplayPrefsUI.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Title/SavedGameSlot.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Title/SavedGamesUI.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/Title/TitleScreen.cs
stackcraft-script:Assets/StackCraft/Scripts/UI/WorldCanvas.cs
```

## 2026-08-16 EquipmentPanel 装备可读反馈吸收结论

- StackCraft 的 `EquipmentPanel` 证明模板在装备卡离桌后仍向玩家展示已装备物品：面板可悬浮查看装备列表，可点击展开装备卡，并在日终期间禁止交互。
- CardLoop 不恢复模板 `EquipmentPanel`、装备卡漂浮动画、`InfoPanel` 请求字典、`Camera.main` 点击检测、装备面板材质槽位或 `TimeManager` 日终锁。装备运行事实仍只由 `CharacterCard` 拥有，装备 / 卸装继续走正式行动结算和 EX-GAS GameplayEffect。
- 当前正式玩家可读入口是 `TabletopCardInfoPanel`：选中或悬浮角色卡时，从角色只读装备事实枚举当前装备，按装备槽位与装备卡作者源显示“已装备”列表。UI 不保存第二份装备状态，也不直接修改装备。
- 已有装备 EditMode 合同补充只读装备枚举断言；统一 Foundation PlayMode 仍需等 Unity 独占后补跑。完整角色侧栏和装备位布局属于后续原创 UI，不用模板面板结构冒充。
## 2026-08-16 WorldCanvas / 候选高亮吸收结论

- StackCraft 的 `WorldCanvas` 只证明模板有一个世界空间 Canvas，并在 `Awake` 里把 `Canvas.worldCamera` 设为 `Camera.main`；这不证明 CardLoop 需要恢复全局 `WorldCanvas.Instance` 或运行时查找主相机。
- CardLoop 不恢复 `WorldCanvas` 单例、`Camera.main` 依赖或额外世界 UI 根。正式 owner 是当前 `TabletopView` 自身 Transform 和各自 UIKit / Canvas 作者入口：牌桌卡牌、行动进度、战斗区域、投射物和卡牌烟雾都挂在绑定的牌桌视图下，屏幕 UI 仍归 UIKit。
- StackCraft 的 `Highlight` 证明卡牌可接受拖拽目标时会显示一个高亮子物体。CardLoop 已由 `TabletopCardDragInput` 更新候选目标，`TabletopView.SetDropTargetHighlight` 切换对应 `TabletopCardView` 的中文子节点“候选高亮”；该高亮是本地表现状态，不进入规则、存档、联机或第二事件链。
- 统一测试场景生成器已在中文卡牌视图预制体中生成“候选高亮”子节点，PlayMode 已覆盖拖拽到候选卡牌时高亮开启、释放后关闭。当前无需新增代码；待 Unity 独占后随 FoundationTest 回归补跑。
## 2026-08-16 暂停灰阶 / 日终暗角后处理吸收结论

- StackCraft 的 `BuiltInPostProcess` / `CustomPostProcessFeature` / `CustomPostProcess.shader` 证明模板有两类全屏反馈：暂停时 `0.3s` 淡入灰阶，恢复时 `0.3s` 淡出；跨日结束时 `0.5s` 淡入暗角，新一天开始时 `0.5s` 淡出。
- CardLoop 不恢复模板 `TimeManager`、DOTween、`OnRenderImage`、自定义 RendererFeature、模板 Shader / Material 或全局后处理单例。正式 owner 是 `ScenarioScreenEffectView`：它只读取 `GameStateSystem.Menu` 与当前 `ScenarioRun.DayCyclePhase`，不保存第二份暂停、天数或菜单状态。
- 渲染实现改用项目当前 URP 能力：运行时根挂全局 `Volume`，Profile 含 `ColorAdjustments` 与 `Vignette`；主相机开启 URP 后处理。缺少 Volume、Profile 或必要 override 时直接中文报错，不运行时静默补默认资源。
- `FoundationTest` 生成器会生成中文资源 `Assets/Gameplay/Tests/牌桌/剧本屏幕效果配置.asset` 与场景对象“剧本屏幕效果”；PlayMode 合同已覆盖暂停菜单灰阶进入 / 恢复、日终阶段暗角进入 / 新日恢复。当前 Unity 自动化被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑。

## 2026-08-25 ScreenFader / TravelSequence 静态吸收守卫

- StackCraft 的 `ScreenFader` 真实职责是 `CanvasGroup` 全屏淡入淡出；真实消费点在 `GameDirector.TravelSequence`，顺序为外部暂停、淡出、`SceneManager.LoadSceneAsync`、解除暂停、淡入。
- CardLoop 不恢复 `ScreenFader.Instance`、模板 `TimeManager.SetExternalPause` 或正式代码里的直接 `SceneManager.LoadSceneAsync`。同职责被拆给两个正式 owner：`SceneSystem` 编排整次技术场景切换，`TransitionSystem` 只持有淡出 / 淡入表现状态；剧本开局、结束、读档和旅行都通过 `ScenarioDirector -> GameManager.SceneSystem.TransitionToAsync` 进入这条链。
- 局内暂停灰阶与日终暗角不属于场景切换淡入淡出，继续由 `ScenarioScreenEffectView` 读取 `GameStateSystem.Menu` 和 `ScenarioRun.DayCyclePhase` 后投影到全局 `Volume`，不新增第二个 fader 系统。
- `gameplay-static-preflight` 已新增源码和 Prefab / Profile 守卫：读取 StackCraft `ScreenFader` 与 `GameDirector.TravelSequence` 作为来源，要求 `SceneSystem` 调用 `TransitionSystem.FadeOut/FadeIn` 与 `SceneKit.LoadSceneUniTaskAsync`，要求 `FoundationTestRuntimeRoot.prefab` 实际挂 `SceneSystem`、`TransitionSystem`、`ScenarioDirector`、全局 `Volume` 和 `ScenarioScreenEffectView`，并要求 `剧本屏幕效果配置.asset` 含 `ColorAdjustments` / `Vignette`。
- 本条只证明源码、Prefab 和 Profile 静态对账；不证明 Unity 编译、PlayMode、最终截图或连续转场手感已经完成。

## 2026-08-16 设置面板吸收结论

- StackCraft 的 `GameOptionsUI`、`GraphicsManager` 和 `AudioManager` 证明模板设置面板包含五类图形按钮：分辨率、全屏、垂直同步、帧率上限、阴影预设；两类音量入口：SFX 与 BGM；以及 Reset 和 Close。
- CardLoop 不恢复模板 `GraphicsManager` 单例、`AudioManager` 单例、模板 `PlayerPrefs` 键、模板文本按钮体系或 `PlayerPrefs.DeleteAll()`。`DeleteAll` 会误删存档、Mod 配置和其它系统偏好，不能作为正式重置语义。
- 正式 owner 是进程级 `DisplaySettingsSystem`、现有 `AudioSystem` 和 UIKit `UISettings`：显示设置由 `DisplaySettingsSystem` 读写自己的偏好键并应用到 `Screen` / `QualitySettings` / 当前渲染管线；音频通道继续由 `AudioSystem` 管理；面板只做按钮映射和文案刷新。
- `DisplaySettingsSystem` 同时接管模板 `GraphicsManager.Update` 中的 `_UnscaledTime` Shader 全局值，避免为了一个 Shader 变量恢复模板全局单例。
- Reset 通过现有确认弹窗执行，只清理显示设置和音频系统拥有的键；Close 走现有菜单栈返回，不直接隐藏一套独立 GameObject 状态。
- 测试场景生成器已补设置面板按钮、背景音乐 / 玩法音效 / 界面音效通道和运行时根 `DisplaySettingsSystem`。当前 Unity 自动化仍被同工程 Unity / ShaderCompiler 进程与 `Temp/UnityLockfile` 阻塞，待环境独占后重建场景并补跑 PlayMode。

## 2026-08-16 日程 HUD 推进模式入口吸收结论

- 2026-08-25 订正：StackCraft 的 `DayTimeUI` 证明模板存在“玩家点击日程 HUD 切换时间速度”的操作入口；当前由 `ScenarioRun.ScenarioTimePace` 在单局内承接 `Paused / Normal / Fast` 三档。它不恢复模板 `TimeManager` 单例，不写全局 `Time.timeScale`，只影响本单局实时普通行动推进秒数。
- `ScenarioTurnPanel` 的 StackCraft HUD 主按钮在实时模式下调用 `ScenarioRun.CycleTimePace()`，并用三张 StackCraft `TimePace_0/1/2` 图标反馈当前档位；暂停档停止本单局实时推进，加速档按 2 倍推进。回合制和日终阶段显示暂停图标，但不把它解释成可手动推进速度。
- `ActionProgressionMode.TurnBased / RealTime` 仍是 CardLoop 为普通行动保留的推进模式切换事实，不等于 StackCraft `DayTimeUI` 速度档。当前 `ScenarioTurnPanel.prefab` 的 StackCraft HUD 不再序列化单独的推进模式按钮；旧 `ProgressionMode` 按钮记录只能作为历史切片，不能再证明当前 HUD 主入口。
- 战斗仍始终按真实秒数推进，不受普通行动 `ScenarioTimePace` 或 `ActionProgressionMode` 影响。
- 当前静态守卫已覆盖 `ScenarioRun.ScenarioTimePace`、`CycleTimePace()`、速度倍率、禁止 `Time.timeScale`、三张速度图标、HUD 点击链、日终隐藏和 `UINavigationTarget` 点击音效；这些只证明源码 / Prefab 静态对账，不证明 Unity 编译、PlayMode、最终截图或完整模板复刻完成。
## 2026-08-16 任务 / 配方菜单吸收口径订正

- StackCraft 的 `QuestsView` / `RecipesView` 证明模板存在两项菜单玩家效果：查看当前任务进度，以及查看已发现配方；状态变化后菜单会刷新。
- CardLoop 不恢复 `MenuView`、`QuestManager`、`CraftingManager`、配方分类折叠 UI 或旧文本按钮体系。正式 owner 是当前单局 `ScenarioRun` 和 UIKit 的 `ScenarioJournalPanel`：任务读取同一 `QuestLog`，配方 / 行动读取本局发现集合，未读提示读取并回写同一单局快照事实。
- 当前地基里配方和可执行交互统一由 `ActionDefinition` 表达，因此 UI 语义改为“已发现配方 / 行动”，不把 StackCraft 的 Recipe 类层级重新并入正式链路。
- 本轮不新增新 UI 系统、不保存第二份任务或发现状态；红点只是 `ScenarioRun` 已读事实的投影，当前可见页刷新后标记可见条目已读。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 牌桌相机平移 / 缩放 / 聚焦吸收结论

- StackCraft 的 `CameraController` 证明模板存在三项牌桌相机玩家效果：中键拖拽平移、鼠标滚轮缩放，以及遭遇 / 解锁等空间反馈时聚焦到目标牌桌位置。
- CardLoop 不恢复模板 `CameraController`、旧输入读取、`Board` 单例依赖或 DOTween 镜头链。正式 owner 是主相机上的 `TabletopCameraController`：输入只消费 `GameCore.InputSystem` 的 `MiddleClick` 和 `ScrollWheel`，牌桌边界只读取当前 `Tabletop.PlacementRules`。
- 空间聚焦不由 `TabletopView` 播放，也不新建相机系统。规则 / 剧本只提交只读 `TabletopPresentationCueKind.CameraFocus` 和牌桌坐标，`TabletopCameraController` 订阅当前绑定牌桌后执行镜头目标移动。
- 命中震动继续归现有 `CameraShake`；它已避免震动开始时重置相机 XY，防止玩家平移后的牌桌视角被命中反馈拉回原点。
- `FoundationTest` 生成器已把唯一 `TabletopView` 写入主相机 `TabletopCameraController` 并保存后回读校验；PlayMode 回归已新增正式中键平移和 `CameraFocus` 聚焦断言。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 日终遭遇提示文本吸收结论

- StackCraft 的 `EncounterDefinition.NotificationMessage` 与 `EncounterManager.ExecuteEncounter` 证明：遭遇不只是生成卡牌，也可以向玩家展示一段事件提示文本；为空时才静默生成。
- CardLoop 不恢复 `EncounterManager`、`InfoPanel`、协程等待或独立遭遇系统。正式 owner 仍是剧本单局：`ScenarioDayEncounterRule` 声明提示文本，`ScenarioRun` 在日终遭遇提交时冻结到 `ScenarioDayEncounterResult`，`ScenarioTurnPanel` 显示提示文本和生成摘要。
- 日终遭遇筛选仍沿用现有正式规则：最早 / 最晚 / 间隔覆盖模板的 SpecificDay、Recurring、Range、MinimumDay；优先级先比作者优先级，再比具体性；一次性记录、友好模式敌对过滤、牌桌卡牌上限和权威随机概率仍由 `ScenarioRun` 执行。
- 现有 `FoundationTest` 日终测试剧本已写入遭遇提示文本，测试场景生成器和 PlayMode 断言同步更新。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 Board / LimitBooster 动态牌桌边界吸收结论

- StackCraft 的 `Board` 监听卡牌统计快照：`TotalBoost` 变化时通过 BlendShape 扩大或缩小桌面视觉，并在收缩时调用 `CardManager.EnforceBoardLimits()` 把现有牌堆拉回新边界。`LimitBoosterDefinition` 的默认 `boostAmount` 为 `4`，玩家效果是“这张卡同时提高卡牌容量，并扩大可摆放桌面”。
- CardLoop 不恢复 `Board` 单例、SkinnedMeshRenderer / BlendShape 权威逻辑、`CardManager` 或 Transform 反写状态。正式 owner 是当前地区的 `Tabletop`：基础边界仍来自剧本 / 地区作者源，当前边界由桌面上所有卡牌的 `CardDefinition.CardLimitBonus` 派生。
- 2026-08-23 回审订正：StackCraft 玩法放置边界必须按 `Board.cs` 运行时的 `BakeMesh + RecalculateBounds` 口径，从 `Board.fbx` 基础网格派生为 `12 × 8`，不是 Prefab 中 `SkinnedMeshRenderer.m_AABB` 的保守渲染包围盒 `48 × 32`。`Scale` BlendShape 权重 100 时网格从 `12 × 8` 扩到 `24 × 16`，所以每 1 点 `TotalBoost` 的单侧扩展为 X `0.06`、Y/Z `0.04`；`Board.HandleStatsChanged` 对桌面形变使用 `Mathf.Min(stats.TotalBoost, 100)`，因此 CardLoop 的放置边界扩张也封顶 100 点，但剧本卡牌容量统计仍读取完整 `CardLimitBonus`。静态预检现在直接从 FBX 顶点和 BlendShape delta 派生扩展比例，并守卫扩张上限。
- 2026-08-23 再订正：StackCraft 的顶部页眉禁放区不是固定世界区域，它跟随 `Board` 扩展后的 `currentBounds.max.z - topMargin` 重新定位。CardLoop 的 `CreateForCardLimitBonus()` 现在扩展牌桌边界时，也同步把贴顶且横跨全宽的页眉禁放区上移并横向扩宽；其它内部禁放区保持原世界位置，避免把障碍物误当作页眉。
- `Tabletop.PlacementRules` 现在返回当前派生放置规则；新增或移除卡牌后刷新边界，收缩时复用 `TabletopCards.ReflowPlacement()` 和既有放置解算把牌堆重新放回有效区域。剧本日终容量统计也读取 `tabletop.CardLimitBonus`，避免剧本和牌桌各算一套加成。
- 当前规则边界与玩家可摆放范围已经按 StackCraft 运行时网格口径吸收；视觉桌面底板仍使用 StackCraft Prefab 的保守渲染包围盒作为渲染安全范围，不得把渲染 AABB 反推成玩法边界。未来如果需要可见桌面扩张，应由 `TabletopView` 或正式牌桌背景资源表现同一 `Tabletop.PlacementRules`，不能再引入第二个 Board 状态。
- 2026-08-25 静态守卫补强：`gameplay-static-preflight` 现在直接读取 `LimitBoosterDefinition.cs`、`Card_Booster_Yard.asset` 和 `Card_Booster_Warehouse.asset`，证明 Yard 的默认加成为 4、Warehouse 的真实加成为 10；同时守卫当前 `CardDefinition.CardLimitBonus`、`Tabletop.CalculateCardLimitBonus()`、`RefreshPlacementRulesForCurrentCards()`、`CreateForCardLimitBonus()` 和相关 EditMode 回归，防止只保留容量字段但丢掉桌面扩张 / 收缩回流链。
- 新增公开契约测试覆盖“放置边界随卡牌上限加成扩大，移除加成卡后收缩并重排”。当前静态校验通过；Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 日终 Game Over 吸收结论

- StackCraft 文档 3.6 证明：日循环中如果牌桌上没有任何人物卡，显示 Game Over，并通过导演入口返回标题，同时清除当前活动存档。
- CardLoop 已有正式 owner：`ScenarioRun` 在日终进食后统计当前所有地区的角色卡；没有幸存角色时进入 `ScenarioDayCyclePhase.GameOver`，不允许继续新日；`ScenarioTurnPanel` 只把按钮操作转交给 `ScenarioDirector.GameOverAsync()`。
- 不恢复 `DayCycleManager`、模板 ModalWindow、直接切标题场景或旧 SaveSystem 静态入口。清除活动槽位和回标题由当前 `ScenarioDirector`、GameCore `SaveSystem` 与 `SceneSystem` 负责。
- 本轮新增回归测试只锁定日终无角色进入 GameOver 且不能继续新日；Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 CardAI 拖拽中暂停自动行为吸收结论

- StackCraft 的 `CardInstance.IsBeingDragged` 与 `CardAI.CanMove()` 证明：被玩家本地拖拽持有的卡牌不会推进自动行为；这里的效果不是全局暂停世界，而是只暂停这张卡的周期产出和自动移动计时。
- CardLoop 不恢复 `CardAI`、旧协程或拖拽状态表。正式入口是新输入链：`TabletopCardDragInput` 在本地按下命中卡牌后通知当前 `Tabletop` 持有该卡，释放或取消时释放；牌桌只跳过该卡的自动行为计时，不修改其它卡牌和行动。
- 该持有状态是本地输入姿态，不进入作者源、存档、联机协议或第二套事件总线；非法重复持有直接报错，避免静默兜底。
- 当前已源码接入“拖拽中不累计周期产出 / 自动移动时间，释放后重新等待完整间隔”，并新增回归测试覆盖。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 CardAI 围栏容量吸收结论

- StackCraft 的 `EnclosureDefinition` / `EnclosureLogic` / `CardAI.ShouldStayInEnclosure` 证明：围栏不是全局工位或牌桌容量，而是同一牌堆内的自动移动约束；非敌对自动移动卡如果位于围栏卡上方且距离不超过容量，会跳过本次自动移动。
- CardLoop 不恢复 `EnclosureLogic`、特殊 `EnclosureDefinition` 运行组件或新的工位系统。正式 owner 是卡牌作者源和当前牌桌：`CardDefinition` 声明“自动移动留存容量”，`Tabletop.AdvanceRealTime` 在自动移动入队前按当前牌堆顺序判断。
- 敌对角色卡继续忽略该约束；它们的追击、开战和增援仍走 EX-GAS 阵营标签与牌桌战斗正式入口。该规则不影响玩家拖拽拆堆，也不改变普通行动 / 战斗占用判断。
- 2026-08-25 静态守卫补强：`gameplay-static-preflight` 现在直接读取 `EnclosureLogic.cs`、`EnclosureDefinition.cs`、`Card_Enclosure_CreatureCage.asset` 和 `Card_Enclosure_CreaturePen.asset`，证明默认 Cage 容量为 1、Creature Pen 容量为 5；同时守卫当前 `CardDefinition.AutomaticMovementRetentionCapacity`、`Tabletop.ShouldStayInAutomaticMovementRetentionStack()` 和留存容量 EditMode 回归。
- 当前已源码接入“容量内留存、容量外照常自动移动”，并新增 EditMode 合同与作者校验。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 CardAI 敌对追击与战斗加入吸收结论

- StackCraft 的 `CardAI.MoveTowardsPlayer` / `HandleCombatBehavior` 证明：敌对可移动卡会优先靠近玩家相关战斗并加入；没有可加入战斗时，寻找最近玩家角色，进入攻击半径后发起战斗，未进入半径则向目标移动。
- CardLoop 不恢复 `CardAI`、旧协程、`CombatManager`、固定 Player / Mob 分组或第二套阵营字段。敌对身份读取角色唯一 EX-GAS 标签，敌对行为半径落在 `CharacterCardDefinition`，运行时仍由 `Tabletop.AdvanceRealTime` 统一推进。
- 敌对自动行为执行前同样先只抽出自身一张卡；加入战斗、开战和移动都走当前牌桌 / 战斗的正式入口，不绕过牌堆、战斗区域合并、权威随机或 GAS 能力结算。
- 当前已源码接入敌对追击、进入攻击半径开战、靠近玩家战斗后增援加入，并用 EditMode 合同覆盖发起战斗和加入既有战斗。围栏容量已由后续 CardAI 子切片接入；拖拽中暂停仍按后续子切片对证。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 CardAI 非敌对随机巡逻吸收结论

- StackCraft 的 `CardAI.AutoMove` / `MoveRandomly` 证明：非敌对可移动卡会按移动间隔周期性选择随机方向，在固定半径处尝试移动；无效候选点不会被夹回有效区，而是继续尝试下一次。
- CardLoop 不恢复 `CardAI`、旧协程、`Board.Instance`、`CardManager.ResolveOverlaps` 或新的 AI 总管。正式 owner 是卡牌作者源和当前牌桌：`CardDefinition` 声明自动移动间隔、半径和尝试次数，`Tabletop.AdvanceRealTime` 使用牌桌权威随机推进。
- 自动巡逻执行前复用当前牌桌占用规则：参与普通行动或战斗的卡牌不会移动；移动提交走 `Tabletop.TryPlaceSingleCard`，因此会先只抽出自身一张卡，再由牌桌唯一放置解算处理重叠。
- 自动移动候选先按当前牌桌边界和禁放区做源码等价校验；候选点无效时重试，不让放置解算把无效随机点自动夹回桌面。
- 当前只吸收非敌对随机巡逻。敌对追击、加入已有战斗和围栏容量已由后续 CardAI 子切片接入；拖拽中暂停仍按后续子切片对证。Unity 自动化仍被同工程 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待环境独占后补跑。

## 2026-08-16 CardAI 自动移动前置吸收结论

- StackCraft 的 `CardAI.EnsureDetachedFromStack` / `DetachFromStack` 证明：自动移动卡牌如果处于多卡牌堆中，会先只把自身抽成新牌堆；这不同于玩家拖拽从选中卡开始带走上方尾段的行为。
- CardLoop 不恢复 `CardAI`、旧协程、`CardManager.RegisterStack` 或新的 AI 总管。本次正式吸收的是牌桌对象能力：`Tabletop.TryPlaceSingleCard` 只移动指定卡牌本身，仍由 `TabletopCards` 和 `TabletopCardStack` 维护牌堆成员关系。
- 单卡抽出保留局内卡牌 ID、内容 ID、角色卡 EX-GAS 状态、装备状态和周期产出累计秒数；剩余牌堆保持相对顺序，放置仍走当前牌桌唯一空间解算，不新增第二位置状态。
- 当前只完成自动移动前置能力和 EditMode 合同；非敌对随机巡逻、敌对追击、加入战斗和围栏容量仍按后续 CardAI 子切片继续对证。
- Unity 自动化仍被当前同工程多个 Unity / ShaderCompiler 进程和 `Temp/UnityLockfile` 阻塞，待编辑器环境收敛后补跑。

## 2026-08-16 CardAI 周期产出吸收结论

- StackCraft 的 `CardAI.StartAI` / `ProduceLoop` / `SpawnProduce` 证明存在“非敌对卡按间隔在自身位置生成另一张卡，并播放卡牌烟雾粒子”的玩家效果；源码在卡牌忙于拖拽、制作或战斗时跳过本次产出。
- CardLoop 不恢复 `CardAI`、旧协程、`CardManager.CreateCardInstance` 或 AI 总管。正式 owner 是卡牌作者源和当前牌桌：`CardDefinition` 声明周期产出卡牌与间隔，`Tabletop.AdvanceRealTime` 统一推进并提交产物。
- 产物创建仍走 `Tabletop.CreateCard`、放置预检和正式内容索引；空间反馈仍走 `TabletopPresentationCueKind.CardSmoke`，由 `TabletopView` 通过 `SoftAssetReference` / `ResourceSystem` 实例化中文素材路径下的 `卡牌烟雾粒子.prefab`。
- 周期产出累计秒数是卡牌实例状态，保存在 `TabletopCard` 并进入 `TabletopCardSnapshot`；角色卡继承同一状态，不复制第二套计时器。
- 当前已实现活动行动和战斗占用下跳过产出。拖拽是输入 / 视图层瞬时状态，尚无规则层正式 owner；要等输入层与单局实时推进的交互暂停策略裁决后再补，不在 `Tabletop` 增加临时拖拽锁或第二状态。
- 当前静态验证确认构造调用点已同步，新增周期产出 EditMode 用例尚未运行。Unity 自动化被当前同工程多个 Unity / ShaderCompiler 进程阻塞，待编辑器环境收敛后补跑。

## 2026-08-15 卡牌烟雾粒子反馈吸收结论

- StackCraft 的 `PuffParticle`、`CardInstance.PlayPuffParticle`、`RecipeDefinition`、`ChestLogic`、`TradeZone`、`EncounterManager` 和 `CardAI` 证明卡牌烟雾是“空间粒子 + 原 `Puff.wav` 音效”的反馈，不是单纯音频枚举。
- CardLoop 不恢复 `PuffParticle`、`AudioId.Puff`、`AudioManager`、`TradeZone` VFX 脚本或新的 VFX Manager。正式 owner 是 `TabletopView`：规则和剧本只发只读 `TabletopPresentationCue`，有空间反馈时携带牌桌坐标。
- `TabletopViewSettings` 保存卡牌烟雾粒子预制体、卡牌烟雾反馈音效和排序值；图片、材质、粒子预制体和音频统一落到 `Assets/Art/Sprites`、`Assets/Art/Textures`、`Assets/Art/Materials`、`Assets/Art/Prefabs`、`Assets/Audio/SFX` 标准资源目录，粒子 prefab 已迁入为 `Assets/Art/Prefabs/卡牌烟雾粒子.prefab`，运行时仍通过 `SoftAssetReference` / `ResourceSystem` 实例化并释放句柄。
- 正式代码语义使用 CardLoop 领域名 CardSmoke；PuffParticle、Puff.wav 和 AudioId.Puff 只作为 StackCraft 来源证据，不进入正式资产名或正式表现提示枚举。
- 当前已接入行动产物 / 卡牌耗尽 / 普通移除、购买卡包、箱子存取币、售卖锚点、战斗死亡、日终进食耗尽、日终遭遇生成和非敌对卡周期产出的卡牌烟雾反馈。
- 当前静态验证确认没有恢复 `AudioManager`、`AudioId`、`PlaySFX`、`CombatManager`、`ProjectileManager`、`HitUI` 或 DOTween 调用；中文粒子 prefab 已登记进 YooAsset 测试收集配置。Unity 自动化被当前同工程多个 Unity / ShaderCompiler 进程阻塞，待编辑器环境收敛后补跑场景重建与 PlayMode。

## 2026-08-15 非战斗反馈音效吸收结论

- StackCraft 的 `CardController`、`RecipeDefinition`、`ChestLogic`、`PackVendor`、`CardBuyer` 和 `AudioManager` 证明模板存在通用反馈音效：拿起卡牌、释放卡牌、日终进食滑动、进食、制作 / 生成、取币、存币 / 出售和购买成交。
- CardLoop 不恢复 `AudioManager`、`AudioId`、`PlaySFX` 或输入回调直接执行业务。拖拽输入只在真实按下 / 释放后请求 `TabletopView` 播放反馈；日终进食由 `ScenarioRun` 发布只读表现事实；行动结算只返回牌桌表现提示，任务事实、规则结算和音频播放仍分属各自 owner。
- `TabletopViewSettings` 保存 8 个非战斗牌桌反馈 `AudioClipResolver` 作者引用；测试场景生成器使用已迁入 `Assets/Audio/SFX` 的中文项目音效生成 Resolver，并继续走现有 `AudioPlaybackRequestedEvent` -> GameCore `AudioSystem`。
- 原 `Puff` 当时不纳入纯音效切片：它是粒子 VFX + 音效，需要对照 StackCraft 源码与当前正式表现 owner 单独裁决；随后已由上方卡牌烟雾粒子反馈切片接入。
- 当前静态验证确认没有恢复 `AudioManager`、`AudioId`、`PlaySFX`、`CombatManager`、`ProjectileManager`、`HitUI` 或 DOTween 调用。Unity 自动化被当前同工程多个 Unity / ShaderCompiler 进程阻塞，待编辑器环境收敛后补跑场景重建与 PlayMode。

## 2026-08-15 战斗 HitUI 命中反馈吸收结论

- StackCraft 的 `HitUI.Initialize` 证明命中反馈由三类命中图标、两类克制图标、伤害数字和 `DOPunchScale(0.15, 1s)` 组成；Miss 不显示伤害数字。
- CardLoop 不恢复 `HitUI`、`CombatManager.SpawnHitUI`、DOTween 依赖或独立弹窗生命周期。`TabletopCardView` 作为目标卡牌视图直接显示命中图标、伤害数字、克制图标，并用 `Time.unscaledDeltaTime` 播放 1 秒 punch 缩放。
- 测试场景生成器使用已迁入 `Assets/Art/Sprites` 的五张命中图标序列化到测试卡牌视图；正式资源加载仍归 `ResourceSystem` / YooAsset，运行时代码不按 StackCraft 路径找图。
- 当前静态验证确认没有恢复 `HitUI`、`CombatManager`、`AudioManager`、`AudioId`、`CombatType` 或 DOTween 调用。Unity 自动化被当前同工程多个 Unity / ShaderCompiler 进程阻塞，待编辑器环境收敛后补跑场景重建与 PlayMode。

## 2026-08-15 战斗音效吸收结论

- StackCraft 的 `CombatTask.AttackSequenceCoroutine` 和 `AudioManager` 证明战斗音效时序是：攻击起手播放 Attack；命中后播放对应类型 Hit；暴击额外播放 Critical；未命中播放 Miss。
- CardLoop 不恢复 `AudioManager`、`AudioId`、`CombatType` 或音频总管。`Battle` 只暴露攻击开始表现事实，`TabletopView` 读取当前攻击类型并通过既有 `AudioPlaybackRequestedEvent` 交给 GameCore `AudioSystem`。
- `TabletopViewSettings` 只保存各战斗音效的 `AudioClipResolver` 作者引用；测试场景生成器用 StackCraft SFX 临时生成 8 个 Resolver，并配置现有 `GameplaySoundFX` 通道。资源加载和音频播放仍归 `ResourceSystem` / `AudioSystem`，不建立第二套音频 ID。
- 静音伤害结算不播放 Miss / Hit / Critical，和现有 GameCore 静音表现语义一致；`NoFloatingText` 只屏蔽浮字，不屏蔽音效。
- 当前静态验证确认没有恢复 `CombatManager`、`CombatType`、`AudioId`、`AudioManager` 或 `ProjectileManager`。Unity 自动化验证被当前同工程多个 Unity / ShaderCompiler 进程阻塞，待编辑器环境收敛后补跑。
- 战斗表现中的投射物、音效和 HitUI 式命中反馈均已完成源码接入；Unity 场景重建与 PlayMode 验证待编辑器空闲后补跑。

## 2026-08-15 战斗投射物前摇吸收结论

- StackCraft 的 `CombatTask.AttackSequenceCoroutine` 与 `CombatProjectile` 证明远程 / 魔法攻击需要在伤害前播放 `0.6s` 线性投射物；该效果属于战斗表现时序，不属于伤害公式。
- CardLoop 由 `Battle` 保存只读攻击前摇表现事实，`Tabletop` 在前摇结束后才激活 EX-GAS Ability。伤害、命中、暴击和克制继续由正式 GNS/EX-GAS 链路结算。
- `TabletopView` 通过现有 `ResourceSystem` 和 `TabletopViewSettings` 实例化 `TabletopProjectileView`，不新增投射物总管、第二事件总线或 YooAsset 封装。
- 当前静态验证已确认没有恢复 `CombatManager`、`CombatStats`、`CombatType` 或 `ProjectileManager`。Unity 自动化验证被当前同工程多个 Unity / ShaderCompiler 进程阻塞，待编辑器环境收敛后补跑。
- 战斗表现中的投射物、音效和 HitUI 式命中反馈均已完成源码接入；Unity 场景重建与 PlayMode 验证待编辑器空闲后补跑。

## 2026-08-11 模块 3.4 牌桌视图吸收结论

- StackCraft 的 `CardInstance` / `CardManager` 证明牌桌需要统一创建、更新和回收卡牌表现，但其视图同时持有玩法状态和调用全局 Manager 的结构不吸收。
- 当前正式对象是 `TabletopView`：它绑定一张 `Tabletop`，按修订投影卡牌、战斗姿态和行动进度，并统一持有与释放 `ResourceSystem` 资源句柄。它是完整表现对象，不是新的玩法系统。
- `TabletopViewSettings` 是唯一牌桌表现作者资产；权威尺寸和 XY 步进继续只属于剧本牌桌放置规则。组件自身 Transform 是唯一视图根，不再手填额外根节点。
- 单卡 `TabletopCardView` 保存对应卡牌对象引用，身份与所属牌堆关系不在表现层复制。定向 EditMode `2/2`、Foundation 真实 YooAsset / 解绑链 `13/13`；模块 3 最终全量 EditMode `425/426`（`1` 条既有忽略）、PlayMode `30/30`。

## 2026-08-11 模块 3.3 拖拽职责吸收结论

- `CardController` 的有效行为是记录按下点、保持拖拽偏移、拖动牌堆尾段、区分点击与拖拽并高亮释放目标；这些行为已由正式新输入链复现。
- StackCraft 把拆堆、交易、装备、开战、制作暂停、音效和 Transform 权威修改塞进输入回调的结构不吸收。CardLoop 输入只形成释放意图，牌桌或当前单局的行动候选链负责复核和提交。
- 2026-08-21 订正：正式点击 / 拖拽阈值来自 `TabletopViewSettings.m_clickThreshold = 0.02`，按 StackCraft `CardSettings.clickThreshold` 的牌桌世界距离计算；拖拽抬升来自 `TabletopViewSettings.m_dragHeight = 0.1`，按 StackCraft `CardSettings.dragHeight` 投影到 Unity Y 轴；主相机从正式上下文读取，`EventSystem` 只负责 UI 命中和释放目标射线，不参与卡牌阈值。
- 当前静态验收为 `gameplay-static-preflight` 禁止 `pixelDragThreshold` 回流、测试作者源回写 `m_clickThreshold: 0.02` 与 `m_dragHeight: 0.1`、拖拽会话以牌桌坐标判断阈值、按下即预览实际拖拽牌段；Unity 编译和 PlayMode 需编辑器可用后补新鲜验证。

## 2026-08-11 模块 3.2 放置职责吸收结论

- `Board` 的有效职责是拥有桌面边界和禁放区域；`CardPhysicsSolver` 的有效职责是作为牌桌内部算法处理整堆边界与重叠。二者不作为全局单例或独立 Gameplay 系统照搬。
- CardLoop 由 `ScenarioDefinition` 配置牌桌空间事实，`ScenarioRun` 创建唯一 `Tabletop` 规则，`TabletopCardStackPlacementSolver` 只消费候选空间体并返回解算结果。
- 模板的 Transform 权威位置、调用方自选规则、视图配置反向决定占地、技术解算轮数作者字段和直接移动旁路均排除。
- 创建卡牌、拖拽空白放置和行动产物使用同一规则；行动产物在删除参与卡前完成整批空间预演。

> 命名与对象模型迁移说明（2026-08-10）：正式模块已从历史 `GamePlay` 迁移为 `Gameplay`，并拆分为 Content、Actions、Tabletop、Scenarios 命名空间。当前单局模型是 `ScenarioDirector -> ScenarioRun -> QuestLog / Tabletop`，行动运行对象是 `ActionInstance`。本文中 `ScenarioTurnSystem`、`QuestSystem`、`TabletopCardActionSystem`、`TabletopCardActionJob`、旧 `GamePlay` 拼写、旧日志和已删除脚本仅保留其原始历史名称，不能作为当前作者入口、API 或 Mod 命名依据。

> 全量重审说明（2026-08-09）：本文保留每个历史切片的裁决和测试证据，但“已完成 / 已收口”只表示当时明确选择的功能切片已经验证，不表示整个一级模块或最终对象模型成立。模块 1-6 的当前架构结论以 [`gameplay-foundation-reaudit.md`](gameplay-foundation-reaudit.md) 为准。

## 记录状态

- 本文记录 2026-08-01 对 `Assets/StackCraft/` 的模块吸收审查。
- 2026-08-02 已按用户补充策划修正口径：新玩法设计只作为扩展性约束；当前仍只打地基、吸收模板能力，不提前实现原创业务。
- 用户 2026-08-06 当前完整设计稿已同步到《卡牌生存：无限》知识文档；本表只吸收它对 StackCraft 地基的架构压力，不把设计稿里的教程、职业、战斗、剧本或局外空间列为当前实现任务。
- 真相来源是当前仓库中的 StackCraft 模板源码、资源、场景、Prefab、编辑器脚本，以及 GamePlay 已登记的项目愿景、地基提案、GameCore / EX-GAS 边界文档。
- 本文用于约束后续重构顺序：不能因为某个表现模块“看得见、能拖动”就跳过上游数据定义。
- 用户口径：重点审查哪些 StackCraft 部分值得保留吸收；已被 GamePlay / GAS 更好覆盖的部分应直接排除；该重构就重构，不为省事保留会变成长期负债的旧实现。

## 关键修正：为什么第一个模块必须是数据定义

- **牌桌不是真相源**：牌桌只知道“有哪些可显示、可拖拽、可交互的运行时对象”；它不应该定义卡牌到底是角色、地点、事件、物品、敌人还是规则承载物。
- **Mod 和关卡编辑器先依赖数据契约**：如果没有唯一 ID、GAS 标签引用/查询、资源引用、对象类型、运行时状态边界，后面的拖拽、配方、任务、存档都会各自发明一套临时字段。
- **StackCraft 最大冲突先在数据层**：`CardDefinition`、`CardCategory`、`Resources.LoadAll`、固定 SO 引用是后续所有旧逻辑的根。先不裁掉这些旧职责，后面每个模块都会被旧枚举和旧资源路径牵着走。
- **激进重构不是乱改顺序**：正确做法是先定 GamePlay 的内容契约，再让牌桌、行动、目标、GAS、存档都消费同一个契约；不是先做一个能拖的原型，再事后迁移全部数据。

## 命名纠偏：不拿大词压模块

- `ContentCatalog` 不是当前项目已有模块，也不作为 GamePlay 第一阶段的正式模块名。若后续需要类似能力，只能是“由作者源自动生成的运行时查询缓存 / 索引”，不能成为第二套内容 ID、第二套作者源或作者需要手动维护的目录。
- 本文后续只使用“作者源”“内容包加载”“运行时查询缓存”“校验器”这类具体职责名；不再用 `ContentCatalog` 把多个职责糊成一个大模块。
- “任务系统”必须拆开说：StackCraft 的 `CraftingTask` 是 **桌面行动进度**，`Quest` / `QuestManager` 是 **玩家可见任务系统参考**，`CombatTask` 是 **战斗结算循环**。这三者不能混成一个总称；其中 Quest 不能再降级成顶级 Objective，正确层级是 Quest -> Task / Objective -> Requirement。
- GamePlay 现有 GameCore 有 `ICommand` / `IInteraction` 这类“命令和交互执行”能力，但没有专门承接“角色对地点/设施/事件执行跨阶段行动”的正式模块。StackCraft 的 `CraftingTask` 证明模板存在延迟完成、暂停、恢复、取消和进度反馈；它的秒数数据和类型结构不吸收。普通行动已经确认以回合消耗为唯一进度真相，默认回合制，切换即时制时由当前回合时间规则统一换算；战斗始终即时且使用独立战斗链。

## 强制重构口径

| 规则 | 执行要求 |
|------|----------|
| 不保留旧职责 | StackCraft 的 `Resources.LoadAll`、大一统 `CardDefinition`、枚举分类、单例管理器、固定场景名、独立战斗规则和 JSON 扫档不得作为 GamePlay 正式职责归属。 |
| 不用 adapter 掩盖遗留 | 临时适配层只允许用于读取参考素材或过渡验证；必须写清删除条件和替换目标，不能长期包住 StackCraft 旧实现继续用。 |
| 先改源头 | 如果一个问题来自作者源数据、资源加载、标签体系或配置 schema，先重构源头，不在下游牌桌、UI、存档里打补丁。 |
| 同职责单归属 | 行动、配方、制作、目标和战斗不得同时保留 GamePlay 旧 GameCore 实现、StackCraft 实现和新实现三套正式链路；开工前必须裁决谁是正式职责归属，未被选择的一方只能作为参考或删除对象。 |
| 保留算法，不保留边界错误 | 可吸收拖拽、拆堆、重叠解算、任务进度等算法和手感；但必须迁入 GamePlay 正式职责入口，重切职责边界。 |
| 每模块开工先列替换清单 | 每个模块开始前必须写清：StackCraft 哪些类只是参考、哪些职责会重写、哪些旧入口必须删除或隔离、验收回到哪里。 |
| 参考目录不等于正式依赖 | `Assets/StackCraft/` 可作为参考样例保留；GamePlay 正式代码不得依赖 StackCraft 命名空间、资源路径或旧单例作为长期运行链路。 |
| 游戏策划不等于实现清单 | 《卡牌生存：无限》的多世界、联机、Mod、跑团、职业、DND/COC、前后排、区域切换等内容只作为扩展性压力测试；当前不开原创业务实现线。 |

## 当前执行顺序（2026-08-10）

当前 Gameplay 地基的唯一执行顺序、每个模块的状态、进入条件和验收口径收口在项目根目录的 [`task_plan.md`](../../../../task_plan.md)。它按进程框架集成、内容会话、单局生命周期、牌桌、行动、剧本流程、战斗、正式 UI、作者工具、存档、联机/Mod 边界重新排列了工作；联机与 Mod 是每一步的约束，不是等到最后才考虑的独立玩法。

本节下面的十模块表保留为 2026-08-01 的**历史初始拆分**，用于追溯当时的 StackCraft 源码对照和已做切片；不得再把它当作当前开工顺序或“模块已经完成”的依据。

## 历史初始模块顺序（2026-08-01）

本表的目标是 **搬迁和重构 StackCraft 的架构**，不是提前实现《卡牌生存：无限》的职业、技能树或联机玩法。GamePlay 的 Mod、关卡编辑器、联机和职业成长只是裁决约束：凡是 StackCraft 的设计会阻碍这些目标，就不能原样保留为正式职责。

用户新增的荒岛剧本、UI、局外主神空间、职业经历、即时战斗和未来剧本池记录在《卡牌生存：无限》知识文档中；本表只用这些信息反向检查地基是否足够可扩展，不把它们列为当前模块实现目标。

### 最新附件对第一模块的影响

- StackCraft 的 `CardDefinition` 更确定不能吸收为万能定义：最新设计里“卡牌”既可能是角色、物品、技能、事件、地点入口、临时工位和规则展示，也存在不适合卡牌化的局外主神空间点位、职业谱系、规则模块、成长带出引用和剧本壳。
- 第一模块要把三类东西分开：**内容共用元信息**、**核心内容定义族**、**引用关系声明**。上一版把技能引用、局外引用和对象定义混在一串里，容易误读为要一次性新增很多系统；正式口径必须拆开。
- 作者源先收窄为确有公共技术合同的内容基类。角色、地点、道具、事件、工位等语义先由标签和后续模块表达；只有出现独立字段与生命周期时，才增加新的作者源类型。卡牌表现、世界表现和可交互能力可以同时成立，不能继续作为互斥继承分类。
- 卡牌表现不是数据根：同一个角色可以被显示成卡牌、右侧详情、意图标记或日志记录，但这些都是展示/引用，不是新的角色真相。
- 工位定义必须前置：固定工位、可移动圆形工位、连通节点和卡牌工位会直接决定第三模块牌桌投放、第四模块行动解析和第五模块世界流程，因此不能等到场景/UI 阶段再临时补。
- NPC 意图、领袖投票、回合确认、成长带出和职业/技能关系只进入引用关系边界：第一模块记录“谁引用谁、用什么 ID/Tag/条件引用”，不实现 AI、投票、好感、教程流程、职业系统或联机同步。
- 局外准备和成长带出要求内容 ID 从第一天就跨局稳定：可购买职业/道具/变异、剧本内掉落、带出成本和经历记录都必须引用同一个内容身份体系，不能局内一套、局外一套。
- 本表后续吸收 StackCraft 时，以玩家可见功能对照为主：参考拖拽手感、跨阶段行动、目标监听、日结节奏、UI 反馈和编辑器校验，但每项都要重新裁决是否进入当前游戏。参考类、字段和流程不自动保留；大一统 `CardDefinition`、枚举分类、`Resources.LoadAll`、直接副作用执行、固定场景名和 RPS 的固定枚举结构明确排除；RPS 玩家效果只能通过 EX-GAS Tag / GameplayEffect 等正式职责吸收。

按依赖顺序，GamePlay 吸收 StackCraft 时应拆成 10 个一级审查模块；一级模块只用于排序，真正开工时必须继续拆成可逐项判断的小子模块：

| 顺序 | 架构审查模块 | 为什么排在这里 | StackCraft 主要参考 | 总裁决 |
|------|--------------|----------------|--------------------|--------|
| 1 | 内容定义总包：作者源 / 加载 / 查询缓存 / 校验 | 所有运行时对象、牌桌表现、行动、目标、存档和 UI 都依赖统一 ID、类型、标签、资源引用和包边界；本模块必须继续拆成 1.1-1.10 小步审查。 | `CardDefinition`、`PackDefinition`、`RecipeDefinition`、`Quest`、`EncounterDefinition`、`Resources` 目录。 | **字段职责切片已订正；内容包、Mod 依赖、引用作者体验和加载会话仍待重构。** |
| 2 | 启动流程 / 系统协作 / 单局状态边界 | StackCraft 用多个单例 Manager 串联流程；当前项目已有 `GameCore.GameManager`、`AGameSystem` 生命周期、`EventKit`、资源/Mod/GAS 初始化，必须先审查这些职责能否重构承接。 | `GameDirector`、`CardManager`、`CraftingManager`、`QuestManager`、`TimeManager`、`DayCycleManager`。 | **进程级启动切片已验证；单局状态仍错误依附进程级系统，未收口。** |
| 3 | 可堆叠卡牌运行时 / 卡牌视图 / 堆栈交互 | 有了内容契约和启动/单局状态边界后，才能安全实例化卡牌视图、堆栈和桌面卡牌。 | `Board`、`CardController`、`CardInstance`、`CardStack`、`CardPhysicsSolver`、`StackingRulesMatrix`。 | **3.1-3.6 已跑通 StackCraft 可堆叠卡牌体验；回审后明确不得把该模型扩张为全部牌桌对象的通用状态。** |
| 4 | 行动选择 / 配方条件 / 桌面行动进度 | 依赖内容定义和牌桌投放事件，解释“谁对什么对象做什么行动”，以及这段行动如何计时、暂停、取消和完成。 | `CraftingManager`、`CraftingTask`、`RecipeDefinition`、`ExplorationRecipe`、`ResearchRecipe`、`TravelRecipe`、`GrowthRecipe`。 | **4.1-4.12 功能切片已验证；行动聚合、配方归属和 Mod 结果扩展入口尚未裁决。** |
| 5 | 剧本 / 目标 / 时间 / 世界流程 | 依赖行动结果和内容事件，组织胜负条件、危机、日结和多世界规则；模板 Encounter 只作为剧本事件触发问题的参考片段。 | `QuestManager`、`EncounterManager`、`DayCycleManager`、`TimeManager`、`GameDirector` 的流程片段。 | **先建立父级归属，再改造吸收流程；不照搬 Manager 或类型枚举。** |
| 6 | 交易 / 卡包 / 经济闭环 | 这是 StackCraft 成品闭环的一部分，但在 GamePlay 中属于剧本可选规则，不能抢内容包和商店职责。 | `PackDefinition`、`PackSlot`、`PackVendor`、`TradeManager`、`CardBuyer`、`TradeZone`。 | **参考闭环，延后接管。** |
| 7 | 战斗 / 冲突区 / Stats / 装备 / 职业变化 | 这里审查的是 StackCraft 现有战斗和装备架构，不是实现 GamePlay 的职业技能系统。 | `CombatManager`、`CombatTask`、`CombatRect`、`CardCombatant`、`CombatStats`、`StatModifier`、`classChangeResult`。 | **旧结构排除，效果吸收；GNS/EX-GAS 承担正式数值、属性和效果，StackCraft 的攻防、攻速、命中、闪避、暴击参数可作为模板复现的临时数值。** |
| 8 | UI 框架 / 界面状态绑定 / 作者工具 | UI 框架本身属于架构：它决定反馈如何订阅状态、行动如何确认、作者如何发现配置冲突。 | `InfoPanel`、`ProgressUI`、`CardStatsUI`、`QuestsView`、`RecipesView`、`ModalWindow`、各 Definition Editor。 | **吸收模式和工具体验，重做数据绑定。** |
| 9 | 存档 / 运行时状态恢复 | 存档应在运行时边界清楚后接入，否则会把 StackCraft 的场景名、卡牌 ID 和 JSON 扫档固化成债务。 | `GameData`、`SceneData`、`StackData`、`CardData`、`SaveSystem`。 | **只参考状态范围，重做存档职责。** |
| 10 | 联机适配约束 | StackCraft 没有联机模块，但 GamePlay 明确支持联机；搬架构时必须预留控制权、同步、随机、可见性和秘密目标边界。 | 无直接模块；反向审查单例、随机、全局可见状态和直接副作用。 | **新增硬约束，不做玩法实现。** |

## 第一模块小步拆分

第一个一级模块不能再作为一个“大数据系统”一次性开工，按下面小步逐项审查和落地：

| 子模块 | 审查问题 | StackCraft 参考 | GamePlay 裁决 |
|--------|----------|----------------|---------------|
| 1.1 唯一 ID | 一个内容对象到底用什么身份被引用、存档、Mod 覆盖和编辑器校验？ | `Quest.id` 会自动生成 GUID 字符串，`RecipeDefinition.Id` 被任务和发现列表引用。 | 只保留一个作者可控内容 ID；Unity GUID、YooAsset 地址和文件路径只作定位，不作第二 ID。 |
| 1.2 SO 作者源 / 内容元信息 | 哪些字段属于多个作者源共同需要，哪些字段必须拆到具体类型？ | `CardDefinition` 把显示、战斗、食物、装备、交易、职业变化等塞在一起。 | 保留 SO 作者源；`ContentAsset` 统一稳定身份、EX-GAS 静态标签和校验扩展，`DisplayableContentAsset` 再承接名称、描述与图标。它们都不是所有玩法数据的业务父类。卡牌专用卡面由 `CardDefinition` 提供，其它表现资源留在对应真实作者源。 |
| 1.3 内容发现 / 加载边界 | 内容从哪里进入运行时，如何支持 YooAsset、Mod 和关卡编辑器？ | `Resources.LoadAll` 扫描固定目录。 | 第一模块不建立手工内容清单或第二套加载器。编辑器直接扫描内容资产；运行时发现、加载会话和包来源由后续资源 / Mod 模块复用 `ResourceSystem` 正式实现。 |
| 1.4 资源引用 | 卡面、Prefab、音效、图标怎么被加载？ | SO 直接引用或 Resources 路径隐含依赖。 | 资源引用只负责加载定位；它不能替代内容 ID，也不能要求作者双重维护；已收口到现有 `SoftAssetReference` / `ResourceSystem`。 |
| 1.5 GAS 标签引用 / 查询 | 配方、目标和规则如何表达“任意木材”“野兽”“火源”等抽象条件？ | `CardCategory`、`QuestType`、`CombatType` 等枚举。 | 内容作者源只保存一组 EX-GAS 标签码，不再把同一内容的标签拆成身份/交互/匹配三份。层级查询必须使用 GAS 正式语义；当前 EX-GAS 生成标签表尚无 Mod 合并入口，因此查询契约延后，不用整数相等匹配冒充 GAS 查询。 |
| 1.6 作者源类型 | 哪些内容本身需要独立作者资产？ | StackCraft 基本都压成 `CardDefinition`。 | 不预建角色、道具、地点、工位、危机等空壳类型；也不把卡牌表现和可交互能力固化成二选一作者源。只有真实独立字段、实例化或覆盖生命周期才能证明新增类型。 |
| 1.7 引用关系声明 | 哪些内容只是“被谁引用”，不是第一模块要实现的新系统？ | StackCraft 的 `Quest` / `RecipeDefinition` / `EncounterDefinition` 里混有目标、结果、场景和卡牌引用。 | 技能引用、职业引用、局外可购买/可带出引用、剧本初始投放、NPC 意图、行动计划、UI 展示摘要都只是引用关系；第一模块只保存 ID/Tag/条件，不实现对应业务。 |
| 1.8 行动与配方条件 | 行动怎么声明可用条件、耗材、蓝图门槛和结果？ | StackCraft 的 `RecipeDefinition.RequiredIngredients`、特殊 Recipe 子类；GameCore 旧 `Recipe` / `CraftingStation` 背包制作站模型。 | 这是后续行动模块的冲突域。第一模块只记录参考字段，不预建行动/配方 schema；等牌桌投放关系和 GAS 查询语义锁定后再建立唯一正式实现。 |
| 1.9 运行时查询缓存 | 运行时如何快速查“某地点可用行动”“某标签匹配哪些配方”？ | 各 Manager 自己建 lookup 或直接遍历列表。 | 可以有派生缓存，但只能由作者源生成或校验，不能手写、不能双更、不能叫第二套目录。 |
| 1.10 内容校验器 | 如何提前发现重复 ID、缺资源、条件冲突和引用断裂？ | `QuestManager` 检重复 ID，`RecipeDefinitionEditor` 检同材料冲突。 | 吸收校验体验，升级为 GamePlay 内容校验器；第一阶段覆盖唯一 ID、非正标签码和重复标签码。资源完整性、GAS 层级查询与包依赖在对应职责接入后校验，不提前伪造。 |

### 第一模块实现裁决（2026-08-01）

| 子模块 | 实现入口 | 当前裁决 |
|--------|----------|----------|
| 1.1 唯一 ID | `ContentId` | 作为公开内容身份供存档、联机和 Mod 引用；GameCore 的 Unity GUID 仍只负责内部资产登记，资源地址只负责加载。 |
| 1.2 SO 作者源 / 内容元信息 | `ContentAsset` / `DisplayableContentAsset` | 前者保留唯一 ID、标签和校验入口，后者承接最小展示信息；通用卡面字段已删除，技术基类不再承担牌桌表现。 |
| 1.3 内容发现 / 加载边界 | 延后到资源 / Mod 模块 | `GamePlayContentSet`、`GamePlayContentLoader` 已删除，避免作者双重登记和提前写死裸地址加载。运行时发现与句柄生命周期必须由正式资源职责实现。 |
| 1.4 资源引用 | `SoftAssetReference<T>` | `GamePlayAssetReference` 已删除；图标、卡面和世界图片使用项目统一软资源引用，只负责定位资源，不替代内容 ID。牌桌 / 世界实例预制体归后续表现模块裁决。 |
| 1.5 GAS 标签引用 / 查询 | `ContentAsset.TagCodes` | 内容只保存一组 EX-GAS 官方整数标签码；`GamePlayContentQuery`、本地 `HasTagCode` 和精确标签索引均已删除。父子层级和条件查询只能使用 EX-GAS 正式入口。 |
| 1.6 作者源类型 | 当前实现为 `CardDefinition` | 删除角色/地点/道具/事件/工位/剧本/世界规则七个空壳定义，并删除职责重叠的 `GamePlayInteractableDefinition`。`CardDefinition` 只承载卡牌专用卡面，可由项目内容或 Mod 内容继承；可交互能力等真实字段出现后再组合到对应作者源。 |
| 1.7 引用关系声明 | 延后 | 第一模块不再预建职业、技能、行动计划或卡牌目标引用；具体模块开始时再按真实字段建立。 |
| 1.8 行动与配方条件 | 延后到 4.3 及后续子模块 | 第一模块提前猜测条件、成本和结果的旧行动实现已删除；4.2 只重新建立独立行动作者身份，不包含任何条件、消耗、时间或结果合同。 |
| 1.9 运行时查询索引 | `ContentIndex` | 从已加载内容资产派生唯一 ID 索引；内部以 `ContentId` 为键，不建立标签索引，也不承担任何 GAS 查询语义。 |
| 1.10 内容校验器 | `ContentValidator` 与 `GamePlay/内容/校验内容资产` | 当前覆盖空资产、重复引用、重复/无效内容 ID、无效/重复标签码；Mod 包和依赖校验延后到正式 Mod 职责。 |

## 第二模块小步拆分

第二个一级模块不是牌桌，也不新增 `RuntimeContext`。它的目标是先审清当前项目已有启动、生命周期、系统注册、资源、Mod、GAS 和事件职责，再吸收 StackCraft 如何串起新局、读档、场景、时间、日结、目标、制作和保存。StackCraft 的问题在于它把这些职责交给多个 `public static Instance` Manager、`Awake` / `Start` 偶然顺序和直接状态修改；此前 GamePlay 的 `RuntimeContext` 小框架已因职责重复且未接入真实入口而删除。

| 子模块 | 审查问题 | StackCraft 参考 | GamePlay 裁决 |
|--------|----------|----------------|---------------|
| 2.1 启动入口 | GamePlay 运行时由谁创建，场景对象只是组合还是拥有全局真相？ | `GameDirector.Awake` 设置单例、`DontDestroyOnLoad`、订阅 `SceneManager.sceneLoaded`。 | 先审 `GameCore.GameManager` 是否应重构为正式启动 / 生命周期入口；只有它无法承接时，才新增 `Director` 类正式职责入口。 |
| 2.2 单局状态边界 | 一局游戏里哪些服务、状态和引用应该集中，哪些应留给已有系统？ | `GameDirector.GameData`、`CardManager` 的卡堆列表、`CraftingManager` 的任务列表、`QuestManager` 的目标状态。 | **2026-08-09 重审后重新打开。** 当前剧本、任务、回合和行动状态仍挂在进程级系统层，必须先确定真实单局 / 联机会话聚合，再迁移这些状态。 |
| 2.3 内容索引接入 | 第二模块如何消费第一模块内容，而不是重新加载内容？ | `CardManager.BuildDefinitionDatabase` 和 `CraftingManager.Awake` 用 `Resources.LoadAll` 扫卡牌、卡包和配方。 | 第一模块内容索引只能通过正式资源 / 内容加载职责进入；不新增 `Resources` 扫描、不新建第二套 loader、不引用 StackCraft 定义库。 |
| 2.4 Manager 职责拆解 | StackCraft 各 Manager 的职责由哪些 GamePlay / GameCore 模块承接？ | `GameDirector` 管新局/读档/切场景/保存；`CardManager` 管定义库/牌桌/统计/进食；`CraftingManager` 管配方/任务/UI；`QuestManager` 管目标监听；`TimeManager` / `DayCycleManager` 管时间和日结。 | 第二模块只裁决协作边界：启动、系统生命周期、事件、场景和单局状态。牌桌、行动、目标、时间、存档分别留给后续模块接管，不能复制 Manager 名称或单例形状。 |
| 2.5 事件 / 命令流 | 系统之间如何通信，状态变化如何解释、同步和回放？ | `QuestManager.Start` 订阅多个 Manager 事件，`CraftingManager.Update` 完成后直接执行 `recipe.Execute`，`DayCycleManager` 直接锁输入和调 UI。 | 建立最小事件 / 请求 / 结果语义：谁发起、目标是谁、结果是什么、谁可见、随机从哪里来；命令发起者优先复用 `GameCore.GameCommandContext`，普通强类型事件默认直接使用 YokiFrame `EventKit`；只有包装承担校验、权限、可见性、回放记录、生命周期分发或跨模块稳定 API 时才允许存在。 |
| 2.6 运行时实例身份 | 静态内容定义和局内实例如何分开？ | `CardInstance` 同时承担视图、动态数值、装备、生命、存档和规则状态。 | 先定运行时实例 ID 与内容 ID 的关系：内容 ID 是静态身份，运行时实例 ID 是单局对象引用；表现层只能投影实例状态，不能保存规则真相。 |
| 2.7 生命周期 / 场景切换 | 新局、读档、切剧本、返回局外、销毁单局状态如何收口？ | `GameDirector.TravelSequence` 用固定场景名、`SceneManager.LoadSceneAsync` 和 incoming travelers 搬运卡牌数据。 | 场景名不能当剧本 ID；第二模块只定义生命周期与切换请求边界，场景加载走正式资源/场景职责，保存和恢复留给存档模块。 |
| 2.8 最小测试和验收 | 怎样证明第二模块没把旧单例链或第二套上下文带进来？ | StackCraft 靠场景运行和 Manager 存在来证明流程。 | 先产出职责裁决表和删除 / 重构清单：确认不依赖 StackCraft 命名空间、`Resources.LoadAll`、固定场景名或 `public static Instance`，并确认不与 `GameCore.GameManager` 重复。 |

### 第二模块实现状态（2026-08-04）

| 子模块 | 实现入口 | 当前裁决 |
|--------|----------|----------|
| 2.1 启动入口 | `GameCore.GameManager`，测试场景为 `Assets/Scenes/FoundationTest.unity` | 不新增 `Bootstrap`、`RuntimeContext` 或并行启动壳；直接使用并重构现有进程级入口，测试场景只用于地基运行验收，详细裁决见下节。 |
| 2.2 单局状态边界 | 当前无正式实现入口 | 不恢复泛化 `RuntimeContext`；但也不能继续把单局状态留在 `GameManager` 进程级系统层。等待真实单局 / 联机会话聚合裁决后迁移。 |
| 2.3 内容索引接入 | `GameCore.ResourceSystem.LoadAssetsByAssetTagAsync<T>`、`ScenarioDirector`、`ScenarioRun.ContentIndex`、`FoundationTest` | 资源发现和包优先级归 `ResourceSystem`；导演在开局时构建索引并让它随对应单局存续；不新增手工内容清单、`ContentCatalog` 或 Gameplay 加载器。 |
| 2.4 内容索引校验边界 | `ContentIndex`、`ContentValidationContext`、`ContentValidationReport` | 校验上下文仍是派生作者规则的协作者；报告、跨资产集合和索引公开集合都必须是真实只读视图；重复资产在校验层直接失败，不自动去重或覆盖。 |
| 2.5 单局内容句柄 | `ScenarioDirector`、`ScenarioRun.ContentIndex`、`ResourceHandle` | 导演唯一持有自己取得的资源句柄并与活动单局成对释放；单局领域对象只拥有冻结查询集合，不直接依赖 YooAsset。运行中 Mod 卸载协议留给正式 Mod 模块。 |
| 2.6 单局创建与结束 | `ScenarioDirector`、`ScenarioRun`、`Tabletop` | 同一时刻只存在一个活动单局；重复开局和无单局推进直接失败。结束后可创建独立新局，旧局牌桌拒绝继续写入；不新增 Session 壳或影子生命周期状态。 |
| 2.7 剧本场景组合 | `ScenarioRegionDefinition`、`ScenarioDirector`、`GameCore.SceneSystem` | 地区定义声明场景资源地址，剧本定义只引用初始地区；导演负责单局与地区场景的先后关系，技术切换仍由唯一 `SceneSystem` 执行。加载完成前不发布单局，结束时先关闭旧局和内容句柄再返回来源场景；不恢复固定场景名或直接 `SceneManager` 切换。 |
| 2.4 系统协作 | `GameManager` / `AGameSystem` / 直接系统依赖 | 已删除地图/读档双重生命周期分发、并行注册表和启动包装；`AGameSystem` 只保留初始化、启停和关闭，系统依赖由真实装配与启动校验承担。 |
| 2.5 事件 / 命令流 | `GameCore.GameCommandContext`、YokiFrame `EventKit` | 普通强类型事实事件直接走 `EventKit`；一对一请求事件、事件记录壳和 `GamePlayRuntimeLifecycleEvents` 已删除。没有真实权限、回放或跨模块稳定合同前不新增包装。 |
| 2.6 运行时实例身份 | 已完成延后裁决 | 静态内容身份继续唯一使用 `ContentId`；提前创建的 session / instance ID 已删除。运行时实例身份等第三模块出现真实牌桌对象、拆堆合堆和恢复消费者后再定义。 |
| 2.7 生命周期 / 场景切换 | YokiFrame `SceneKit`、`MapSystem`、`ResourceSystemSceneLoaderPool`、`TransitionSystem` | `SceneKit` 是唯一场景生命周期 owner；资源后端只选择默认包 / Mod 包，地图系统只持有场景地址，过渡系统只负责视觉。固定场景名和正式代码中的直接 `SceneManager` 加载链已删除。 |
| 2.8 最小测试和验收 | 地基 PlayMode、全量 EditMode、残留扫描、`.spec` lint | PlayMode `2/2` 通过；EditMode `304` 通过、`1` 条条件跳过、`0` 失败；第二模块重复 owner、StackCraft 依赖和固定场景残留扫描为空。 |

### 2.1 启动入口与生命周期裁决（2026-08-04）

#### 证据对照

| 来源 | 当前事实 | 对裁决的意义 |
|------|----------|--------------|
| 当前 `GameCore.GameManager` | `Start` 依次初始化 `ResourceSystem`、`ModAPI`、GAS 和全部 `AGameSystem`；`OnDestroy` 逆序停止 GAS、关闭 Mod 与资源系统。 | 已有进程级初始化职责，不应另建 GamePlay 启动包装。 |
| 当前场景 / 资产 | `Assets/Scenes/FoundationTest.unity` 已装配唯一 `GameManager`，并引用 `Assets/Scenes/FoundationTestConfig.asset`；场景已加入 Build Settings。 | GameManager 基础设施已有真实 Unity 测试入口，但该场景不是正式单局入口，不能把测试装配冒充 StackCraft 单局流程已落地。 |
| 当前系统收集 | `FindSystems` 使用 `FindObjectsByType<AGameSystem>` 扫描当前已加载对象，并按具体类型注册；当前 `GameManager` 已公开启动状态，但扫描到的系统仍可能是场景对象。 | 启动状态已有真实入口；系统归属、初始化顺序和场景系统脱离进程根的问题仍未解决，不适合把现有扫描方式原样保留。 |
| 当前 GAS 初始化 | `FormalAbilityRuntimeBootstrap` 不再通过 `BeforeSceneLoad` 自动初始化；`GameManager.Start` 在资源与 Mod 初始化后唯一调用 `EnsureInitialized`。 | 初始化职责已收口到 GameManager 的显式顺序，未来 Mod 标签或内容进入 GAS 时有明确前置入口。 |
| StackCraft | `GameDirector.prefab` 确实放在 `Title` 场景，通过 `DontDestroyOnLoad` 常驻；同一个类还负责扫档、新局、读档、保存、场景旅行和跨场景卡牌搬运。 | 吸收“入口必须真实装配、流程必须显式”的经验；排除常驻上帝类、固定场景名和业务状态混装。 |
| UE Gameplay Framework | 官方 `UGameInstance` 从游戏创建持续到关闭，`GameInstanceSubsystem` 与它同生命周期；`GameMode` 则随关卡 / 对局创建，不跨关卡常驻。 | 进程基础设施与单局 / 场景规则必须分层，不能继续塞进同一个 Manager。官方参考：`https://dev.epicgames.com/documentation/unreal-engine/gameplay-framework-in-unreal-engine`、`https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/UGameInstance`。 |

#### 正式裁决

- **不新增 GamePlay 启动壳。** `GameCore.GameManager` 是现有进程级职责入口，应直接重构它；不得再增加 `Bootstrap`、`RuntimeContext`、薄包装或同职责 `GameDirector`。当前新增的测试场景只是验证装配，不是新的启动壳。
- **保留 `GameManager` 名称，但缩窄职责。** 它只应编排整个应用生命周期的基础设施初始化、就绪 / 失败状态和逆序关闭，例如资源、Mod、GAS；不拥有新局、读档、剧本规则、牌桌状态、玩家队伍或地图业务。
- **完整游戏流程不在 2.1 创建。** StackCraft `GameDirector` 的 NewGame / LoadGame / SaveGame / Travel 流程关系留给 2.2、2.7 和存档模块；只有这些真实编排职责成立后，才允许使用 `GameDirector` 名称。5.4 的 `ScenarioDirector` 只编排当前剧本及其子模块生命周期。
- **进程系统与场景系统必须拆开。** 当前全局扫描 `AGameSystem` 会把输入、玩家、地图、UI 和存档等带场景引用的系统一起交给进程根；该注册方式不能原样保留。具体拆分放在 2.2 / 2.4，不在 2.1 猜系统列表。
- **GAS 初始化只能有一个正式时序入口。** 当前顺序是资源可用 -> Mod / 内容包准备 -> GAS 标签和生成缓存准备 -> 依赖 GAS 的运行时系统启动；旧的 `BeforeSceneLoad` 自动入口已删除，保留 `GameManager` 的显式入口。
- **正式启用必须有真实 Unity 装配。** 地基已通过专用测试场景验证；后续正式单局实现仍需选择明确的首场景 / 常驻根入口，不能只保留静态类或依赖任意业务场景碰巧挂有 `GameManager`。

#### 本小步不做

- 不创建正式产品启动场景、常驻 Prefab 或单局 GameConfig 资产；专用地基测试场景和测试配置不承担这些产品职责。
- 不迁移新局、读档、存档、地图切换和跨场景角色数据。
- 不决定 `AGameSystem` 的最终替代结构。
- 不实现 Mod、联机或 GameplayTag 动态合并。

#### 当前验收与下一小步

2.1 已完成进程级启动闭环的基础验收：测试场景中的启动状态为 `Ready`，资源系统、YooAsset、ModAPI 和 GAS 均可运行，退出后资源系统、YooAsset、ModAPI 释放且 GAS 停止，重复 `GameManager` 被拒绝。2.2 已完成架构裁决；2.3 已完成内容包发现和索引接入；下一步进入 2.4 系统协作时，必须继续以现有 `GameManager` / `EventKit` / `ResourceSystem` 为候选职责归属，不恢复并行上下文或启动壳。

### 2.2 单局状态边界裁决（2026-08-04）

#### 本小步的范围

本小步只回答一件事：**一局游戏的运行状态由谁拥有、什么时候创建、什么时候销毁，以及存档和场景分别处在什么位置。**

不在本小步实现新局按钮、读档界面、荒岛剧本、牌桌、行动、任务、日结、联机或 Mod。未来预留只体现在生命周期边界和数据归属上，不提前创建没有真实字段和消费者的通用容器。

#### 当前项目证据

| 对象 | 当前实际职责 | 生命周期判断 |
|------|--------------|--------------|
| `GameManager` | 初始化 `ResourceSystem`、`ModAPI`、EX-GAS 和已发现的 `AGameSystem`；维持进程级入口和启动状态。 | 属于应用进程级基础设施入口，不应保存某一局的牌桌、角色、地图或剧本状态。 |
| `SaveSystem` | 向各正式系统收集 `SaveDataBlock`，交给 YokiFrame `SaveKit` 写入文件；读档时把快照分发给各系统。 | 是文件和快照协调者，不是运行状态真相源，也不应复制一份角色、地图或牌桌状态。 |
| `GameFlagSystem` | 保存布尔世界标记，并实现 `GameFlagsDataBlock` 的生成和恢复。 | 标记属于当前存档 / 单局状态；系统可由框架管理，但状态必须随新局创建、随单局结束清空或替换。 |
| `MapSystem` | 保存当前地图、检查点栈和地图切换过程；`MapDataBlock` 保存其运行快照。 | 地图状态属于当前单局；场景只是地图表现和对象载体，场景名不能成为剧本身份。 |
| `PlayerSystem` | 保存玩家主角色、当前控制角色和控制组；`PlayerDataBlock` 保存其运行快照。 | 当前控制和队伍状态属于当前单局 / 玩家席位，不能被进程根永久当成上一局真相。 |
| `PersistenceSystem` | 保存预实例化和运行时实例化对象，负责按持久化标识恢复对象。 | 持久化对象属于当前单局世界；运行时实例标识只在实例存在时产生，不在地基阶段提前创建。 |
| `GameStateSystem` | 管理菜单、对话、Gameplay 的交互状态栈，切换输入映射和时间缩放。 | 这是应用 / 表现流程状态，不是需要写入局内存档的世界状态；新局结束时必须能明确重置，不能让状态栈跨流程累积。 |
| StackCraft `GameDirector` | 把新局、读档、保存、场景旅行、当前场景和跨场景卡牌搬运放在一个常驻单例中。 | 吸收显式流程和快照范围，排除上帝类、固定场景名、单例互相调用和专用 traveler 搬运协议。 |

#### 正式裁决

1. **不新增 `GamePlayRuntimeContext`、`RunOwner`、`SessionOwner` 或空的 `GameDirector`。** 当前 CardLoop 还没有正式的新局入口，也没有已经确定的完整游戏流程字段；现在创建它们只会制造第二套状态容器和网页 / 后端式命名。5.4 已建立的 `ScenarioDirector` 只负责活动剧本生命周期；未来只有真实承担新局、读档、场景组合、保存恢复或结束单局时，才按完整流程建立 `GameDirector`。
2. **进程服务和单局状态分离。** `GameManager` 只负责进程级基础设施；`SaveSystem` 只负责文件和快照协调；地图、玩家、持久化对象、世界标记等动态数据由各自正式系统拥有，不能被 `GameManager` 再汇总成一份可修改的副本。
3. **系统可以常驻不等于系统状态可以跨局常驻。** 后续若某个系统为了场景切换需要常驻，必须在新局开始、读档、返回局外和结束单局时清楚地重置或替换其状态；不得靠重新加载场景、静态字段或单例残留碰运气。
4. **存档是运行状态的快照，不是第二真相。** StackCraft `GameData/SceneData` 值得吸收的是全局快照与场景局部快照分层。CardLoop 的正式存档以后应由运行系统生成快照，并包含剧本 / 世界状态；场景加载名、YooAsset 地址、内容 ID 和运行时实例标识各自只承担加载、内容引用或实例定位职责。
5. **场景是载体，不是单局。** 一个剧本可以由多个场景组成，也可以在同一场景中切换多个地区；不能用 `Scene.name` 代表剧本、世界或单局身份。未来切换场景时，先由流程编排请求场景变化，再由地图 / 场景职责加载，最后由状态恢复流程绑定动态对象。
6. **为联机预留的是权威边界，不是网络代码。** 未来单机时，玩家输入可以由本地流程提交；联机时，当前单局的规则状态应由主机 / 服务器裁决，客户端只提交命令并接收可见状态。现在不添加网络状态类，但后续任何直接从 UI、Mod 或表现对象修改世界状态的入口都不得成为正式设计。
7. **Mod 预留只记录依赖快照边界。** 未来读档需要知道这局启用了哪些基础包和 Mod 版本 / 依赖解析结果，但这些是内容加载会话的元数据，不是新的内容 ID，也不由 `SaveSystem` 或单局容器手工维护第二份。

#### 本轮识别与处理结果

以下问题均由源码直接证明；其中不依赖 `AGameSystem` 最终分层的三项已立即重构：

- **已修正系统装配范围。** `GameManager` 不再用 `FindObjectsByType<AGameSystem>` 扫描整个已加载场景，只登记自身层级中明确装配的 `AGameSystem`。这样随 `GameManager` 常驻的只有同一根层级，其他场景对象不会被误缓存为进程系统。
- **待 2.4 拆分生命周期职责。** `AGameSystem` 仍同时提供进程启动、地图切换和读档回调；需要按真实系统逐个判断进程级、单局级和场景级职责，不能新增泛化上下文掩盖。
- **已修正状态启停幂等。** `GameStateSystem` 现在拒绝重复启动，启动失败会回收事件订阅；停止时清空状态栈并恢复 `Time.timeScale`，重复启停不再累积状态层。
- **已修正读档完成时机。** `MapSystem.LoadDataBlock` 现在可在过场、地图生命周期和落点校验完成后回调；`SaveSystem` 只在该回调到达后派发读档完成生命周期并记录加载成功。完整存档 schema、版本迁移和失败传播仍留给正式存档模块。
- **测试保护。** `GameManagerAndGameStateLifecycleEditModeTests` 覆盖明确层级装配、重复启停和过场完成回调三条行为。

#### 本小步保留 / 排除 / 延后

| 分类 | 结论 |
|------|------|
| 保留吸收 | StackCraft 的显式新局 / 读档 / 保存 / 返回 / 结束流程关系；全局快照与场景局部快照分层；切换前暂停、切换后恢复；跨场景状态需要可恢复而不是依赖场景对象仍在。 |
| 排除 | `GameDirector` 上帝类、`public static Instance` Manager 链、固定 “Title” / “Main” 场景名、`incomingTravelers` 特殊搬运列表、直接从各 Manager 互改状态。 |
| 现有职责继续承担 | `GameManager` 进程启动；`SaveSystem` 文件和快照聚合；`ResourceSystem` / YooAsset 资源生命周期；`ModAPI` Mod 生命周期；EX-GAS 能力、效果和标签语义；YokiFrame `EventKit` 普通事件派发。 |
| 延后到真实模块 | 单局具体状态对象、正式 `Director` 流程入口、运行时实例身份、剧本 / 世界 / 牌桌 / 行动 / 目标数据接入、存档版本迁移、网络权威和 Mod 依赖恢复。 |

#### 进入后续模块的门槛

在 2.4 系统协作和 2.7 生命周期 / 场景切换完成前，不进入牌桌运行时实现。系统扫描和启停幂等已经消除；下一步必须继续拆清 `AGameSystem` 生命周期职责，并补齐正式场景恢复和失败传播，不能把当前回调链越权称为完整存档模块。

### 2.3 内容索引接入裁决（2026-08-04）

#### 本小步的范围

本小步只解决一条链路：**YooAsset 已加载的默认包 / Mod 包内容如何进入第一模块的唯一 ID 索引。** 不实现剧本、牌桌、行动、配方、任务、职业、技能、联机或运行时 Mod 热重载。

#### 证据对照

| 来源 | 当前事实 | 对裁决的意义 |
|------|----------|--------------|
| StackCraft `CardManager` / `CraftingManager` | 通过 `Resources.LoadAll` 扫描固定目录，启动时把定义塞进临时字典。 | 只吸收“启动后形成查询索引”的需求；固定目录和 `Resources` 入口排除。 |
| 当前 `ResourceSystem` / `ModLoader` | 默认包由 `YokiFrame.YooInit` 初始化；启用 Mod 各自加载成独立 YooAsset 包，并由 `ResourceSystem` 按包生命周期回收。 | 资源发现、包选择、句柄和 Mod 包优先级只能归 `GameCore.ResourceSystem`，不能由 GamePlay 新造加载器。 |
| YooAsset 3.0.5 本地源码 / 官方示例 | `ResourcePackage.GetAssetInfos(string tag)` 获取清单中的资源信息，再用 `LoadAssetAsync(AssetInfo)` 逐个加载；`GetAllAssetInfos()` 返回的 `AssetInfo.AssetType` 为空，不能用它做类型筛选。 | 使用一个专用 YooAsset 资源标签做构建期分类，运行时加载后再按 `ContentAsset` 类型校验；不通过全包盲扫替代正式标签入口。 |
| 当前第一模块 | `ContentIndex.Build` 只接收已加载 `ContentAsset`，校验唯一 ID 后建立查询表。 | 索引仍只负责派生查询，不拥有资源发现、包加载或作者清单。 |

#### 正式裁决

1. **不创建 `ContentCatalog`、`GamePlayContentLoader`、手工 `GamePlayContentSet` 或第二套资源地址表。** 这些职责没有独立真相源，且会和 `ResourceSystem` / YooAsset 抢资源所有权。
2. **`ResourceSystem.LoadAssetsByAssetTagAsync<T>` 是唯一跨包批量入口。** 它按默认包后 Mod 包的稳定顺序读取 YooAsset 资源标签，持有逐资产句柄，返回给调用方统一释放；Mod 包只要已由 `ModLoader` 正式加载，就自动参与内容发现。
3. **本段原“进程级 `ContentRegistrySystem` 持有内容索引”的裁决已被 2026-08-11 的 2.1a 迁移取代。** 当前由 `ScenarioDirector` 在开局时调用 `ContentIndex.Build`，成功后把索引交给 `ScenarioRun`，并在单局结束时释放加载句柄；`GameManager` 不保存索引副本。
4. **`gameplay-content` 只是 YooAsset 构建清单标签。** 它用于资源发现，不是 EX-GAS GameplayTag，不是内容 ID，不参与规则查询、存档或联机引用。
5. **构建期自动收集取代人工登记。** `ContentAssetFilterRule` 以 `ContentAsset` 继承关系过滤 `Assets` 下的作者资产，写入 `gameplay-content` 标签；作者只创建一次 SO，不需要同时登记内容清单、地址或 GUID。
6. **内容资源禁用地址生成。** YooAsset 收集规则使用 `AddressDisable`，因为内容唯一身份已经由 `ContentId` 承担；YooAsset 地址只在需要加载具体表现资源时出现，不能成为内容定义的第二 ID。
7. **跨包重复内容 ID 直接失败。** 默认包和 Mod 包的内容会一起进入 `ContentIndex.Build`；如果 Mod 与基础包声明相同 ID，校验失败，不静默覆盖。覆盖 / 替换语义留到 Mod 冲突规则真正裁决时再设计。
8. **当前内容快照随单局开局建立。** 本小步不实现启用 / 禁用 Mod 后的运行时索引重建，也不宣称支持 Mod 热重载；动态刷新需要先有明确的 Mod 会话生命周期和索引替换时机，不能让一部分旧索引和一部分新包并存。

#### 本轮实现与删除

| 类型 | 内容 |
|------|------|
| 历史新增 | `ResourceSystem.LoadAssetsByAssetTagAsync<T>` 和其跨包句柄状态；曾新增但已于 2026-08-11 删除的 `ContentRegistrySystem`；YooAsset 自定义 `ContentAssetFilterRule` 过滤规则；地基测试场景中的 `地基测试卡牌` 作者资产；真实 PlayMode 测试。 |
| 重构 | `ResourceSystem.Shutdown` 删除对 YooAsset `DestroyPackageOperation` 的同步等待；YooAsset 3.0.5 该操作不支持 `WaitForCompletion`，全局关闭改为释放项目句柄后直接调用官方 `YooAssets.Destroy()`。 |
| 保留 | `ContentIndex` 的校验和唯一 ID 查询职责；`SoftAssetReference` 只负责具体表现资源的地址加载；`ModLoader` 继续负责 Mod 包初始化。 |
| 明确不吸收 | StackCraft `Resources.LoadAll`、固定 `Cards/Packs/Recipes` 目录、运行时临时定义数据库、静默重复 ID、独立 GamePlay 加载器和第二套资源包注册表。 |

#### 验收

- 新鲜 Unity `6000.5.4f1` 编译：`Temp/codex-gameplay-module23-compile-20260804.log`，退出码 `0`。
- 场景生成：`Temp/codex-gameplay-module23-rebuild-scene-20260804.log`，地基测试场景包含唯一 `ContentSystem`，并生成测试作者资产。
- 真实 PlayMode：`Temp/codex-gameplay-module23-playmode-r2-20260804.log` 与 `Temp/codex-gameplay-module23-playmode-results-r2.xml`，`FoundationScene_LoadsTaggedContentIntoIndex` 通过；启动状态为 `Ready`，索引成功查询 `test.foundation.card`，退出未再出现 `DestroyPackageOperation` 同步等待异常。
- YooAsset 模拟清单包含 `gameplay-content`、`Assets/GamePlay/Tests` 和 `地基测试卡牌.asset`，证明内容收集不是场景引用自证。

#### 下一小步

2.3 已完成；进入 2.4 前仍不能进入牌桌实现。下一步审查 `AGameSystem` 的进程 / 场景 / 单局生命周期与系统协作顺序，重点确认内容系统与未来牌桌系统之间的启动依赖，禁止再添加并行注册表或 `RuntimeContext`。

### 2.4.1 地图 / 读档通知单通道裁决（2026-08-04）

#### 本小步的范围

本小步只解决 `AGameSystem` 同时承担系统启停、地图切换和读档回调的问题。它不重做系统注册表，不实现异步依赖排序，不引入单局容器，也不处理牌桌、行动、事件回放或联机命令。

#### 现有问题证据

| 证据 | 现实问题 |
|------|----------|
| `AGameSystem` 同时声明 `OnSystemInit/Start/Stop`、四个地图回调和 `OnSaveFileLoaded`。 | 每个系统被迫继承一组与自身无关的空生命周期，进程系统、地图系统和存档监听者边界混在同一个父类。 |
| `GameManager.LifecycleRuntime` 先遍历所有 `AGameSystem` 调用地图 / 读档回调，再发送同语义 `EventKit` 事件。 | 同一现实通知存在两条正式路径；系统作者必须猜该覆写父类还是订阅事件，未来容易重复执行。 |
| `PlayerCameraRig` 已直接订阅 `MapLoadedEvent/MapUnloadedEvent`，而 `PersistenceSystem`、`PlayerSystem`、`UISystem` 使用父类回调。 | 当前项目本身已经混用两套机制，不是为了兼容第三方而保留的必要差异。 |
| StackCraft `QuestManager` 等系统在 `Start` 订阅多个 Manager 事件，在 `OnDestroy` 注销。 | 值得吸收的是显式订阅和注销关系；不吸收单例 Manager 依赖和偶然 `Start` 顺序。 |

#### 系统生命周期分类

| 分类 | 当前系统 | 本小步处理 |
|------|----------|------------|
| 进程级基础 / 表现 | `AudioSystem`、`InputSystem`、`TransitionSystem`、`UISystem` | 继续由 `GameManager` 显式装配和启停；不接收地图父类回调。内容索引不属于进程级系统。 |
| 当前单局可变状态 | `GameFlagSystem`、`MapSystem`、`PlayerSystem`、`PersistenceSystem`、`SaveSystem` | 仍可挂在常驻根，但状态必须由各自系统拥有并在后续单局流程明确重置；地图 / 存档通知直接订阅 `EventKit`。 |
| 应用交互状态 | `GameStateSystem` | 继续负责输入映射和暂停层；它不是世界存档真相，停止时必须清空。 |
| 场景级对象 | `MapInfo`、`Persistable`、`PlayerCameraRig` 等普通组件 | 不进入 `AGameSystem` 注册表；通过明确登记或 `EventKit` 响应当前场景变化。 |

该分类只说明职责和后续重构方向，不在本小步创建新的父类、接口或容器。UE 的 `GameInstanceSubsystem` / 关卡对象分层仍作为成熟框架校准：常驻系统和关卡对象生命周期分开，但不照搬 UE 类名。

#### 正式裁决与实现

1. **`AGameSystem` 只保留 `OnSystemInit/OnSystemStart/OnSystemStop`。** 它继续作为现有 GameManager 装配系统的技术基类，不再伪装成地图和存档生命周期总线。
2. **删除 `GameManager.LifecycleRuntime`。** `GameManager` 不再遍历系统后重复发送事件，也不保留 `DispatchMap*` / `DispatchSaveFileLoaded` 薄包装。
3. **事件发送回到真实来源。** `MapSystem` 在开始 / 完成加载和卸载时直接发送 `MapLoadingEvent`、`MapLoadedEvent`、`MapUnloadingEvent`、`MapUnloadedEvent`；`SaveSystem` 在地图恢复和落点校验完成后直接发送 `SaveFileLoadedEvent`。
4. **监听者显式订阅和注销。** `PersistenceSystem` 订阅地图加载 / 卸载事件；`PlayerSystem` 订阅地图加载 / 存档完成事件；`UISystem` 订阅存档完成事件。监听生命周期统一跟随各系统的 `OnSystemStart/OnSystemStop`。
5. **不新增事件包装。** 事件类型仍归 `GameCore`，派发机制直接使用 YokiFrame `EventKit`；没有 `GameRuntimeEvents`、生命周期适配器或第二事件总线。
6. **不改变存档完成语义。** `SaveFileLoadedEvent` 仍只在 `MapSystem.LoadDataBlock` 的完成回调到达后发送，不能因为去掉 GameManager 转发就提前通知。

#### 验收

- 源码扫描不再存在 `AGameSystem.OnMap*`、`AGameSystem.OnSaveFileLoaded`、`GameManager.DispatchMap*`、`GameManager.DispatchSaveFileLoaded` 或 `m_lifecycleEventsEnabled`。
- 定向 EditMode：`Temp/codex-gameplay-module241-editmode-results.xml`，`7/7` 通过，覆盖系统装配、状态幂等、地图恢复完成时机和持久化对象通过 `MapLoadedEvent` 恢复。
- 地基 PlayMode：`Temp/codex-gameplay-module241-playmode-results.xml`，内容索引启动链继续通过。
- EditMode 全套：`Temp/codex-gameplay-module241-editmode-full-results.xml`，`299` 通过、`1` 跳过、`1` 个既有失败；唯一失败仍是项目缺少测试要求的 2D 碰撞层，层号为 `-1`，与本小步无关。

#### 下一小步

2.4.2 审查 `GameManager` 当前按层级发现顺序初始化系统的问题：明确哪些系统存在真实前置依赖、是否需要显式顺序或依赖校验，以及启动中途失败时如何只回收已经启动的系统。不得用 Script Execution Order、组件排列碰运气，也不得恢复并行注册表。

### 2.4.2 系统启动依赖与失败回收裁决（2026-08-04）

#### 本小步的范围

本小步只处理现有 `GameManager` 装配系统的初始化、启停、最终释放和启动失败回收。不创建第二注册表、依赖注入容器、`RuntimeContext`、`Bootstrap` 或新的 GamePlay 启动入口，也不借启动排序提前实现牌桌、剧本、Mod 热重载或联机系统。

#### 真实依赖证据

| 消费系统 | 启动阶段直接行为 | 必须先就绪的系统 | 裁决 |
|----------|------------------|------------------|------|
| `PlayerSystem` | `OnSystemStart` 直接调用持久化系统登记主玩家对象。 | `PersistenceSystem` | 声明为真实启动依赖。 |
| `GameStateSystem` | `OnSystemStart` 压入初始状态，并立即调用输入系统切换 Action Map。 | `InputSystem` | 声明为真实启动依赖。 |
| `InputSystem` | 运行期间从 `PlayerSystem` 查询控制目标。 | 无启动依赖 | 查询发生在输入回调 / Update，不为此提前声明启动依赖。 |
| `MapSystem` | 运行期间请求 `TransitionSystem` 执行地图过场。 | 无启动依赖 | 地图切换不发生在系统启动阶段，留给 2.5 处理一对一协作方式。 |
| `SaveSystem`、`UISystem`、`PersistenceSystem` 等 | 启动时只登记自身监听或建立本地状态。 | 无新增依赖 | 不根据未来可能协作猜依赖。 |

#### 正式裁决与实现

1. **只保留现有 `GameManager` 注册表。** 系统仍由 `GameManager` 层级中的 `AGameSystem` 组件装配；没有第二份手工系统清单，也没有平行容器。
2. **依赖由消费系统自己声明。** `AGameSystem.StartupDependencies` 只表达“进入初始化 / 启动前必须已经就绪”的真实前置条件，不表达普通运行时调用、事件监听或未来设想。
3. **启动顺序由依赖图生成。** `GameManager` 在发现系统后做稳定拓扑排序；没有依赖的系统继续保持场景装配顺序，有依赖的系统必定排在依赖项之后。缺失依赖、自依赖、非法类型和循环依赖在启动前直接失败，不靠 Hierarchy 顺序或 Script Execution Order 碰运气。
4. **分别记录已初始化和已启动系统。** 初始化、启动、停止和最终释放不再遍历整个字典猜状态；启动中途失败时，当前失败系统先清理自身已经进入的阶段，再逆序回收此前真正进入该阶段的系统，尚未进入的系统不会收到伪生命周期。
5. **增加一次性的最终释放阶段。** `OnSystemStop` 仍服务组件禁用 / 重新启用时的监听注销；`OnSystemShutdown` 只在进程关闭或启动失败时释放初始化阶段持有的长期资源。历史上的 `ContentRegistrySystem` 曾据此释放长期句柄，但该登记器已删除；当前内容句柄跟随单局释放。
6. **基础设施按启动逆序关闭。** GameCore 系统先释放，再关闭 GAS、ModAPI 和 ResourceSystem；每个基础设施只在实际进入过初始化后回收，并且单项关闭失败不会阻止后续资源继续释放。
7. **ModAPI 未完成初始化即视为启动失败。** `ModAPI.Initialize` 返回后必须确认正式初始化状态，不能在 Mod 发现失败或初始化未完成时继续启动依赖其内容包的系统。

#### 明确不做

- 不为 `MapSystem -> TransitionSystem`、`InputSystem -> PlayerSystem` 等运行期间协作滥加启动依赖。
- 不增加“优先级整数”、手工执行顺序数组或 Inspector 双重登记。
- 不把系统依赖声明扩成服务定位、命令总线、网络权限或单局状态容器。
- 不要求未来系统为了进入注册表继承新的空壳层；当前技术基类只承载现有统一生命周期。

#### 验收

- 定向 EditMode：`Temp/codex-gameplay-module242-editmode-results.xml`，`8/8` 通过。覆盖真实层级发现、依赖优先顺序、逆序停止 / 最终释放、缺失依赖、循环依赖、初始化失败和启动失败回收。
- 地基 PlayMode：`Temp/codex-gameplay-module242-playmode-results.xml`，`1/1` 通过。内容索引仍能通过 YooAsset 标签加载 `test.foundation.card`，统一 `OnSystemShutdown` 没有破坏句柄释放。
- 源码确认仅存在一个 `GameManager` 系统字典和一份派生执行顺序；没有新增 `Registry`、`RuntimeContext` 或 `Bootstrap`。

#### 下一小步

2.5 审查事件 / 命令流：一对多结果通知继续直接使用 YokiFrame `EventKit`；一对一必需协作优先回到明确方法调用。重点处理 `MapTransitionDelegationRequestedEvent` 携带可变委托的问题，不建立第二事件总线或命令总线。

### 2.5 事件 / 命令流裁决（2026-08-04）

#### 本小步的范围

本小步只处理现有 GameCore 中“请求通过事件绕到唯一执行者”的链路，以及保留真正适合广播的 EventKit 事实 / 表现事件。不建立 `GameRuntimeEvents`、`GameEventBus`、命令总线或 GamePlay 事件包装层；`ICommand` / `IContextualCommand` / `GameCommandContext` 继续作为现有命令与归因入口。

#### 逐项裁决

| 原链路 | 当前接收者 / 用途 | 裁决 |
|--------|------------------|------|
| `MapTransitionDelegationRequestedEvent` + 三个可变回调 | `MapSystem` 请求唯一 `TransitionSystem` 执行过场。 | 删除事件和 `MapLoadingDelegationParams`；`MapSystem` 直接调用 `TransitionSystem` 的 SceneKit 过渡接口。开始 / 完成事实由拥有整段地图流程的 `MapSystem` 通过 EventKit 广播。 |
| `MenuRequestedEvent` + `TaskCompletionSource` | `OpenMenu` 命令等待唯一 `UIManager` 打开并关闭菜单。 | 删除请求事件；`UISystem` 负责 UI 实例生命周期并提供 `OpenMenuAsync`，由现有 `UIManager` 完成菜单栈和关闭任务。没有订阅时不再静默丢失并永久等待。 |
| `CloseAllMenusRequestedEvent` | 只有 `UIManager` 执行关闭菜单。 | 删除请求事件；`CloseMenus`、玩家死亡和 UI 内部动作直接调用 `UISystem.CloseAllMenus`。 |
| `ReturnToMainMenuRequestedEvent` | 只有 `GameStateSystem` 执行返回主菜单。 | 删除请求事件；UI 面板直接调用 `GameStateSystem.ReturnToMainMenu`。场景实际加载方式留给 2.7 改为 YooAsset。 |
| `AudioPlaybackRequestedEvent` | 多个游戏 / UI / 表现对象发起无返回值音频请求。 | 保留 EventKit 广播；它是表现通知，不返回可变回调，也不承载音频资源真相。 |
| `MapLoadingEvent`、`MapLoadedEvent`、`MapUnloadingEvent`、`MapUnloadedEvent`、`SaveFileLoadedEvent` | 多个系统依据地图 / 存档事实进行恢复、快照或 UI 响应。 | 保留 EventKit；事实由真正产生它的 `MapSystem` / `SaveSystem` 发送。 |
| `GameCommandContext` | 命令来源、发起者、角色和未来远端玩家归因。 | 保留现有类型；它不是事件总线，也不重复拥有世界状态或网络同步状态。 |

#### 正式实现

- `TransitionSystem` 不再订阅唯一请求事件，改为实现 YokiFrame `ISceneTransitionUniTask`，只持有淡入淡出表现状态。
- `MapTransitionStartedEvent` / `MapTransitionCompletedEvent` 的发送方统一为 `MapSystem`；它拥有地图切换管线和并发保护，输入锁定等观察者继续使用 EventKit。
- `UIManager` 的菜单运行时初始化改为幂等；`UISystem` 作为已经存在的 UI 生命周期系统持有其运行时入口，直接暴露菜单打开 / 关闭方法，不创建新的 UI 桥接类。
- 删除 `GameCoreUiEvents.cs` 及其中三个一对一请求事件；GameCore 事件目录不再保留无消费者的请求定义。
- 保留 `ICommand`、`IContextualCommand` 和 `CommandExecutionExtensions`，命令执行仍是直接调用、可等待、可携带 `GameCommandContext` 的链路；没有新增命令路由层。

#### 验收

- 当前生命周期定向 EditMode：`8/8` 通过。覆盖 SceneKit 过渡接口、直接系统协作和第二模块既有生命周期保护。
- 源码扫描：`MapTransitionDelegationRequestedEvent`、`MapLoadingDelegationParams`、`MenuRequestedEvent`、`CloseAllMenusRequestedEvent`、`ReturnToMainMenuRequestedEvent` 在正式源码中均无残留。
- EventKit 项目用法已同步至 `.spec/knowledge/features/ai-quick/event-system.md`，明确一对一直接调用与广播事实的边界。

#### 下一小步

2.6 审查运行时实例身份：在当前没有牌桌运行时实体的事实下不新增 `RuntimeInstanceId`、`SessionId` 或通用实例容器；只确认 `ContentId`、`Persistable` 标识和未来实例定位各自不重复承担内容身份。

### 2.6 运行时实例身份裁决（2026-08-04）

#### 证据对照

| 身份 / 定位值 | 当前现实用途 | 是否是 GamePlay 内容 ID |
|---------------|--------------|--------------------------|
| `ContentId` | 作者维护的静态内容身份，用于内容索引、未来存档 / 联机 / Mod 引用。 | **是，唯一正式内容 ID。** |
| StackCraft `CardData.Id` | 保存 `CardDefinition.Id`，依靠卡堆列表位置重建卡牌；模板没有独立卡牌实例 ID。 | 只是旧模板的静态定义 ID，不吸收为本项目实例身份。 |
| `Persistable` identifier | 标识一份可恢复的世界对象存档；运行时创建时可生成 GUID，读档时用于找回同一对象。 | 否，只是持久化实例定位。不能替代卡牌 / 角色 / 地点的内容 ID。 |
| `DatabaseEntryReference.guid` | 旧 GameCore 数据库资产和 prefab 的 Unity 资产定位 / 迁移引用。 | 否。旧数据库仍可服务其现有框架资产，但不得扩展成 GamePlay 内容身份。 |
| `SoftAssetReference.Address` / YooAsset 地址 | 定位并加载资源。 | 否，只负责资源位置。 |
| Unity `GetInstanceID` / `GetEntityId` | UI 关闭任务和诊断集合中的本进程临时字典键。 | 否，不存档、不联网、不暴露给作者。 |
| 角色动作锁内部随机 key | 同一角色对象内部移除临时速度修饰或动作锁的令牌。 | 否，只在持有者闭包内短暂有效。 |

#### 正式裁决

1. **本小步不新增代码。** 当前没有正式牌桌运行时实体，也没有已确认的实例引用、复制、拆堆、合堆、联机同步或存档恢复合同；创建通用 `RuntimeInstanceId`、`SessionId`、实例注册表或实体容器只会冻结错误抽象。
2. **静态内容身份和动态实例身份严格分开。** 未来一张卡牌实例即使引用同一个 `ContentId`，也可能有不同耐久、持有者、堆位置、装备和可见性；实例身份只能在模块 3 的真实牌桌实体出现后按这些消费者设计。
3. **现有 `Persistable` 标识继续限定在可恢复世界对象。** 它不能被重命名或包装成所有运行时实体的通用 ID；未来牌桌实体是否直接使用持久化标识，要等存档、联机和牌桌生命周期共同裁决。
4. **不吸收 StackCraft 的缺口。** StackCraft 通过卡堆顺序和定义 ID 重建卡牌，无法支持跨堆引用、秘密信息、联机指令目标和稳定事件回放；该缺口不能通过现在随手加一个 GUID 字段冒充解决。
5. **未来创建门槛明确。** 只有模块 3 出现至少一个真实消费者，例如卡牌实例互相引用、联机命令指定目标、存档跨结构恢复或事件回放，才定义实例身份；届时由权威运行时创建并保证作用域唯一，作者不手填，也不成为第二内容 ID。

#### 静态验收

- GamePlay 正式源码中不存在 `RuntimeInstanceId`、`SessionId`、通用实例注册表或第二套内容身份字段。
- `ContentId` 明确禁止 Unity GUID、YooAsset 地址、文件路径和运行时实例号。
- 现有随机 GUID 的使用范围只命中 `Persistable` 恢复标识和角色内部临时动作令牌；Unity 实例号只命中诊断 / UI 运行时字典。

#### 下一小步

2.7 重构地图 / 场景生命周期：复用 YokiFrame `SceneKit`，并让 `ResourceSystem` 只承担 YooAsset 默认包 / Mod 包选择；删除 `SceneManager.LoadSceneAsync(string)` / `UnloadSceneAsync(string)` 的正式地图加载链。场景地址只作为加载定位，不能成为地图内容 ID 或剧本身份。

### 2.7 地图 / 场景生命周期裁决（2026-08-04）

#### 吸收与排除

- 吸收 StackCraft `GameDirector.TravelSequence` 中“过场开始 -> 卸载旧地图 -> 加载新地图 -> 激活 -> 过场结束”的流程关系，以及旅行完成后才继续后续逻辑的时序要求。
- 排除固定 `Title` / `Main` / `Island` 场景名、`SceneManager.LoadSceneAsync(string)`、`SceneManager.UnloadSceneAsync(string)` 和按场景名搬运业务数据。场景地址只是 YooAsset 定位值，不是地图、剧本或存档身份。
- 不新增第二套 SceneSystem、场景注册表或公开场景句柄。YokiFrame `SceneKit` 是场景生命周期 owner；`ResourceSystem` 只通过 ResKit 官方扩展点选择默认包 / Mod 包，`MapSystem` 负责地图语义和事件，`TransitionSystem` 只负责过场表现。

#### 实现结果

- 删除重复的 `SceneResourceHandle` 和 `ResourceSystem.LoadSceneAsync`；新增内部 `ResourceSystemSceneLoaderPool`，在 SceneKit 的 ResKit 扩展点上选择默认包 / Mod 包并复用 YokiFrame YooAsset 加载器。
- `MapSystem` 只持有当前场景地址，通过 `SceneKit.LoadSceneUniTaskAsync` 以 `Single` 模式切换地图；`TransitionSystem` 实现 `ISceneTransitionUniTask`，不拥有场景状态。
- 地基测试使用入口场景和两张纯地图场景验证 A -> B 的真实切换；YooAsset 过滤规则只收集这三张测试场景。
- 删除旧 FantasyWord 主菜单链中的 `M2DEngine` / `Main Menu` 固定场景字段与直接加载入口；当前没有真实应用级主菜单流程，因此不提前创建 Director 或替代场景跳转。
- EX-GAS 遵守插件现有进程级 World 契约：`GameManager` 关闭时只 `Stop()` 并清空绑定，不重复初始化生成缓存和标签图。完整 World 销毁仍是插件缺少正式 Shutdown API 的已登记缺口，不在 GamePlay 侧用反射伪造。

#### 验收证据

- `GamePlay.Tests.ContentLoadingPlayModeTests` `2/2` 通过：内容索引加载 `1/1`，SceneKit A -> B 地图加载 / 活动场景切换 / 旧场景卸载与事件顺序 `1/1`。
- 同一 PlayMode 测试进程连续创建两次 `GameManager` 后，第二次启动不再重复创建 `SingletonGameplayTagMap`。
- GameCore / GamePlay 正式运行时代码中没有 `SceneManager.LoadScene*` / `SceneManager.UnloadSceneAsync`，也没有固定 `Title` / `Main` / `Island` / `M2DEngine` / `Main Menu` 场景名。

#### 下一小步

2.8 对第二模块做整体验收：运行定向与全量测试、规范校验和残留扫描；只保留有证据的既有失败与第三方缺口，不把它们冒充为第二模块已解决。

### 2.8 第二模块整体验收（2026-08-04）

#### 已通过

- 地基 PlayMode：`ContentLoadingPlayModeTests` `2/2` 通过；最终 batchmode 退出码为 `0`，日志见 `Logs/scene-refactor-final-play.log`。
- 生命周期定向 EditMode：`GameManagerAndGameStateLifecycleEditModeTests` `8/8` 通过，结果包含在本轮全量 EditMode 证据中。
- 全量 EditMode：共 `305` 项，`304` 通过、`1` 按条件跳过、`0` 失败，证据为 `Temp/codex-gameplay-module2-scene-refactor-editmode-results.xml`。
- `node .spec/tools/spec-lint.mjs` 通过。
- 正式 GameCore / GamePlay 源码残留扫描为空：StackCraft 命名空间依赖、固定模板场景名、`Resources.LoadAll`、直接 `SceneManager` 加载 / 卸载、并行 RuntimeContext / Bootstrap / 注册表、一对一请求事件和已删除的本地内容类型均未命中。

#### 未解决但不属于本轮回归

- 原先要求 `Character` Layer 的测试已删除：它强制的是 FantasyWord 角色物理约定，CardLoop 当前没有该正式 Layer、Prefab 或碰撞矩阵契约，不能靠添加空 Layer 让旧测试变绿。
- 该阶段退出日志曾提示 EX-GAS 标签图 Persistent allocation；后续已在插件正式生命周期补齐 `GASManager.Shutdown()`，释放标签图、绑定 ASC 属性数组、PlayerLoop 和 World。修复证据见 `gamecore-gas.md`，不再把原 68 笔泄漏列为当前既有缺口。

#### 第二模块收口结论

- StackCraft `GameDirector` 的可吸收部分已经落实为启动依赖、失败逆序回收、直接系统协作、EventKit 事实广播和 YooAsset 场景生命周期；固定场景、单例 Manager 链、请求事件包装和旧业务流程均未进入正式地基。
- 当前仍没有新局、读档、剧本日结、牌桌实例、任务、配方、职业、战斗、联机或 Mod 业务 Director。等第三模块出现真实牌桌运行时消费者后，再继续一小步一小步吸收对应 StackCraft 模块。

## StackCraft 真实模块拆分

StackCraft 是一个完整小型 Stacklands 式成品模板，不是通用 Mod 框架。按 GamePlay 正确引入顺序重新拆分如下：

| StackCraft 模块 | 主要文件 | 职责内核 | 关键局限 |
|-----------------|----------|----------|----------|
| 内容定义与加载 | `CardDefinition`、各类 Definition SO、`Resources.LoadAll`、`PackDefinition` | 用 ScriptableObject 表达卡牌、卡包、配方、任务、遭遇等内容，启动时扫描资源。 | 大一统卡牌字段过重；固定 `Resources` 目录不适合 YooAsset、Mod 包、版本依赖和覆盖顺序。 |
| 标签 / 分类 / 状态 / 技能雏形 | `CardCategory`、`CardFaction`、`CombatType`、`EquipmentSlot`、`QuestType`、`StatModifier`、`classChangeResult` | 用枚举和少量字段区分卡牌类别、阵营、战斗类型、装备槽、任务类型和职业变化。 | 不是可扩展 Tag 系统；新增剧本/Mod 会不断改枚举和管理器分支。 |
| 牌桌交互与卡牌表现 | `Board`、`CardController`、`CardInstance`、`CardStack`、`CardManager`、`CardPhysicsSolver`、`StackingRulesMatrix` | 卡牌拖拽、拆堆、合堆、边界夹取、重叠解算、目标高亮、卡牌表面显示。 | `CardInstance` 同时拥有表现、生命、食物、装备、战斗和存档状态；堆叠规则依赖枚举矩阵。 |
| 行动 / 配方 / 桌面行动进度 | `RecipeDefinition`、`CraftingManager`、`CraftingTask`、`ExplorationRecipe`、`ResearchRecipe`、`TravelRecipe`、`GrowthRecipe` | 堆叠满足配方后创建带进度的桌面行动，支持消耗模式、持续任务、随机权重、研究蓝图、探索产出和旅行。 | 配方按具体 `CardDefinition` 匹配；`RecipeDefinition.Execute` 直接改世界；规则解释和作者校验能力不足。 |
| 剧本目标 / 事件 / 日结节奏 | `Quest`、`QuestManager`、`EncounterDefinition`、`EncounterManager`、`DayCycleManager`、`TimeManager`、`GameDirector` | 监听卡牌、配方、交易、时间等事件推进目标；按日期触发遭遇；日结串联喂食、卖超额卡、遭遇、新一天。 | 目标类型、日结阶段和事件结果都偏硬编码；固定场景名和固定流程不适合多世界剧本。 |
| 战斗 / 冲突区 | `CombatManager`、`CombatTask`、`CombatRect`、`CardCombatant`、`CombatStats`、`HitResult` | 把敌我卡牌拖入战斗矩形，按攻速、命中、暴击和三系克制持续结算。 | 战斗 Manager、卡牌自持 `CombatStats` 和枚举克制结构不吸收；攻防、攻速、命中、闪避、暴击这些模板数值可临时映射到 GNS/EX-GAS 参数以复现效果。 |
| 存档 / 运行时状态 | `GameData`、`SceneData`、`StackData`、`CardData`、`SaveSystem` | 保存卡堆、卡牌动态数值、装备、任务、战斗、商店、遭遇和时间。 | 直接写 `persistentDataPath` JSON 并全目录扫档；缺 Mod 依赖、内容版本、局内/局外分层和迁移策略。 |
| UI / 作者工具 / 原型素材 | `InfoPanel`、`ProgressUI`、`QuestsView`、`RecipesView`、各 Definition Editor、`RecipeDefinitionEditor`、`StackingRulesMatrixEditor`、Prefab/Resources/URP 设置 | 提供完整反馈闭环、进度条、任务列表、卡牌详情、配方冲突提示、矩阵编辑器和可用素材。 | UI 文案偏模板教程；编辑器只服务 StackCraft 数据结构；URP/设置不应接管 GamePlay 全局配置。 |

## 与 GamePlay 职责归属的逐块裁决

### 1. 内容定义 / 加载 / 作者源校验

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `CardDefinition`、`RecipeDefinition`、`Quest`、`EncounterDefinition`、`PackDefinition` 都是 ScriptableObject；`CardManager` 和 `CraftingManager` 用 `Resources.LoadAll` 构建数据库。 |
| GamePlay 职责归属 | GamePlay 自己的数据定义模块；ScriptableObject 是正式作者源之一，YooAsset 只负责资源加载，Luban / 表格 / JSON 只有在证明更适合对应作者入口时才接管。 |
| 裁决 | **第一个重构模块；不吸收 StackCraft 实现，只参考字段范围和成品闭环；现有 GameCore 数据库若与唯一 ID 或 SO 作者源冲突，必须重构。** |
| 必做重构 | 建立 GamePlay 自己的内容共用元信息、卡牌/可交互对象作者源和派生索引；技能、职业、局外带出、NPC 意图与行动条件等引用等真实模块开始时再设计。Unity GUID、YooAsset 地址、文件路径和运行时实例号不得成为第二套内容 ID。 |
| 保留范围 | 可参考它“一个完整小模板需要哪些字段”：显示名、描述、卡面、产出、消耗模式、任务目标、遭遇日期、动态状态保存范围。 |
| 排除范围 | 不吸收 `Resources.LoadAll`、不吸收大一统 `CardDefinition`、不把 StackCraft 的卡牌 ID / 世界观 / 资源目录作为 GamePlay 正式内容事实。 |

### 1.x StackCraft 枚举分类层（排除为正式职责）

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | 主要是 `CardCategory`、`CardFaction`、`CombatType`、`EquipmentSlot`、`QuestType`、`RecipeCategory`、`StackingRule` 等枚举，以及装备上的 `classChangeResult`。 |
| GamePlay 职责归属 | EX-GAS 的 GameplayTag、Ability、GameplayEffect、Attribute、Cue、TargetCatcher、XParam 与 Luban 配置；内容侧使用可扩展 tag / symbol query，而不是 StackCraft 枚举。 |
| 裁决 | **排除 / 已覆盖。** StackCraft 这块不要吸收为正式架构，只作为理解旧逻辑入口。 |
| 为什么排除 | StackCraft 的“tag-like 能力”其实是枚举分支；GAS 已有标签条件、GrantedTags、Ability 激活/阻断、GameplayEffect 应用/持续/移除规则，覆盖面更强，也更适合未来技能树、职业状态、Buff/Debuff、世界效果和 Mod 扩展。这里不是现在实现职业技能，而是防止 StackCraft 枚举体系抢正式职责。 |
| 后续要求 | 第 1 模块就要把 GAS 标签引用 / 查询的命名和内容引用规则定下来，避免后续牌桌、配方、目标或 UI 继续依赖 `CardCategory`，也避免继续保留独立 `Symbols` 规则体系。 |

### 2. 启动流程 / 系统协作 / 单局状态边界

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `GameDirector` 负责新局、读档、保存、切场景和旅行卡牌搬运；`CardManager`、`CraftingManager`、`QuestManager`、`TimeManager`、`DayCycleManager` 通过单例、事件订阅、`Awake` / `Start` 和保存前回调串起局内流程。 |
| GamePlay 职责归属 | `GameCore.GameManager` 负责进程级启动，`AGameSystem` 负责系统生命周期，`EventKit` 负责事实事件，`SceneKit` 负责场景生命周期，`ResourceSystem` 负责 YooAsset 默认包 / Mod 包资源后端；没有并行 `RuntimeContext` 或 GamePlay 启动壳。 |
| 裁决 | **已完成：吸收流程关系，复用并重构现有正式 owner。** StackCraft 的系统协作关系已用于校正启动依赖、失败回收、事件和场景生命周期；单例链、固定场景名、直接互改状态和旧资源扫描已排除。 |
| 保留范围 | 新局/读档/保存前汇总、场景切换前暂停、场景数据 ready 事件、目标监听事件、日结阶段锁输入、时间推进事件和任务完成事件这些流程关系。 |
| 重构方向 | `GameCore.GameManager` 已缩窄为进程级基础设施入口，不接管新局、读档、牌桌或剧本状态；场景生命周期归 `SceneKit`，单局 Director 只有在真实新局/读档流程出现后才允许建立。 |
| 排除范围 | 不复制 `GameDirector` / `CardManager` / `CraftingManager` / `QuestManager` 等名称作为正式职责；不保留 `Resources.LoadAll`；不保留 `SceneManager.LoadSceneAsync("Main")` 这类固定场景；不在第二模块实现牌桌、配方、目标、日结、存档或原创玩法。 |
| 验收方式 | 已通过职责裁决、PlayMode、EditMode、残留扫描和 `.spec` lint，证明正式 GamePlay 运行时代码不依赖 `CryingSnow.StackCraft`，且没有与 `GameCore.GameManager` 并行的第二套启动 / 生命周期 / 系统注册职责。 |

### 3. 可堆叠卡牌运行时 / 卡牌表现

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `CardController` 的拖拽/拆堆/投放流程，`CardStack` 的队列式堆叠，`Board` 的牌桌边界，`CardPhysicsSolver` 的重叠解算，卡牌高亮和拖拽阻尼手感。 |
| GamePlay 职责归属 | GamePlay 自己的可堆叠卡牌状态与 `Card View`；输入只接 Unity 新 Input System 的玩家意图；表现层不拥有领域真相。固定工位、圆形节点和连通节点不归本模块强行统一。 |
| 裁决 | **保留吸收，重构实现。** 这是 StackCraft 最值得吸收的手感模块，但必须排在内容契约之后。 |
| 保留范围 | 拆堆/合堆手感、最近目标吸附、拖拽尾牌阻尼、边界夹取、桌面重叠解算和拖拽高亮；进度条贴世界坐标只作为后续行动/UI 模块参考。 |
| 必做重构 | `CardInstance` 拆成视图和运行时状态投影；`CardManager` 整体不吸收，内容、牌桌状态、视图生成和空间解算分别回到正式 owner；投放只提交空间意图，不直接决定配方、交易、装备或战斗。 |
| 排除范围 | 不吸收 `CardInstance` 的多职责状态；不吸收 `public static Instance` 串联系统；不吸收按 `CardCategory` 决定所有交互的矩阵。 |

### 3.1 StackCraft 牌桌旧实现替换清单（2026-08-05）

本小步只拆解参考源码并锁定正式职责，不新增牌桌代码、运行时实例 ID、拖拽事件、交互接口或空壳服务。下表中的“后续职责”是边界裁决，不等于要求一个职责对应创建一个新类。

#### 逐文件裁决

| 参考文件 | 真实职责与问题 | 吸收内容 | 删除、迁移或延后 |
|----------|----------------|----------|------------------|
| `CardInstance` | 一个 `MonoBehaviour` 同时保存内容定义、堆栈引用、生命、营养、使用次数、装备、战斗、制作、存档恢复、悬浮信息、材质、Tween 和销毁副作用。视图既是规则实体又是存档实体。 | 卡面初始化、悬浮命中、显示刷新、移动动画、拖拽尾牌的指数阻尼和受击表现只作为视图手感参考。 | 生命、营养、装备、战斗、制作、掉落、发现、存档和内容变形全部离开牌桌视图；视图后续只投影运行时状态，不拥有规则真相，也不直接调用任何 Manager。 |
| `CardStack` | 用 `List<CardInstance>` 同时表示堆栈成员、顺序、空间布局和制作锁定，并在增删时直接注册 Manager、停止制作、销毁 GameObject、驱动 Tween。`TopCard = Cards[0]`、`BottomCard = Cards[^1]` 与追加、拆分注释存在方向歧义。 | 有序成员、从指定成员处分堆、合堆、按固定步长布局、拖拽首牌即时跟随与尾牌阻尼跟随。 | 正式堆栈模型不得保存视图组件引用、制作状态或销毁副作用；必须先定义唯一的顺序语义和拆分不变量，再由视图布局消费结果。`RefuseAll` 哨兵对象改为显式的“禁止自动吸附”请求参数，不把特殊值伪装成真实堆栈。 |
| `CardController` | 把输入、拖拽、点击判定、拆堆、吸附、交易、装备、战斗、制作暂停/恢复、音效和高亮写在一次投放流程里。 | 按下时记录起点、拖动平面投影、点击距离阈值、拆堆后拖动尾段、结束时寻找最近候选、进入和退出目标高亮。 | 正式输入仍归 `GameCore.InputSystem`；不保留 `StackCraftInput` 或第二套 Input Manager。投放只能形成玩家意图和空间关系，不能直接交易、装备、开战或触发配方。真实投放合同出现时直接使用 `EventKit` 或正式命令入口，不预建空事件包装。 |
| `Board` | 把牌桌几何边界、顶部禁放区、SkinnedMesh 变形、全局卡牌统计、UI RectTransform 夹取和 `Board.Instance` 混在一起。 | 根据对象完整占地夹取位置、可玩区域与禁放区域分离、边界变化后重新校正对象。 | 牌桌几何来源、放置规则和视觉网格必须分开；不订阅卡牌统计，不负责扩大牌桌的业务规则，不处理 UI 控件位置，不使用全局单例。未来多地区、多牌桌或节点地图不能被单一世界 `Bounds` 写死。 |
| `CardPhysicsSolver` | 用迭代 AABB 最小位移解算堆栈与堆栈、堆栈与 `CombatRect` 重叠，但直接依赖 `CardStack`、`CombatRect` 和 `Board.Instance`。两个中心完全重合时 `Mathf.Sign(0)` 会返回零位移，却仍报告已处理重叠。 | AABB 占地、选择较小穿透轴、锁定对象只推动另一侧、双方可动时各分一半、有限迭代收敛。 | 改成纯几何算法：输入占地、锁定状态、限制区和稳定顺序，输出位移结果；不驱动 Tween、不调用 Board、不认识战斗。必须补同中心稳定方向、epsilon、未收敛结果和确定性排序，联机时只由权威牌桌状态提交最终位置，客户端只做预览。 |
| `CardManager` | 同时承担内容扫描、定义索引、Prefab 选择、实例工厂、堆栈注册、重叠解算、发现记录、存档、旅行恢复、装备恢复、营养统计、进食日结和事件源，是典型上帝类。 | 活跃牌桌对象需要统一登记、创建/移除反馈、视图工厂和解算调度这一事实可以保留。 | 整个类不作为单元吸收。内容索引随 `ScenarioRun` 存续；资源与 Prefab 加载归 `ResourceSystem`；牌桌状态、视图生成、空间解算分别按真实职责建立；发现、存档、日结、装备和战斗延后到各自模块。不得保留 `public static Instance`。 |
| `StackingRulesMatrix` | 用 `CardCategory x CardCategory` 枚举矩阵决定 `None / CategoryWide / SameDefinition`，新增 Mod 类型必须改代码和矩阵尺寸。 | 只保留“拖拽时需要查询候选是否可接收，并据此高亮”的交互体验。 | 矩阵和 `StackingRule` 枚举不进入正式架构。牌桌不自行解释 GAS 标签或配方；后续行动/交互模块返回可接受关系，牌桌只展示候选并提交投放意图。 |
| `Highlight` / `ProgressUI` | 高亮临时创建子物体和材质实例；进度条直接读取 `CraftingTask` 并跟随堆栈世界位置。 | 可接受目标的即时高亮、世界锚点进度反馈。 | 高亮归卡牌视图状态，不能每次临时制造规则对象；进度条属于第四模块行动进度与第八模块 UI 绑定，本模块只保留世界锚点参考，不引用 `CraftingTask`。 |

#### 与当前项目职责对比

| 职责 | 当前正式 owner 或候选 | 3.1 裁决 |
|------|-----------------------|----------|
| 静态卡牌内容 | 当前实现为 `CardDefinition`、`ScenarioRun.ContentIndex` | 唯一内容身份、索引和正式加载入口继续复用；卡牌专用 `CardArt` / `Artwork` 已收口到 `CardDefinition`，其它内容不再被迫提供卡面。 |
| 资源和 Prefab 加载 | `GameCore.ResourceSystem`、`SoftAssetReference` | 直接复用正式加载入口。卡牌视图 Prefab 的作者字段在视图工厂真正实施时裁决，不在 3.1 提前加字段。 |
| 原始玩家输入 | `GameCore.InputSystem`、Unity 新 Input System | 继续作为唯一输入 owner。当前 `Click` 仍带 FantasyWord 点击移动语义，后续牌桌输入接入必须重构正式 owner，而不是增加 `StackCraftInput`。 |
| 领域事实事件 | YokiFrame `EventKit` | 直接使用。没有真实订阅者和结果语义前不新增“卡牌已投放”空事件。 |
| 旧世界交互 | GameCore `IInteraction` / `IInteractionTarget` | 目前绑定 `CharacterBase` 和旧世界命令执行，不足以接管卡牌对地点、技能对卡牌或填槽交互；保留为候选对比，第四模块再决定重构、迁移或删除，不做桥接。 |
| 牌桌卡牌运行时对象 | `Tabletop -> TabletopCards -> TabletopCardStack -> TabletopCard` | 已建立局内卡牌、底到顶堆栈顺序、牌桌位置和放置锁定的唯一对象关系；控制权与玩家身份等正式命令消费者出现后再扩展，不提前加空字段。 |
| 卡牌视图与空间解算 | `TabletopCardLayout`、`TabletopCardView`、`TabletopView`、`TabletopCardStackPlacementSolver` | 3.2-3.4 已收口：整堆空间解算由 `Tabletop.TryPlaceStack` 原子提交；`TabletopView` 只投影并通过 `ResourceSystem` 管理表现资源。 |

#### 必须保住的算法与手感

1. 拖起堆栈中间卡牌时，从该卡开始带走连续尾段，剩余堆栈立即重新排布。
2. 被拖动的首牌紧跟指针，其余牌按固定间距和指数阻尼追随，松手后回到稳定布局。
3. 投放候选按完整堆栈占地和最近有效目标选择，不因一个堆栈有多个 Collider 被重复计算。
4. 牌桌边界按整个堆栈占地夹取，不只检查指针或首牌中心。
5. 重叠解算使用有限迭代和最小穿透轴；锁定对象不移动，双方可动时分摊位移。
6. 可接受、拒绝和当前选中目标必须有稳定高亮反馈，但高亮不参与规则判断。

#### 必须修正的实现缺陷

- 堆栈顺序必须有唯一术语和不变量，禁止继续保留 `TopCard`、`BottomCard`、追加顺序和位移方向互相矛盾的实现。
- 空间算法必须脱离 MonoBehaviour、Manager、战斗区和 Tween，才能做 EditMode 单测、联机权威结算和回放。
- 最近目标搜索必须分成“空间候选”和“规则可接受性”两步；物理距离不能替代行动、配方、装备或战斗规则。
- 拖拽预览和最终提交必须分开；客户端可以预览位置，但只有正式牌桌状态 owner 能确认拆堆、合堆和位置变化。
- 本小步不建立临时适配层。`Assets/StackCraft/` 继续留在参考区；等正式牌桌达到同等手感并通过测试场景验收后，再单独裁决参考脚本是否保留。

#### 当前编号订正

本段是 2026-08-05 的历史拆分记录；其“3.2 运行时状态”已经并入当前计划的 **3.1 卡牌实例与牌堆**。2026-08-11 对象关系重审后，下一小步按 `task_plan.md` 执行当前 **3.2 桌面区域、位置和放置**，不再沿用旧编号继续新增切片。

### 3.2 牌桌运行时状态与堆栈模型（2026-08-05）

#### 当前项目对比与正式裁决

| 候选来源 | 当前能力 | 3.2 裁决 |
|----------|----------|----------|
| StackCraft `CardInstance` / `CardStack` | 直接用 `MonoBehaviour` 引用表示成员关系，没有独立局内卡牌 ID；堆栈同时调用 Manager、制作、销毁和 Tween。 | 只吸收有序堆栈、拆分与合并行为；不吸收视图引用、单例、副作用和隐含顺序。 |
| GameCore `Persistable` / `Entity` | 用场景对象、Prefab、持久化标识和旧 `CharacterBase` 交互支撑 FantasyWord 世界实体。 | 不复用为牌桌对象基类。它绑定 MonoBehaviour、场景恢复和旧交互职责，无法作为纯牌桌状态；也不新增包装或桥接。 |
| `ContentId` | 作者维护的唯一静态内容身份。 | 继续作为卡牌引用的内容定义 ID；同一内容可以创建多张不同局内卡牌。 |
| GamePlay 3.2 新增状态 | 此前不存在。 | `TabletopCardId`、`TabletopCard`、`TabletopCardStack`、`TabletopCards` 已完成卡牌专用命名订正，共同承担可堆叠卡牌实例、成员关系和位置真相；不再声称覆盖所有牌桌形态。 |

#### 实现入口

| 类型 | 唯一职责 | 关键边界 |
|------|----------|----------|
| `TabletopCardId` | 当前标识一局可堆叠卡牌状态中的一张卡牌。 | 由当前状态从 `1` 开始自动分配，`0` 无效；作者不填写，不替代 `ContentId`，不复用 Unity InstanceID 或随机 GUID。是否成为未来其它桌面形态的共享实例 ID，要等非卡牌消费者出现后裁决。 |
| `TabletopCard` | 保存当前局内卡牌 ID、静态内容 ID、剩余使用次数和所属牌堆；通过牌堆回答逻辑位置与锁定状态。 | 使用次数是卡牌实例自身状态，最后一次使用由所属 `Tabletop` 直接移除，不建立耐久管理器；不重复保存牌堆位置，不保存装备、行动、战斗、存档块或视图引用。 |
| `TabletopCardStack` | 保存一个卡牌堆栈的底到顶顺序、牌桌二维位置、位置锁定状态，并唯一维护成员归属。 | 顺序唯一规定为索引 `0` 是底部、最后一个是顶部；成员列表对外只读，位置没有公开 setter；构造、合堆、拆堆和移除同步更新卡牌对象的所属牌堆。 |
| `TabletopCards` | `Tabletop` 直接拥有的卡牌 / 牌堆集合与局内 ID 索引。 | 不再维护“卡牌属于哪个堆”的派生字典；纯 C#、非单例、不继承 `AGameSystem`。写方法为程序集内部，正式调用继续由 `Tabletop` 对外收口。 |

#### 已锁定行为

1. 同一个 `ContentId` 可以创建多张局内卡牌，每张卡牌得到不同的 `TabletopCardId`。
2. 新卡牌先形成一个独立单卡堆栈；位置属于堆栈，不在每张卡牌上重复维护。
3. 合堆时目标成员和顺序保持在下方，来源堆按原顺序整体追加到上方，最终位置使用目标堆位置。
4. 从某张卡牌拆堆时，该卡牌及其上方卡牌形成新堆；下方卡牌留在原堆，两个堆继承拆分前的位置。
5. 从底部卡牌开始拆分等价于移动整个现有堆栈，不制造第二个空堆或临时哨兵卡牌。
6. 位置锁定只禁止整个卡牌堆栈移动或作为合堆来源；锁定底牌上方的卡牌仍可拆成新的未锁定堆栈。它只表达固定卡堆，不再被解释成固定工位或固定地点模型。
7. 合并、拆分和移除由 `TabletopCardStack` 更新卡牌对象的所属牌堆；`TabletopCards` 不保存第二份成员关系表，调用方和视图也不能维护副本。

#### 联机、Mod 与存档边界

- `Tabletop` 是牌桌状态的正式提交边界，`TabletopCards` 是其内部拥有的卡牌集合，不等于服务器、玩家席位或网络传输。未来命令层先校验玩家控制权，再调用牌桌提交变化；客户端拖拽预览不能直接写入正式状态。
- `TabletopCardId` 当前只保证单个 `TabletopCards` 作用域内唯一。存档恢复、网络快照和事件回放出现后，扩展的是权威状态的创建/恢复入口，不新增第二种局内卡牌 ID。
- Mod 作者继续只维护 `ContentId` 和内容资产；局内卡牌 ID 由运行时分配，避免 Mod 内容和每局实例双重登记。
- 本小步不建立存档 DTO、网络命令、玩家 ID、随机源、可见性字段或 Mod 状态容器；没有消费者前不冻结这些合同。

#### 验收证据

- 2026-08-11 `TabletopCardsEditModeTests` `10/10` 通过，覆盖同内容多实例、卡牌对象所属牌堆与位置、合堆、拆堆、移除、锁定和快照恢复。
- 同轮全量 EditMode `421/422`（`1` 条既有忽略）、全量 PlayMode `30/30`，覆盖拖拽、行动结果、战斗阵型和视图投影消费者。
- `GamePlay.Runtime` 和 `GamePlay.EditModeTests` 使用 Unity `6000.5.4f1` 当前 Bee/Roslyn 响应文件编译通过。
- 全量 EditMode 共 `310` 项：`309` 通过、`1` 条既有条件跳过、`0` 失败，证据为 `TestResults-Module32-Final.xml`；跳过项是 UnitySkills 对缺失可选包的既有条件忽略，与 3.2 无关。
- 正式实现不引用 `CryingSnow.StackCraft`、`Persistable`、`Entity`、`EventKit`、`GameManager`、`MonoBehaviour`、`GameObject` 或 Tween。

#### 下一小步

按当前计划进入 3.2，只处理牌桌二维空间、可玩边界、禁放区域和原子放置解算：吸收 `Board` / `CardPhysicsSolver` 的几何职责并回审同中心位移、确定性顺序、未收敛结果和位置提交。它仍不提前重构拖拽、视图、行动或配方。

### 3.3 牌桌二维空间、边界与重叠解算（2026-08-05）

#### 吸收与重构结果

| StackCraft 参考 | 吸收内容 | 正式实现差异 |
|-----------------|----------|--------------|
| `Board.ClampToBounds` | 按完整卡牌占地夹取，而不是只检查中心点。 | `TabletopCardPlacementArea` 使用纯二维 `Rect`，不依赖 SkinnedMesh、Transform、CardManager 或顶部固定边距。 |
| `Board` 顶部禁放区 | 可玩区域内存在不可放置区域。 | 禁放区改为任意数量矩形，供 HUD 保留区、固定工位、冲突区或地图节点布局使用。 |
| `CardPhysicsSolver` AABB 最小穿透轴 | 选择较小穿透轴、锁定对象只推动另一侧、双方可动时平均分摊。 | `TabletopCardStackPlacementSolver` 只处理整堆值快照，输出不可变结果，不调用 Board、Tween、战斗区或牌桌状态；最终提交归 `Tabletop`。 |
| StackCraft 有限迭代 | 避免对象链式推动时无限循环。 | 结果显式返回 `Converged` 和迭代次数；锁定冲突或次数耗尽不会被误报为完成。 |

#### 正式合同

- `TabletopCardPlacementArea` 是牌桌二维边界和禁放矩形的不可变配置，拒绝非有限坐标和无效宽高。
- `TabletopCardStackGeometry` 是公开几何入口，作者只配置卡牌尺寸和堆叠步进；编辑器、预览和测试可用它计算整堆占地矩形。
- `TabletopCardStackSpatialBody` 和 `TabletopCardStackPlacementSolver` 是牌桌内部解算细节：使用底牌局内 ID 定位整堆，先稳定排序，再处理边界、禁放区和堆间重叠；它们不作为业务 / Mod 公开 API。
- 两张卡牌中心完全重合时，低 ID 稳定向负 X、高 ID 向正 X，修复 StackCraft `Mathf.Sign(0)` 返回零位移但仍报告已处理的问题。
- 锁定卡牌只作为权威阻挡，不为了视觉整齐被解算器偷偷移动；两张锁定卡牌重叠时返回未收敛。
- 解算结果不会直接暴露给客户端预览或 UI。只有 `Tabletop.TryPlaceStack` 在规则收敛后才能一次性提交拆堆和位置变化；客户端预览不能越过这一边界。

#### 注释与维护边界

- 所有公开类型和公开方法已补中文 XML 注释，说明输入限制、失败方式、返回语义和副作用。
- 同中心稳定方向、锁定卡牌不移动、最终收敛复核和分离向量语义均在对应复杂分支旁说明原因；简单循环和赋值不写流水账注释。

#### 验收证据

- `TabletopCardStackPlacementSolverEditModeTests` 当前 `6/6` 通过：整堆完整占地、同中心稳定分离、锁定/可动对象分离、双锁定未收敛、禁放区推出和多成员占地；统一场景另有真实边界与重叠 PlayMode 验收。
- 正式空间代码不引用 StackCraft、MonoBehaviour、GameObject、Transform、Physics、Collider、CombatRect 或 Tween。

#### 下一小步

3.4 只建立卡牌视图投影、堆栈布局和视图创建职责：视图读取 `TabletopCards` 与内容索引，展示卡面和堆栈位置，但不拥有运行时状态，也不处理输入、投放规则或行动结算。

### 3.4 卡牌视图投影、堆栈布局与视图创建（2026-08-05）

#### StackCraft 吸收与排除

| 参考来源 | 吸收内容 | 正式实现差异 |
|----------|----------|--------------|
| `CardSettings.StackStep` | 同一堆栈按固定三维步进展示顶部成员。 | 收敛为 `TabletopCardLayoutParameters.StackVisualStep`；允许按相机方向配置正负深度，只影响表现，不回写牌桌位置。输入阈值、经济、AI、粒子和战斗设置不进入本配置。 |
| `CardInstance.Initialize` | 绑定内容显示名、卡面资源和视图对象。 | `TabletopCardView` 只保存局内卡牌 ID 与内容 ID，拒绝不一致绑定；不保存生命、营养、装备、制作、战斗、堆栈或存档状态。 |
| StackCraft 卡面 Mesh / `_OverlayTex` 用法 | 允许临时原型继续使用 Renderer 材质属性显示卡面纹理。 | 正式视图使用可配置 Shader 属性名和 `MaterialPropertyBlock`，不复制材质实例，也不把 StackCraft Shader 属性写死为 GamePlay 默认。纯 Sprite 预制体可直接使用 `SpriteRenderer`。 |
| `CardManager` 类别到 Prefab 映射与 `Instantiate` | 牌桌需要统一创建、同步和回收视图。 | 不吸收类别枚举、Prefab 字典或上帝类。`TabletopView` 只按局内卡牌创建一个通用视图预制体；差异化卡牌表现等真实需求出现后再建立明确扩展点。 |

#### 正式职责

- 卡牌投影现在只接受 `CardDefinition`，并使用其卡牌专用 `Artwork`；独立卡面未配置时回退到通用 `Icon`。通用 `ContentAsset` 不再提供牌桌首选图片，非卡牌内容也不会被投影器静默当成卡牌。
- 当前 `TabletopViewSettings` 是牌桌表现统一作者源，保存现有 `SoftAssetReference<GameObject>` 预制体地址、Z 深度、排序和拖拽手感；权威卡牌尺寸与 XY 步进已经归剧本牌桌放置规则。它不是内容目录、Prefab 注册表或第二资源加载器。
- `TabletopCardLayout` 是纯计算入口，把 `TabletopCardStack` 的二维位置和“底部索引 0”顺序转换为局部三维位置与渲染顺序。
- `TabletopCardView` 是 Unity 表现组件，只接受身份一致的 `TabletopCard` 与 `CardDefinition`，只修改自身 Transform、SpriteRenderer 或 Renderer 属性块。
- `TabletopView` 只读 `TabletopCards.Stacks` 与 `ContentIndex`；内容 ID 若指向非卡牌作者源会显式失败。视图实例统一通过 `ResourceSystem.InstantiateAsync` 创建，实例和图片句柄由牌桌视图持有并通过 `ResourceSystem` 释放。
- `TabletopCards.Stacks` 是实时只读视图；视图和输入层不能维护第二份堆栈成员关系，也不能通过 Transform 反向修改正式位置。

#### Mod、联机与资源边界

- 预制体作者字段继续使用 `SoftAssetReference<GameObject>`，运行时地址解析和 Mod 包优先级继续归 `ResourceSystem`；GamePlay 没有新增 `GamePlayAssetReference`、视图包加载器或第二套 YooAsset 入口。
- 图片句柄按内容 ID 在投影器内复用，避免每张同内容卡牌重复加载，也避免 ScriptableObject 作者资产承担场景视图生命周期。
- 视图只消费权威状态结果。未来联机客户端可以重建同一投影，但不能把本地 Transform 当成网络状态；拖拽预览和最终提交仍留给 3.5。
- 本小步只建立通用卡牌视图合同。Mod 自定义视图、内容差异化 Prefab、对象池和动画策略必须等真实消费者出现后审查，不能先用类别枚举或空接口冻结扩展方式。

#### 注释与验收证据

- 新增公开类型、公开方法和 Inspector 字段均有中文职责/契约注释；资源句柄为何归投影器持有、材质为何使用属性块等非显然选择已就近说明。
- `GamePlay.Tests` 定向 EditMode 共 `14/14` 通过，其中 3.4 新增 `4/4`：堆栈底到顶布局、越界索引拒绝、视图身份绑定和错误内容身份拒绝。证据为 `TestResults-Module34-Final.xml`。
- 正式 3.4 代码不引用 `CryingSnow.StackCraft`、`CardManager`、`Resources.Load`、`UnityEngine.Input`、`SceneManager`、第二事件层或第二资源引用类型。
- 当前尚未在真实 YooAsset 构建清单中实例化 GamePlay 卡牌视图 Prefab，也未验证 Sprite/Renderer 卡面和场景回收；这些属于 3.6 统一测试场景的明确验收缺口，不能以 EditMode 通过代替。

#### 下一小步

3.5 只重构正式输入与拖拽：复用 `GameCore.InputSystem` 和 Unity 新 Input System，吸收按下阈值、拆堆预览、指针平面投影、尾牌阻尼、目标高亮和投放空间意图；不新增第二 Input Manager，不执行配方、装备、交易或战斗。

### 3.5 正式输入与拖拽空间意图（2026-08-05）

#### 输入作者源订正

- 正式输入 owner 仍是 `GameCore.InputSystem` 与 `Assets/InputSystem_Actions.inputactions`，GamePlay 没有新增 Input Manager、输入动作资产或设备轮询入口。
- 原 CardLoop 输入资产仍保留了模板 `Player` 动作图和 `Attack`、`Jump`、`Crouch` 等 FantasyWord 业务动作，但 `GameCore.InputSystem` 的正式合同已经要求 `Gameplay`、`UI`、`None`。本小步从 FantasyWord 的同职责作者源吸收 `Gameplay` / `None`，同时保留 CardLoop 当前更完整的 `UI` 动作图。
- `Gameplay` 动作图补齐鼠标、触屏的 `Point` / `Click` 绑定；102 个动作与绑定 ID 均唯一，避免 Unity Input System 在合并作者源后出现重复身份。
- 删除 `GameCore.InputSystem` 内“左键自动执行点击移动”的旧业务假设。`Click` / `Point` 现在只提供通用主指针输入，具体牌桌行为由 GamePlay 消费者解释。
- `IsGameplayActionBlocked` 复用现有动作图释放门禁；切换 `Gameplay` / `UI` / `None` 时，牌桌不会把切换前仍按住的指针误判成新操作。

#### StackCraft 吸收与排除

| 参考来源 | 吸收内容 | 正式实现差异 |
|----------|----------|--------------|
| `CardController` 指针按下、拖动、释放流程 | 按下位置、拖拽距离阈值、相机射线到牌桌平面投影、按下即拿起到 `DragHeight`，释放时形成一次结果。 | `TabletopCardDragSession` 是设备无关状态机；`TabletopCardDragInput` 只监听正式输入 owner，不直接读取 `Mouse.current`、`Pointer.current` 或 `UnityEngine.Input`。 |
| `CardStack` 拖拽尾段与阻尼 | 从选中成员到堆顶形成临时拖拽预览，首张立即跟随，尾牌指数阻尼追赶；拖最上层卡时移动整叠。 | 预览只改 `TabletopCardView` 的 Transform；不拆堆、不改 `TabletopCards`，释放后恢复权威布局；预览锚点使用实际被拖走牌段首张卡。 |
| `CardController` 最近目标与高亮 | 拖拽时排除来源堆，先取射线直接命中的其它卡牌；没有直接命中时，按 `CardSettings.attachRadius = 0.25` 在拖拽牌段首张卡周围选择最近可接受候选并显示高亮。 | `TabletopCardDragInput` 仍只形成释放意图；`TabletopViewSettings.m_attachRadius` 承接半径参数，`TabletopView` 基于当前可见卡面 footprint 查找候选。半径候选必须能形成当前行动候选或普通合堆候选，避免靠近无效卡牌阻止空白放置。 |
| `CardController.DropCard` 的投放结果 | 释放时保留来源卡牌、按下位置、释放位置、是否拖拽和候选卡牌。 | 收敛为 `TabletopCardPointerReleaseIntent`，由显式传入的真实消费者接收；没有消费者时拒绝绑定，不发送空事件，也不直接合堆、交易、装备、开战或执行配方。 |

#### 正式职责与边界

- `TabletopCardDragSession` 只判定一次主指针交互是点击还是拖拽，并产出不可变释放意图；它不依赖 Unity 输入设备、场景对象或事件系统，可供鼠标、触屏、回放和未来联机命令入口复用同一阈值规则。
- `TabletopCardDragInput` 负责输入监听、卡牌视图命中、牌桌坐标投影、拖拽预览和空间候选高亮。它优先使用真实射线命中；射线未命中卡牌时，才使用 StackCraft `attachRadius` 半径在当前可见卡面中查找最近可执行候选。它不能提交正式牌桌变化，也不能解释最终规则结果。
- `TabletopCards.TryGetStackContaining` 只为输入和视图处理“对象可能刚被权威状态移除”的正常竞态；正式状态提交仍只能经过 `TabletopCards` 的明确操作。
- `TabletopCardPointerReleaseIntent.TargetCardId` 是候选卡牌，不是已批准目标。第四模块的行动解析或未来权威命令消费者负责校验控制权、规则条件和最终状态变化；非卡牌目标不会被硬塞进这个字段。
- 所有新增公开类型、公开方法、复杂生命周期和 Inspector 字段均使用中文注释说明职责、失败方式和副作用；不以流水账注释重复代码。

#### 验收证据

- `TestResults-Module35-GamePlay.xml`：GamePlay EditMode `18/18` 通过，其中新增拖拽会话测试覆盖点击阈值、拖拽阈值和来源堆排除后的候选目标。
- `TestResults-Module35-InputAsset.xml`：输入作者源合同 `1/1` 通过，确认正式动作图为 `Gameplay` / `UI` / `None`，且不存在重复动作或绑定 ID。
- 正式输入与牌桌代码不引用 `CryingSnow.StackCraft`、`StackCraftInput`、`UnityEngine.Input`、`Mouse.current`、`Pointer.current`、第二事件层或第二输入资产。2026-08-23 补充静态验收：`gameplay-static-preflight` 已要求 `TabletopViewSettings` 的 `m_attachRadius` 从 StackCraft `Default_Card_Settings.asset` 派生，拖拽输入必须具备“直接命中优先、AttachRadius 有效候选兜底”的方法体结构，距离算法必须读取 `TabletopCardView` 当前可见卡面尺寸；释放阶段必须把直接命中或半径吸附目标写入 `TabletopCardPointerReleaseIntent` 并交给正式交互入口。
- 真实鼠标/触屏交互、YooAsset 卡牌视图实例化、碰撞命中、高亮和释放消费者仍留到 3.6 统一测试场景验收，不能以 EditMode 通过代替。

#### 下一小步

3.6 只收口统一测试场景：在 `FoundationTest` 中装配真实卡牌视图 Prefab、牌桌表现配置、内容索引、正式输入和只记录释放意图的测试消费者，验证完整交互链路；仍不实施行动、配方、装备、交易、战斗或原创剧本规则。

### 3.6 统一测试场景与真实牌桌链路（2026-08-05）

#### 场景与作者资产

- 统一测试场景仍是 `Assets/Scenes/FoundationTest.unity`，没有新增正式启动场景、StackCraft `Title` 入口或第二套测试框架。
- 场景当前装配 `PlayerInput`、`GameCore.InputSystem`、`ScenarioDirector`、`TabletopView`、`TabletopCardDragInput`、正交相机和卡牌 `BoxCollider`；内容索引由测试流程开始单局后从 `ScenarioRun` 取得。
- 新增测试场景视图配置 `Assets/Gameplay/Tests/牌桌/牌桌测试视图设置.asset`，并引用正式牌桌表现 Prefab `Assets/Art/Prefabs/牌桌/卡牌视图.prefab`；卡面使用已迁入项目素材目录的 `Assets/Art/Sprites/卡牌占位图.png`，只作为测试卡面占位，不成为 Gameplay 内容身份或正式美术风格规范。
- 场景、正式牌桌表现 Prefab 和临时图片都由现有 YooAsset Collector 收集；运行时仍通过 `SoftAssetReference` / `ResourceSystem` 创建视图和加载图片，没有新增 GamePlay 资源加载封装。
- `GamePlay/地基/重建测试场景` 会重建固定测试场景和资产、写入 Build Settings 与 YooAsset 测试收集项，并在保存后回读关键引用；它只服务地基验收，不是关卡编辑器或正式剧本入口。

#### StackCraft 手感吸收结果

- `FoundationTestSceneHarness` 创建三张卡组成的来源堆和一张独立候选卡，验证同一内容 ID 可以产生多张独立局内卡牌。
- 正式拖拽从来源堆的中间卡牌开始，首张立即跟随，顶部尾牌按表现配置阻尼追赶；没有在预览阶段拆分 `TabletopCards`。
- 指针移动到独立卡牌时只显示空间候选高亮，释放后形成“来源卡牌、按下位置、释放位置、是否拖拽、候选卡牌”的 `TabletopCardPointerReleaseIntent`。
- 正式释放消费者按目标形态执行分流：命中其它卡牌时先查询行动候选；没有行动候选时通过 `Tabletop.TryDropStackOnto` 普通合堆；空白桌面或无效目标释放时通过 `Tabletop.TryPlaceStack` 原子提交拆堆、边界与堆间重叠结果。输入组件本身仍不直接执行合堆、行动、配方、装备、交易或战斗。

#### 正式职责订正

- `GameCore.InputSystem` 增加初始化状态保护：Unity `Update` 早于 `GameManager` 异步初始化时返回无输入，系统关闭时清理动作引用和初始化状态。这修复的是正式生命周期缺口，不是只在测试中吞异常。
- PlayMode 输入测试直接继承 Unity Input System 官方 `InputTestFixture`，使用官方设备 `Move` / `Press` / `Release` 驱动场景中的 `PlayerInput`；此前诊断用的自制虚拟输入注入已删除。
- `TabletopCardDragInput` 暴露的 `IsPointerSessionActive` / `IsDragging` 只提供只读运行诊断，不成为第二状态 owner，也不允许测试直接推进内部会话。
- 所有新增或审计到的公开、内部类型和方法、Inspector 字段、编辑器入口及关键生命周期已补中文契约注释；注释明确唯一 ID、EX-GAS 标签码、资源句柄、预览与权威状态之间的边界。

#### 验收证据

- `TestResults-Module36-Comments-GamePlay.xml`：GamePlay 定向 EditMode `18/18` 通过。
- `TestResults-Module36-Comments-GameCore.xml`：正式输入作者源与 `GameManager` 生命周期定向 EditMode `9/9` 通过。
- `TestResults-Module36-Comments-AllEditMode.xml`：全量 EditMode `323` 通过、`1` 条既有条件跳过、`0` 失败。
- `TestResults-Module36-Comments-PlayMode.xml`：牌桌相关 PlayMode `2/2` 通过。
- 第一条用例确认 4 个卡牌视图均由 YooAsset 实例化，卡面由内容作者源地址经过 `ResourceSystem` / YooAsset 写入视图。
- 第二条用例确认正式 `PlayerInput` 与 `GameCore.InputSystem` 读取测试鼠标，从中间卡牌开始拖拽、尾牌跟随、候选高亮和释放意图完整成立；释放后仍为两个正式堆栈。
- 运行日志出现“牌桌释放意图：卡牌 2，拖拽 True，候选卡牌 4”，与测试状态和目标卡牌一致。
- 2026-08-05 回审订正后的新鲜证据：正式场景生成器退出码 `0`；最终 `TestResults-Module13-CardRefactor-EditMode-Final.xml` 为 `18/18`，`TestResults-Module13-CardRefactor-PlayMode.xml` 为 `2/2`，`TestResults-Module13-CardRefactor-AllEditMode.xml` 为 `323` 通过、`1` 条既有条件跳过、`0` 失败；Missing Script 与旧 `TabletopObject*` 符号扫描通过，`.spec` lint 通过。

#### 第三模块收口

- 第三模块 3.1-3.6 已完成 StackCraft 可堆叠卡牌能力的拆解、改造吸收和真实运行验收；正式 GamePlay 代码不依赖 `CardManager`、`CardController`、`StackCraftInput`、`Resources.LoadAll`、固定场景名或模板单例链。
- 本模块只建立内容到可堆叠卡牌状态、表现和输入意图的地基。候选是否可接受、释放后执行什么、是否消耗材料或施加效果，仍必须留给后续行动解析与效果结算模块裁决；固定工位、圆形节点和连通节点不由当前堆栈状态代替。
- `Assets/StackCraft/` 继续作为参考源码和未迁入原件区；是否删除参考脚本要在正式能力覆盖与素材依赖清单完成后单独裁决，不能因 3.6 通过直接整目录删除。

## 模块 1-3 回审裁决（2026-08-05）

| 模块 | 是否存在生搬或错误泛化 | 证据 | 最新裁决 |
|------|------------------------|------|----------|
| 1. 内容定义 | **回审发现的问题已完成代码订正。** 此前为了给卡牌投影提供统一图片，把模板的“内容天然有卡面”假设写回了数据根，并把卡牌表现与可交互能力做成平行继承类型。 | `ContentAsset` 已删除通用卡面；`GamePlayInteractableDefinition` 已删除；`CardDefinition` 只保留卡牌专用 `CardArt` / `Artwork`，投影器只接受卡牌作者源。2.1a 已删除进程级 `ContentRegistrySystem`，`ContentIndex` 现在随单局存续并继续对跨包重复 ID 直接失败。 | 唯一 ID、SO、EX-GAS 标签、`ResourceSystem` 和派生索引继续保留。当前“默认包 + 已启用 Mod 包”快照链路已收口；剧本级 Mod 依赖与覆盖规则仍留给正式 Mod / 资源职责，不得因本轮通过而宣称已解决。 |
| 2. 启动与生命周期 | **没有发现 StackCraft 生搬。** | 正式入口没有 `GameDirector`、并行 `RuntimeContext`、StackCraft Manager 单例链、固定 `Main` 场景或 `Resources.LoadAll`；场景走 `SceneKit`，事件走 `EventKit`，资源走 `ResourceSystem`。 | 本次回审通过“未照搬”检查。`GameManager` 静态系统访问和 `MapSystem` 的宽职责仍是现有 GameCore 债务，不等于最终最佳实践；只有真实流程证明阻塞时再重构，不为审查形式新增第二入口。 |
| 3. 牌桌与卡牌 | **回审发现的错误泛化风险已完成代码订正。** | 状态、空间解算、表现、输入意图、测试和场景装配均已改为 `TabletopCard*` 卡牌专用合同，旧通用名称和兼容别名已清除。`TabletopCards.CreateCard` 仍建立独立单卡堆栈，因为这是可堆叠卡牌状态的不变量。 | 当前能力正式限定为“可堆叠卡牌子系统”。固定工位、圆形节点、连通节点和其它非卡牌表现不得塞进 `TabletopCardStack`；后续目标形态由真实行动消费者驱动设计。 |

回审没有否定模块 2 的测试结果，也没有否定模块 3 已复现的 StackCraft 卡牌拖拽手感；它订正的是职责范围。现有测试只能证明可堆叠卡牌链路成立，不能越权证明全部牌桌对象、全部交互形态或未来 Mod 覆盖已经成立。

### 4. 交互行动 / 配方 / 桌面行动进度

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `CraftingManager.CheckForRecipe` 根据卡堆组成找配方；`CraftingTask` 保存配方、目标堆、进度、暂停、取消；`RecipeDefinition` 支持消耗/保留/销毁、持续任务、允许额外材料、随机权重；特殊配方提供探索、研究、旅行、成长样例。 |
| GameCore 旧实现 | 旧 `Recipe` / `CraftingStation` / `UICraft` 是背包制作站模型：按 `Item` 扣材料、扣钱、把产物放进背包，由 `InventorySystem.ExecuteCraftRecipe` 直接执行交易；当前工作树里这些文件已被移除，只能作为候选参考，不是当前正式运行职责。 |
| GamePlay 职责归属 | 行动定义、配方条件、桌面行动进度、库存/牌桌状态变更和 GAS 效果结算共同承担，但必须形成一条正式链路。角色对地点/事件/道具/敌人执行行动，行动可以产生一段有进度的桌面作业；完成后发事件、产资源、解锁蓝图或施加 GameplayEffect。 |
| 裁决 | **冲突域，旧结构全部排除，玩家可见功能逐项重新裁决。** 不保留 GameCore 旧背包配方，也不保留 StackCraft `RecipeDefinition` / `CraftingTask` / `CraftingManager` 结构；最终只能形成一条由 GamePlay 需求推导的正式链路。 |
| 参考证据 | 模板证明可能存在延迟完成、暂停/恢复/取消、进度反馈、不同材料处理、额外参与对象、随机分支、研究发现、探索产出和旅行请求；这些只是待判断需求，不是默认实现清单。 |
| 目标方向 | 从“堆叠自动触发配方”或“背包菜单立即交易”改为“玩家选择参与者和行动 -> 查询可选行为 -> 按行动回合消耗推进 -> 必要时由回合规则换算即时速度 -> 由正式系统提交结果”。战斗始终即时并保持独立；连续执行和材料消费在后续小步单独证明。 |
| 排除范围 | 不吸收按具体 `CardDefinition` 完全等值匹配；不吸收旧 `Recipe` 只能按 `Item` 匹配背包材料；不让 `RecipeDefinition.Execute` 直接生成卡/切场景；不把旅行当固定场景名循环。 |

### 4.1 StackCraft 行动、配方与进度旧实现替换清单（2026-08-05）

#### 本小步边界

4.1 只读取参考源码、真实配方资产和当前项目正式职责，建立替换清单与后续拆分顺序。本小步不新增行动定义、配方定义、条件接口、任务系统、事件包装、进度组件、效果结算器或测试场景业务。

真相来源：

- StackCraft 作者定义：`Crafting/Definitions/RecipeDefinition.cs`、`ExplorationRecipe.cs`、`ResearchRecipe.cs`、`TravelRecipe.cs`、`GrowthRecipe.cs`。
- StackCraft 运行链：`CraftingManager.cs`、`CraftingTask.cs`、`CardController.cs`、`CardInstance.cs`、`CardAI.cs`、`CardStack.cs`。
- StackCraft 表现、校验与恢复：`ProgressUI.cs`、`RecipesView.cs`、`RecipeDefinitionEditor.cs`、`GameData.CraftingData`。
- 真实内容规模：`Assets/StackCraft/Resources/Recipes` 下共有 `90` 份配方资产，`90` 个 ID 全部唯一；其中普通配方 `77`、探索 `5`、成长 `6`、研究 `1`、旅行 `1`。`10` 份声明连续执行，`3` 份允许额外材料；材料配置实际使用 `Consume` `132` 次、`Keep` `87` 次，当前没有资产使用 `Destroy`。随机权重为 `1` 的有 `86` 份，另外四份使用 `0.5 / 0.5 / 0.15 / 0.1`。
- 当前项目正式边界：第三模块只产出 `TabletopCardPointerReleaseIntent`；内容身份归 `ContentId`，内容查询归 `ContentIndex`，标签语义归 EX-GAS，资源归 `ResourceSystem`，场景切换归 `MapSystem` / `SceneKit`，普通领域事件归 YokiFrame `EventKit`。
- 历史 GameCore 配方只从 Git 历史读取。当前工作树中的 `Recipe`、`CraftingStation`、`CraftInteraction`、`UICraft` 和旧库存制作入口已删除，不是现存 owner。

#### StackCraft 逐项拆解与裁决

| 参考对象 | 当前真实职责 | 参考证明的需求，不代表保留结构 | 结构裁决 | 后续正式职责 |
|----------|--------------|----------|----------------|--------------|
| `RecipeDefinition` | 一个 SO 同时保存配方 ID、UI 枚举分类、显示名、材料、结果卡、连续标志、额外材料标志、持续秒数、随机权重，并通过虚方法直接执行世界副作用。 | 模板证明“可由作者配置某种交互条件和结果”有价值，并暴露了显示、数量、候选分支和结果描述需求。它不能证明正式系统也需要同样字段或同一个 SO。 | 整个继承结构不保留。删除 `OnValidate` 自动不透明 GUID、`RecipeCategory`、固定 `CardDefinition` 条件和 SO `Execute`；作者源是否存在、分成几类、有哪些字段全部留给 4.2 重新推导。 | 4.2 裁决作者源边界；4.3 定义参与者与条件；4.7 定义结果意图和结算。 |
| `RecipeDefinition.Ingredient` | 用“具体卡牌引用 + 数量 + 消耗枚举”表达全部参与条件。 | 模板证明交互可能关心参与对象数量，并且完成后对不同对象可能有不同处理。 | `Ingredient` 结构和 `IngredientConsumption` 枚举不保留。角色、目标、工具、工位、物品和符号是否需要不同槽位由 4.3 裁决；耐久消耗/直接移除必须等 4.7 的真实状态修改入口，不为尚不存在的竞争新增预留层。`Destroy` 没有真实资产用例，更不能因代码存在而吸收。 | 4.3 参与槽位/条件；4.7 结果结算。 |
| `CraftingManager` | 单例同时负责 `Resources.LoadAll`、配方索引、匹配、随机选择、任务列表、`Update` 计时、UI 实例化、发现记录、存档回调、加入校验、重复执行和完成事件。 | 模板证明一条完整玩家流程会涉及候选、选择、等待或立即完成、反馈、结果和恢复，但不证明这些阶段必须全部存在，更不证明应该由一个 Manager 承担。 | 整类删除，不拆零件复用。单例、资源扫描、Unity `Update` 总循环、UI 字典、直接随机、直接执行、发现记录和存档汇总都不得迁入新总管类。 | 内容发现复用模块 1；其余职责只有在后续小步证明需要时，才进入对应正式模块。 |
| `CraftingTask` | 保存配方引用、目标卡堆、秒数进度和暂停/取消布尔值；完成条件是进度达到配方持续时间。 | 模板证明某些玩家行为可能不是立即完成，并可能被打断或恢复；它不能证明本项目也应以秒数作为行动作者真相。 | 整个类型、秒数作者数据和直接按 `Time.deltaTime` 累加的结构不保留；只吸收可见进度、暂停、恢复、取消和完成语义。普通行动在 4.6 改为回合消耗，切即时制时由回合规则换算。 | 4.6 时间模型与作业状态。 |
| `CardController` / `CardInstance` / `CardAI` 触发链 | 每次拆堆、放下、合堆、AI 移牌或销毁卡牌时直接暂停、恢复、停止或重新扫描配方。 | 模板证明参与对象变化可能影响正在进行的行为。 | 触发结构全部删除。“任何合堆都自动开始制作”不作为等价功能保留；是否出现按钮、默认执行或自动执行必须由 GamePlay 行动规则明确声明。 | 4.4 行动候选与选择；4.9 中断/重复策略。 |
| `ExplorationRecipe` | 从卡堆找 `Area` 枚举卡，读取地点 Loot，随机生成卡并发探索完成事件。 | 模板证明探索可以从地点状态或结果池得到产出。 | 特殊子类不保留；类别判断、随机读取、直接生成卡牌和 Manager 通知全部删除。是否吸收探索结果池功能由第四模块功能清单决定，若吸收则用通用条件/结果组合实现。 | 4.3 目标条件；4.7 结果意图；4.8 权威随机。 |
| `ResearchRecipe` | 从全局配方列表排除三个具体子类，随机挑未发现配方，生成配方卡并写发现集合。 | 模板明确提供“研究完成后随机解锁一项尚未发现配方”的玩家效果。 | 特殊子类、类型排除列表、全局单例和直接写发现状态全部删除；效果改由通用行动结果声明候选行动与配方卡，完成时读取 `ScenarioRun` 的唯一发现集合并使用牌桌权威随机流选择。 | 行动作者源与结果冻结归模块 4；发现事实归 `ScenarioRun`；随机归牌桌权威随机流。 |
| `TravelRecipe` | 保存场景名字符串列表，完成后消费输入并调用 `GameDirector.InitiateTravel`。 | 模板证明某种行动结果可能请求地区迁移并携带参与者。 | 特殊子类、固定场景名循环和 SO 直接切场景全部删除。只有“行动可产生旅行请求”被选入功能清单时，才接入现有 `MapSystem` / `SceneKit`。 | 4.7 旅行请求候选；正式执行复用 `MapSystem` / `SceneKit`。 |
| `GrowthRecipe` | 用具体子类和结果卡识别种植设施/种子，直接消耗、拆堆、平移、解重叠并生成结果。 | 模板证明一次结果可能包含多个状态变化。 | 特殊子类和直接牌桌操作全部删除。成长功能若被选中，只能作为通用结果组合的验收案例，不能为它恢复 Recipe 继承树。 | 4.7 组合结果候选，由各正式状态系统提交。 |
| `ProgressUI` / `RecipesView` | Manager 创建世界进度条；UI 直接读取任务、配方和发现集合。 | 模板证明玩家需要知道当前行为、剩余阶段或发现内容，并且世界锚点反馈有体验价值。 | UI 类型、Manager 数据绑定和枚举栏目全部不保留。若第四模块最终没有通用进度，就不为了复刻 `ProgressUI` 创建一套进度状态。 | 第 8 模块表现投影；第四模块只提供最终被证明需要的可观察状态。 |
| `RecipeDefinitionEditor` | 扫描项目内全部配方，对“具体卡牌 + 数量”完全相同的资产显示冲突、权重和跳转按钮。 | 模板证明作者需要发现歧义、查看候选概率并定位冲突资产。 | 编辑器实现和“具体卡牌 + 数量”签名全部删除。只有正式作者源和条件模型确定后，才重新设计对应校验。 | 4.10 作者校验与语义签名。 |
| `CraftingData` | 每个卡堆只保存配方 ID 和进度，读档时依赖当前卡堆重新创建任务。 | 模板证明跨阶段行为若存在，就需要恢复。 | 该保存结构全部删除。只有 4.6 确认存在正式跨阶段作业后，4.11 才按真实状态设计快照；不能先假设一定要保存进度。 | 4.11 根据最终运行状态裁决快照边界。 |

#### 当前项目与成熟框架校准

| 现有或成熟职责 | 当前能力 | 第四模块裁决 |
|----------------|----------|--------------|
| `TabletopCardPointerReleaseIntent` | 只记录来源卡牌、按下/释放位置、是否拖拽和候选卡牌，不修改牌桌状态。 | 继续作为空间输入事实；4.4 才把来源与候选解释成可选行动，不直接合堆或启动任务。 |
| `ContentAsset` / `DisplayableContentAsset` / `ContentIndex` | 前两者提供唯一内容 ID、内容标签码和可选展示信息，索引提供当前内容集合的查询入口。 | 后续行动作者源必须复用唯一 ID 和内容索引，不建 `Resources/Recipes` 扫描或第二内容目录。是否建立独立行动 SO 由 4.2 单独裁决。 |
| EX-GAS GameplayTag | 提供静态标签层级与角色动态标签查询；`TagRequirementData` 支持 all/any/none。 | 标签条件和角色状态查询直接复用 EX-GAS，不新建本地标签查询语法；固定内容、数量、槽位和世界事实仍由 GamePlay 条件模块表达。 |
| EX-GAS Ability / AbilityTask | Ability 负责角色技能的激活、Cost、Cooldown、取消/结束；AbilityTask/Timeline 只在激活 Ability 的生命周期内推进逻辑帧任务。 | 角色主动技能或引导行为优先复用 Ability/AbilityTask；桌面行动作业不能直接等同于 AbilityTask，因为它还要承载地点工位、多人参与、牌桌对象、存档和非角色世界行动。GamePlay 不复制技能状态机。 |
| EX-GAS GameplayEffect | 正式承担属性修改、动态标签、持续效果、周期效果和 Cue 触发。 | 行动完成后的属性/状态改变必须交给 GE；GamePlay 结果结算只提出效果请求，不自建 Buff、Modifier 或属性写入。 |
| `MapSystem` / `SceneKit` | 负责正式地图地址、过渡、加载/卸载和场景生命周期事件。 | 旅行结果只能请求该 owner，行动配置不能直接调用 `SceneManager` 或保存固定场景名循环。 |
| YokiFrame `EventKit` | 当前项目普通强类型领域事实的正式派发入口。 | 真正出现“行动开始/暂停/完成/失败”等跨模块消费者后直接使用 EventKit；4.1 不预建事件包装，也不为未来可能性先声明空事件。 |
| GameCore 历史 `Recipe` / `CraftingStation` / `InventorySystem.ExecuteCraftRecipe` | 曾提供费用检查、材料检查、统一扣钱、扣料、主产物/附加产物写入和失败原因。当前实现已删除。 | 不恢复旧背包制作系统；只保留“结算前完整验证、统一提交、明确失败原因”的事务经验。库存 owner 将来存在时由 4.7 调用，不在行动模块复制背包。 |
| Unreal Gameplay Ability System 校准 | 官方职责把激活检查、Cost/Cooldown 提交、AbilityTask 异步阶段、取消/结束和 GameplayEffect 结算分开，并由服务器重新校验预测激活。 | 证明 StackCraft“一个 Recipe SO 直接执行全部副作用”不是应保留的成熟边界；GamePlay 也必须把请求、条件、进度、取消、结算和权威验证拆开，但不把所有桌面行动强行伪装成角色 Ability。 |

成熟框架资料证据：Epic 官方 [Ability API](https://dev.epicgames.com/documentation/unreal-engine/BlueprintAPI/Ability) 列出的激活、Cost、Cooldown、Task、取消、结束和 GameplayEffect 操作边界；GASDocumentation 的 [能力激活与服务器复核说明](https://github.com/tranek/gasdocumentation/blob/master/README.md) 用于补充联机预测与服务器重新校验。EX-GAS 的当前项目能力仍以本仓 `Assets/Plugins/GAS/Wiki` 和源码为第一真相源。

#### 4.1 硬裁决

1. **参考不等于保留。** StackCraft 和旧 GameCore 的类、字段、枚举、继承、Manager、时间模型、UI 绑定和存档形状全部默认删除；只有被当前游戏需求再次证明、并列入功能吸收清单的玩家可见能力才进入正式设计。
2. **最佳实践不是拼接参考结构。** 第四模块最终只能有一条从 GamePlay 需求、现有正式系统、Unity 原生能力和成熟框架边界推导出的完整链路，不能让 StackCraft 提供进度、GameCore 提供交易、EX-GAS 提供标签后直接拼成一套混合结构。
3. **行动不等于配方。** 探索、采集、研究、旅行、学习、装备、使用技能和纯符号组合是否共享数据模型，要等 4.2 裁决；不得为了复用先建立万能 Recipe 基类。
4. **作者配置只声明，不执行。** 如果 4.2 证明需要 SO，它只能声明身份和被确认需要的数据，不能持有运行时 Manager，也不能直接生成/销毁对象、切场景、写发现状态或施加属性。
5. **牌桌组成不是默认触发器。** 合堆/释放只产生空间事实。“合堆自动制作”已明确排除；按钮、默认执行或自动执行只能来自后续正式行动规则。
6. **回合消耗是普通行动唯一进度真相。** StackCraft 的 `CraftingTask` 和秒数作者数据不保留；4.6 默认消费已确认回合，切换即时制时使用唯一回合时间规则把 `deltaTime` 换算成同一份回合进度。战斗始终即时且不读取该规则。存档恢复与联机权威必须等 4.11 根据最终真实状态设计。
7. **条件查询与结果结算必须分开。** “现在能不能做”不能在成功后顺便扣材料；只有真实消费需求和状态修改入口成立时才设计提交，并由对应状态系统执行。不得因为想象中的重复消费风险提前建立预留表或影子状态。
8. **EX-GAS 只承担它已经拥有的职责。** 角色技能生命周期、Cost、Cooldown、标签和 GameplayEffect 归 EX-GAS；GamePlay 不复制，也不把所有桌面行动强行包装成 AbilityTask。
9. **随机和表现都不能成为状态真相。** 正式随机由权威结算提供；按钮、高亮、进度和冲突提示只投影正式查询/运行状态。
10. **功能等价验收是一级模块门禁。** 第四模块全部子模块完成后，必须先在 `FoundationTest` 用新框架复现 StackCraft 玩家可见功能，并证明已排除的旧结构没有进入正式链路；只有用户明确作出产品排除裁决的效果可以不复现，通过后才能开始第五模块。

#### 第四模块后续拆分

| 子模块 | 只裁决什么 | 本步明确不做什么 |
|--------|------------|------------------|
| 4.2 行动/配方作者源边界 | **已完成：**只有行动是独立作者资产；配方是后续由参与条件与结果组合形成的行动语义，不建立第二套 SO。唯一 ID、显示和标签复用内容技术基类。 | 不写条件解释器或运行任务。 |
| 4.3 参与槽位与条件表达 | 角色、目标、工具、工位、物品、固定内容、EX-GAS 标签、符号、数量、蓝图和世界事实如何声明与查询。 | 不扣材料、不创建进度。 |
| 4.4 行动候选与玩家选择 | **已完成：**消费释放回调的交互组合入口提供当前可用行动集合；解析器确定性分配来源/目标卡并返回零个、一个或多个完整/待填充候选，玩家按行动唯一 ID 显式选择。 | 不直接合堆或执行结果；不隐式选第一个候选。 |
| 4.5 工位分配不变量与消费边界 | **已订正：**删除无真实入口依据的预留/共享/独占系统。工位不限参与人数；多人属于同一次行动；唯一归属由未来工位状态的唯一写入口保证，内部重复归属直接报错。StackCraft 三态消耗不照搬，真实卡牌材料变化交给 4.7。 | 当前不创建尚无真实状态入口的工位类，不扣耐久、不删卡、不扣库存。 |
| 4.6 时间模型与作业状态 | **已订正：**行动只配置回合消耗，作业只累计回合进度；系统默认回合制，允许由正式规则切换即时制并按唯一的每回合秒数换算。战斗始终即时且独立。 | 不复制 `CraftingTask`、AbilityTask 或 UI 状态；不保存行动持续秒数、世界当前回合、存档/网络状态或第二套结算真相。 |
| 4.7 结果意图与效果结算 | 如何把完成结果交给牌桌、未来库存、蓝图/发现、MapSystem 和 EX-GAS GE 的正式职责入口。 | SO 不直接产生副作用。 |
| 4.8 权威随机与候选分支 | **已完成：**正整数权重的结果分支在行动开始时由一次性注入种子的权威随机流选择，分支键写入作业供结算和解释。 | 不在客户端或 UI 决定正式结果；不随机替玩家选择行动。 |
| 4.9 连续执行与中断策略 | **已完成当前真实切片：**作业开始与每次推进前复核参与卡、内容和可选动态标签；参与失效、显式取消和系统关闭记录明确取消原因。重复执行必须重新查询并新建作业。 | 不吸收 `isContinuous`、剩余材料自动重扫或旧候选自动续作；不把连续标志塞回 Manager Update。 |
| 4.10 发现/蓝图与作者校验 | 研究结果池、发现记录边界、语义冲突签名、概率展示和引用断裂校验。 | 不提前实现剧本目标或正式 UI。 |
| 4.11 存档、联机、Mod 与统一测试场景收口 | 只为最终实际存在的运行状态设计快照、控制权、可见性和包依赖；在新框架测试场景逐项复现功能吸收清单。 | 不保存未实现的假想状态，不把存档文件或网络传输塞进行动运行对象。 |

#### 4.1 验收

- 已逐个读取 StackCraft 作者定义、运行任务、触发入口、表现、编辑器校验和保存快照；真实配方资产数量、ID、连续/额外材料、消耗模式和权重分布已经对账。
- 已读取当前 EX-GAS GameplayTag、Ability、AbilityTask/Timeline 和 GameplayEffect 正式文档，并用 Unreal GAS 的激活、Task、取消、效果和服务器复核职责做成熟框架校准。
- 已订正“参考什么就保留什么”的错误口径：4.1 只确认来源事实、暴露需求和排除旧结构，不承诺保留进度、暂停、材料枚举、特殊 Recipe、UI 或存档形状。
- 本小步没有新增或修改运行时代码、Unity 资源、场景、程序集依赖或事件类型；纯文档裁决不以测试通过冒充业务实现，也不触发一级模块功能等价门禁。

### 4.2 行动/配方作者源边界（2026-08-05）

#### 参考事实

- StackCraft 共有 `90` 个真实 Recipe 资产：`77` 个基础 Recipe、`5` 个探索、`6` 个成长、`1` 个研究、`1` 个旅行。
- 这些资产共用唯一 ID、显示名和“可被独立发现/引用”的作者生命周期，但参与材料、持续秒数、随机权重、连续执行和世界副作用被塞在同一个 SO 中。真实数据里有 `10` 个连续执行、`3` 个允许额外材料、`7` 个无固定结果卡；持续时间分布为 `5-180` 秒，随机权重有 `0.1/0.15/0.5/1`。
- 上述分布证明行动需要独立作者身份，但不能证明条件、消耗、时间、随机、结果和执行代码应该继续由同一个 Recipe SO 拥有。

#### 正式裁决

1. **只有一个正式作者源：`ActionDefinition`。** 它表示可被牌桌、世界节点、AI 或后续行动解析器独立引用的行动内容。
2. **不建立 `GamePlayRecipeDefinition`。** “配方”不是第二种运行链路，而是某个行动在 4.3 参与条件和 4.7 结果组合下呈现出的作者/玩家语义；制作、探索、研究、旅行和成长不再通过特殊 Recipe 子类区分。
3. **身份与展示不重复。** 行动继承 `DisplayableContentAsset`，通过其狭窄父层复用唯一 `ContentId`、显示名、描述、图标和一组 EX-GAS 标签码；不新增 `ActionId`、Recipe ID、分类枚举、第二标签集或手工登记表。
4. **当前无额外序列化字段是有意边界。** 行动已经有真实独立生命周期：可创建 SO、进入 YooAsset 内容包、被内容索引强类型查询、被 Mod 派生。参与槽位、条件、消耗、时间、随机和结果尚未裁决，不能为了让类型“看起来不空”提前塞入错误字段。
5. **允许 Mod 派生，但配置不执行副作用。** `ActionDefinition` 保持可继承；Mod 可以扩展作者数据，但 SO 不提供 `Execute`、Manager 引用、场景切换、生成/销毁卡牌或属性写入入口。正式扩展数据仍需经过对应模块的校验和执行 owner。
6. **引用继续使用唯一内容 ID。** 未来地点、工位、剧本或 UI 只保存 `ContentId`，通过 `ContentIndex.TryGet<ActionDefinition>` 校验类型；当前没有消费者证明需要 `GamePlayActionReference` 包装，因此不新增。

#### 与现有职责边界

| 现有职责 | 4.2 边界 |
|----------|----------|
| EX-GAS Ability | 角色技能的激活、Cost、Cooldown、取消、结束和逻辑帧 Tick 继续归 Ability；桌面/世界行动作者源不复制技能生命周期，也不继承 Ability 配置。 |
| GameCore `IInteraction` | 当前是绑定 `CharacterBase` / `IInteractionTarget` 的运行时执行接口，不是可独立索引、Mod 覆盖的行动作者源；4.2 不桥接、不包装，也不拿它承载作者数据。 |
| `ContentIndex` | 继续作为唯一内容 ID 查询入口，已能强类型区分行动与卡牌，不新增行动目录或第二索引。 |
| EX-GAS GameplayTag | 制作、探索、研究等开放分类后续用正式标签语义表达，不恢复 `RecipeCategory` 或 `ActionKind` 枚举。 |

#### 实现与验收

- 新增 `Assets/Scripts/GamePlay/Runtime/Actions/ActionDefinition.cs`，只定义行动作者生命周期和边界，没有条件、时间、结果或执行方法。
- 统一测试生成器新增真实作者资产 `Assets/GamePlay/Tests/地基测试行动.asset`，内容 ID 为 `test.foundation.action`；它由现有 `ContentAssetFilterRule` 自动进入 YooAsset，不增加专用 Collector。
- RED：PlayMode 合同首次编译只因 `ActionDefinition` 不存在而出现 `CS0246`，没有其它目标外错误。
- GREEN：`TestResults-Module42-Green-R2.xml` `1/1` 通过，验证行动资产经过 `ResourceSystem` / YooAsset 进入正式索引、显示名正确，并且不能被强类型查询为卡牌。
- 生成器暴露并修复了 batchmode 退出竞态：大量 AssetDatabase 写入后增加同步刷新，原始 `-executeMethod -quit` 链路最终退出码为 `0`；证据为 `Temp/module42-scene-rebuild-r3-20260805.log`。
- PlayMode 整组回归暴露了正式输入 owner 的生命周期缺口：外部 Gameplay/UI 监听此前没有被 owner 记录，旧场景销毁顺序会让牌桌 `Click` 回调残留到下一场景。`GameCore.InputSystem` 现在统一登记外部监听，并在 `OnSystemStop` / `OnSystemShutdown` 清理；牌桌解绑也会先清本地订阅状态，不能再因已销毁的 Unity owner 跳过收口。
- 最终验证：`TestResults-Module42-PlayMode-R4.xml` `4/4`、`TestResults-Module42-GamePlay-EditMode.xml` `18/18`、`TestResults-Module42-AllEditMode.xml` 为 `323` 通过、`1` 条既有条件跳过、`0` 失败；内容校验扫描 `2` 个作者资产全部通过，行动作者源职责扫描和 `.spec` lint 通过。

#### 下一小步

4.5 只裁决工位分配不变量与消费边界：多个角色怎样共同属于同一次工位行动、内部重复归属怎样直接暴露，以及为什么当前不能建立预留/锁表；不修改属性、不生成产物、不创建实时作业。

### 4.3 参与槽位与条件表达（2026-08-05）

#### StackCraft 参考结论

- `CraftingManager.DoesStackMatchRecipe` 把卡堆按具体 `CardDefinition` 分组计数，再逐项匹配 `RequiredIngredients`。资源类只要求“至少数量”，其它卡牌要求数量完全相等；`AllowExcessIngredients` 为真时又整体跳过多余对象检查。
- 这套实现证明数量、固定对象和额外参与对象是实际需求，但它把内容类别、数量策略和工位特例写死在 `CardCategory` 与布尔分支里。它不能表达“任意带木质符号的对象”“两个角色和一个工位”“角色当前没有受伤状态”，也无法供 Mod 增加新的参与语义。
- `IngredientConsumption` 不属于条件查询。是否保留、消耗耐久或直接移除，只能在 4.7 建立真实状态修改与结果提交时裁决，不能重新塞回参与槽位，也不能提前创建预留计划。

#### 正式数据合同

1. **行动仍是唯一作者源。** `ActionDefinition.ParticipationSlots` 保存该行动需要的参与位置；没有 `RecipeDefinition`、配方 ID、配方分类或第二内容索引。
2. **槽位不是封闭类型枚举。** 每个 `ActionSlotDefinition` 使用行动内稳定 `Key` 区分绑定位置，并有独立显示名。`Key` 只相当于该行动的参数名，不进入全局内容索引，也不是第二个内容 ID；角色、目标、工具、工位、物品或 Mod 自定义位置都通过新增槽位数据表达，不修改核心枚举。
3. **数量属于一次行动的槽位基数。** 每个槽位声明最少和最多参与对象数；最少为 `0` 表示可选，最多为 `0` 表示同一次行动内不限制人数，适合任意数量角色共同参与。它不表示地点有多少工位，也不表示可以并行多少次行动；不恢复 StackCraft 的“资源至少、其它严格相等”类别特判。
4. **固定内容与开放语义分开。** `AllowedContentIds` 非空时按唯一 `ContentId` 白名单限制具体内容；角色、木质、工具、燃料等开放分类和策划所说的“符号”使用 EX-GAS 静态内容标签的 all/any/none 查询，不建立 `CardCategory`、`ActionKind` 或 string 符号表。
5. **静态内容标签与角色动态标签不合并。** 内容标签回答“这是什么内容”，通过 `TagHelper.HasTag(actualTag, queryTag)` 支持子标签匹配父标签；角色动态标签回答“这个角色当前处于什么状态”，直接通过 `AbilitySystemCell.HasAllTags` / `HasAnyTags` 查询固有和临时标签。非角色槽位没有动态标签条件时不要求伪造 Cell。
6. **查询没有副作用。** `ActionParticipationEvaluator` 只判断数量、固定内容、静态标签和动态标签；它不合堆、不锁定对象、不扣材料、不改耐久、不启动计时、不选择随机结果，也不发送“已开始”事件。
7. **调用方必须明确分配槽位。** 4.3 不做自动组合搜索，也不从整个牌桌扫描“最像的配方”。4.4 才根据拖拽来源、目标和玩家补充选择形成槽位绑定，然后逐槽查询。

#### 没有进入本步的条件

| 条件 | 本步裁决 | 正式 owner / 后续入口 |
|------|----------|-----------------------|
| 蓝图是否已解锁 | 不是参与对象，也不是内容自身标签；它是某个玩家/队伍/剧本当前的发现状态。 | 4.10 已建立行动发现状态过滤，但未建立完整蓝图系统；后续蓝图规则必须消费正式发现 / 世界状态，不借 `GameFlagSystem` 字符串冒充。 |
| 世界事实、剧本阶段、天气 | 不是槽位数据，且当前正式世界状态 owner 尚未建立。 | 第 5 模块建立世界规则/事实状态后提供权威查询；4.3 不复制旧 `ICondition.Evaluate()` 的隐藏全局访问。 |
| 属性、技能、职业等级 | 角色运行条件，但具体查询合同尚未裁决。 | 角色/GAS 正式 owner 出现后扩展参与查询；不把 DND 属性或职业字段提前写进行动槽位。 |
| 距离、相邻、前后排 | 属于运行时空间关系，不是内容作者身份。 | 4.4 形成候选时读取牌桌/地图正式状态；不把坐标快照写入 SO。 |

这些延期不是用字符串通用条件留坑，而是防止 4.3 抢走尚未成立的世界状态、蓝图、角色属性和空间 owner。后续扩展必须接入各自正式状态来源，再由行动查询组合；不得恢复参数为空的全局 `GameCore.ICondition` 或新造万能条件脚本。

#### 实现与验收

- `GamePlayActionParticipation.cs` 新增一个可序列化槽位合同和一个无状态查询器；`GamePlay.Runtime` 直接引用 `com.exhard.exgas.runtime`，没有第三方能力包装、项目侧标签表或本地标签身份。
- 统一测试行动配置一个需要 `2` 名参与者的槽位：固定内容 ID 为 `test.foundation.card`，静态内容标签以 `Faction_Player` 子标签匹配 `Faction` 父标签，角色动态标签以 `Ability_Gun_Shoot` 子标签匹配 `Ability_Gun` 父标签，并分别验证 any/none。
- RED 证据：`Temp/module43-red-r2.log` 只因 `ParticipationSlots`、`ActionSlotDefinition` 和 `ActionParticipationEvaluator` 尚不存在而编译失败。
- GREEN 与反证：`Temp/TestResults-Module43-Boundaries.xml` 为 `1/1` 通过；覆盖数量不足/满足/超出、无上限槽位、固定内容不匹配、静态标签缺失/禁止、动态标签缺失/禁止，以及无动态条件的非角色内容。
- 新鲜回归：统一场景生成器退出码为 `0`；`Temp/TestResults-Module43-PlayMode.xml` 为 `5/5`，`Temp/TestResults-Module43-GamePlay-EditMode.xml` 为 `18/18`，`Temp/TestResults-Module43-AllEditMode.xml` 为 `323` 通过、`1` 条既有条件跳过、`0` 失败；内容校验扫描 `2` 个作者资产并全部通过。
- 4.10 已把行动作者源基础形状接入 `ContentValidator`。同条件多行动不再做模板式签名提示，因为本项目需要把它们作为多个玩家选项；负回合数在作者校验阶段拒绝，运行查询仍保留对失效输入的拒绝。

### 4.4 行动候选与玩家选择（2026-08-05）

#### StackCraft 参考结论

- `CardController.OnPointerUp` 和 `CardInstance` 的放置链先改变物理卡堆，再调用 `CraftingManager.CheckForRecipe` 扫描全部 Recipe；卡牌拆堆、AI 移动和活动任务变化也会重新扫描。
- 多个 Recipe 匹配时，`CraftingManager` 直接按权重随机选一个并创建 `CraftingTask`。玩家看不到完整候选，也没有明确选择步骤；“空间上能堆”被当成“规则上应该执行”。
- 这套链路只证明放下卡牌后需要反馈可用行为，不证明应该先合堆、全局扫行动、随机替玩家选择或立即开工。GamePlay 的释放意图必须先保持为只读输入事实。

#### 正式查询与选择合同

1. **输入层不拥有行动。** `TabletopCardDragInput` 继续只通过绑定回调提交 `TabletopCardPointerReleaseIntent`；它不引用行动定义、不扫描内容索引，也不新增 EventKit 包装事件。绑定该回调的交互组合 owner 才消费释放事实。
2. **可用行动集合必须显式提供。** 交互提供者提交行动唯一 ID，不传入可替代本局真相的外部行动 SO；`ScenarioRun` 只从本局 `ContentIndex` 解析正式定义并应用发现状态，内部候选解析器不遍历 `ContentIndex.AllAssets`。后续地点、工位、剧本、权限和秘密信息 owner 可以先裁剪当前可见行动 ID；测试场景只提供 `test.foundation.action`。
3. **来源与目标都是待分配参与者。** 解析器从 `TabletopCards` 读取局内卡牌，再通过唯一内容 ID 从 `ContentIndex` 取得作者资产；所有输入卡都必须分配进某个 4.3 槽位，否则该行动不是候选。
4. **不新增 Source/Target 内容枚举。** 解析器先处理拖拽来源、再处理命中目标，并在所有合法槽位分配中选择缺少参与者最少的结果；条件和缺口相同的歧义由行动槽位作者顺序稳定裁决。因此同类角色可以用第一个槽位表示发起者、第二个槽位表示目标，异类对象则由内容 ID/标签条件自然区分。
5. **允许待填充候选。** 已提供卡牌全部能进入槽位，但仍未达到某些最少数量时，候选保留并给出 `MissingParticipantCount`。这为《苏丹的游戏》式弹窗补卡提供规则数据；未就绪候选不能提交为行动。

### 7.2 行动选择与填槽吸收结果（2026-08-12）

- 吸收 StackCraft 拖拽后提供多个可行动选项的玩家效果，不吸收卡牌脚本直接查询全局 Manager、自动选择单一配方或调用方手填行动清单。
- 当前剧本单局的已发现内容是行动授权真相；`ScenarioRun` 自动筛选已发现行动，`Tabletop` 用现有内容与 EX-GAS 槽位条件解析候选。
- 候选快照仍是一次交互的只读结果；未完成候选选择后创建独立 `ActionPlan`。计划由 `Tabletop` 直接拥有，不升级为全局 System，也不放在 UIKit 内保存。
- `TabletopInteraction` 协调候选选择、直接提交与计划面板；候选面板只回传选择。填槽面板和牌桌拖拽共同编辑同一个计划，完整后才生成 `ActionRequest`。
- 现有正式链路已支持：零到多个候选、完整候选直接启动、未完整候选形成计划、拖入更多牌、移出、取消和提交。尚未实现作者工具中的可视化槽位编辑器、正式 UI 皮肤、联机命令和计划存档。
- 同一牌桌可并列多个待计划，但 UIKit 不创建多个面板或计划副本；一个填槽面板在牌桌的 `ActionPlans` 间切换。卡牌从牌桌移除时同步解绑计划，仍填在计划中的卡牌禁止跨地区旅行。
6. **零个、一个和多个候选使用同一返回结构。** 没有匹配时返回空数组；一个候选仍只是一个选项；多个候选保持调用方提供的稳定顺序，供第 8 模块显示按钮或选择面板。重复提供同一行动 ID 时只保留第一次，避免重复按钮。
7. **选择必须引用唯一行动 ID。** `TabletopCardActionCandidateSelector.TrySelect` 只能从本次候选快照按 `ContentId` 取回候选；任意其它行动 ID 都会失败。没有 `CandidateId`、选择索引身份或第二套 Action ID。
8. **单候选不等于自动执行。** 解析器不隐式选择第一个候选，也没有 `IsDefault`、`AutoExecute`、优先级或随机权重字段。默认行为取决于地点/交互提供者当前上下文；自动行为必须由后续明确规则、控制权和权威复核证明，不能成为行动定义的全局布尔值。
9. **候选与选择都没有副作用。** 不移动、拆堆、合堆、锁卡、扣材料、创建任务、发送完成事件或执行结果；候选就绪只表示参与条件已经满足，可以交给后续正式行动入口。
10. **角色 GAS 状态直接归角色卡。** 候选查询只有一个入口；运行时卡牌若实际是 `CharacterCard`，就读取它直接拥有的唯一 `AbilitySystemCell`。纯物品、地点和静态符号没有运行时 GAS 状态，不再暴露第二个查询入口或外部解析回调。

#### 当前没有抢先建立的 owner

| 职责 | 4.4 裁决 |
|------|----------|
| 地点/工位提供哪些行动 | 当前具体地点作者源尚未建立，因此不把行动列表塞进 `CardDefinition`，也不全局扫描。未来提供者把已按剧本、控制权和可见性裁剪的行动集合传给解析器。 |
| 默认行动与按钮排序 | 属于提供者上下文和 UI 投影，不是行动自身永久属性；第 8 模块根据候选与提供者数据表现。 |
| 控制权、联机授权、秘密行动 | 候选只是本地可见查询快照，不是服务器授权。4.11 必须按发起玩家和权威状态重新查询，不能同步一个客户端候选就直接执行。 |
| 无行动时移动或合堆 | 属于牌桌放置/堆叠命令策略，不能被“零候选”隐式决定；本步保持原权威牌桌状态不变。 |

#### 实现与验收

- `TabletopCardActionCandidates.cs` 新增不可变槽位绑定、候选快照、确定性解析器和显式选择入口；没有新增 Manager、MonoBehaviour 单例、事件包装、候选 ID、全局行动索引或执行接口。
- `TabletopCards.TryGetCard` 只开放牌桌局内引用的只读解析，所有成员关系写入仍归原状态 owner。
- RED：`Temp/module44-red.log` 只因候选/解析/选择类型不存在而失败；`Temp/module44-playmode-red.log` 只因统一测试控制器尚未提供候选观察入口而失败。
- 纯规则 GREEN：`Temp/TestResults-Module44-Green-R2.xml` 为 `1/1`；后续 GamePlay EditMode `20/20` 覆盖零/一/多候选、部分填槽、显式选择、重复行动去重和同条件槽位的稳定方向分配。
- 真实场景 GREEN：`Temp/TestResults-Module44-PlayMode.xml` 为 `5/5`；真实鼠标拖拽中间卡到目标卡后得到 `1` 个已就绪行动候选，绑定两张正确局内卡牌，牌桌仍保持两个独立堆栈。
- 全量回归：`Temp/TestResults-Module44-AllEditMode.xml` 为 `325` 通过、`1` 条既有条件跳过、`0` 失败；内容校验扫描 `2` 个作者资产并全部通过，候选职责扫描确认没有牌桌状态写入、全局行动扫描、第二候选 ID、Recipe、随机、默认或自动执行字段。
- EX-GAS 标签表在 PlayMode 退出时仍报告已登记的 Persistent 原生容器未释放；本步新增的四个测试 `AbilitySystemCell` 已在控制器禁用时逐个释放，泄漏栈仍指向既有 `TagHelper.InitTagMap`，不能把候选测试通过冒充该插件生命周期缺口已修复。

### 4.5 工位分配不变量与消费边界（2026-08-06 订正）

#### 错误回审

- 先前新增的 `TabletopCardActionReservations`、共享计数、独占/共享开关和候选来源牌桌记录，没有对应的正式工位状态或第二个合法行动入口。它们只是在预防错误调用能够制造的重复归属，形成了牌桌状态之外的第二份占用真相。
- StackCraft 只有一个卡堆制作状态和一条 `CraftingTask` 关系，没有独立预留表。模板的不足是把工位、参与者和材料都压进卡堆，不是缺少一张全局冲突表。
- 删除测试后复杂度直接消失，也没有合法调用方需要重新实现这套竞争处理；按 deletion test，它不是有独立职责的深模块，而是在保护错误状态模型。

#### 正式工位不变量

1. **工位数量和参与人数分开。** 固定工位数量表示同一时间可以运行多少次独立行动；单个工位不限制参与人数。沙滩两个固定工位表示最多并行两次行动，不表示每次只能放一个角色；营地无限休息工位表示可以按需建立任意数量独立休息行动。
2. **多人属于同一次行动。** 多个角色放入一个工位后，共同组成该工位当前行动的参与者列表；不能解释成每个角色各自创建一条行动，再让这些行动“共享地点”。
3. **唯一归属由状态结构保证。** 正式工位状态出现时，角色分配与移除只能通过一个写入口；角色当前归属直接保存在该权威关系中。合法 UI、AI 和单机流程只能消费这份关系，不增加预留表、锁表、`IsBusy` 防重标记或共享计数。
4. **内部错误立即暴露。** 如果内部代码绕过正式入口，让同一角色同时属于两个工位行动，这是程序不变量被破坏，必须在最靠近写入的位置抛出带角色和工位信息的明确异常，不返回 `ParticipantAlreadyReserved` 让游戏继续运行。
5. **外部过期命令是正常拒绝。** 联机客户端可能基于旧画面提交已经失效的分配命令；权威端按当前工位状态处理，返回“状态已变化”的命令拒绝，不把客户端候选或预留对象当作授权。
6. **当前不伪造工位状态。** 工作树尚无固定工位、圆形工位、卡牌工位统一后的真实运行状态与创建入口。本步只删除错误职责并锁定不变量，不为了填空立即新增 `WorkstationState`、Manager、容量表或通用接口。

#### 多人默认判定

- 多人结算策略允许行动和 Mod 自定义，不通过工位容量或共享开关表达。
- 2026-08-06 最新设计覆盖旧口径：默认由参与人数缩短行动耗时，成功判定只读取相关判定等级最高的参与者，不因人数增加自动获得多次独立重掷。
- 多人耗时倍率、复合属性判定、难度、隐藏阈值、随机种子和结果解释仍必须进入各自正式职责；本步只记录产品规则，不提前实现万能掷骰器或工位策略接口。

#### 消费边界

- 真实配方资产使用 `Consume` `132` 次、`Keep` `87` 次，当前没有资产使用 `Destroy`；这只能证明完成时可能对不同参与对象产生不同结果，不能证明三态枚举或预留计划应保留。
- 当前没有牌桌卡牌耐久、正式删卡入口或库存数量状态，因此不恢复 `IngredientConsumption`、消费标签或 `Consume()` 接口。
- 角色技能 Cost、Cooldown 和 GameplayEffect 继续归 EX-GAS；卡牌材料的耐久变化、移除和 Mod 自定义消费语义等 4.7 建立真实状态修改与结果提交后裁决。

#### 删除与订正

- 删除 `TabletopCardActionReservations.cs` 及其 `.meta`，同时删除六个只证明预留体系自身行为的 EditMode 测试。
- `TabletopCardActionCandidate` 删除来源牌桌引用；候选恢复为一次无副作用查询结果，不承担授权、锁定或占用状态。
- `ActionSlotDefinition` 删除独占/共享开关；`MaximumParticipants = 0` 只表示同一次行动内该槽位参与人数不限，不再冒充工位数量或并行行动数量。
- 遵循系统 AGENTS `D:\codex-home\AGENTS.md` 的“禁止无依据防护性架构”红线；具体裁决见系统 skill `D:\codex-home\skills\improve-codebase-architecture\SKILL.md`。

#### 验收

- 运行时代码、测试和 Unity 资产残留扫描未发现 `TabletopCardActionReservations`、`RequiresExclusiveReservation`、候选来源牌桌或预留失败枚举；纠错文档中的历史名称只用于说明删除原因。
- `Temp/TestResults-Module45-Correction-GamePlay.xml` 为 `20/20`；删除的六个测试只验证错误预留体系自身，不属于业务覆盖。
- `Temp/TestResults-Module45-Correction-AllEditMode.xml` 为 `325` 通过、`1` 条既有条件跳过、`0` 失败；`Temp/TestResults-Module45-Correction-PlayMode.xml` 为 `5/5`；内容作者资产校验为 `2/2`。
- 本轮当时的 PlayMode 日志曾报告 68 笔 Persistent 分配；后续已由 EX-GAS 正式 `Shutdown()` 修复并通过带栈泄漏复测。旧预留测试通过记录只证明被删除实现曾自洽，不再作为正式架构证据。

#### 下一小步

4.6 只建立普通行动作业的真实状态、唯一创建入口、参与者列表、回合进度、回合 / 即时换算和开始/暂停/恢复/取消/完成；工位运行状态必须等固定、圆形和卡牌工位的共同事实足以确定后再建立，不复制 `CraftingTask`，也不提前执行 4.7 的材料与结果结算。

### 4.6 时间模型与作业状态（2026-08-06，回合真相订正）

#### 同职责对照

| 来源 | 同职责流程 | 本项目裁决 |
|------|------------|------------|
| StackCraft `CraftingTask` | 保存 Recipe 和目标卡堆，用进度、暂停、取消三个字段拼生命周期；`CraftingManager.Update` 直接累加 `Time.deltaTime`，同时刷新 UI、执行 Recipe、移除任务和尝试连续制作。 | 只吸收可见进度、暂停、恢复、取消和完成。秒数作者字段、Recipe/卡堆引用、多布尔状态、Manager 总循环、UI 和结果执行全部排除。 |
| 普通行动回合消耗 | 普通行动默认在结束回合后推进；同一剧本也允许切换成即时推进。 | `ActionDefinition.TurnCost` 是每个普通行动唯一耗时作者数据；`TabletopCardActionJob.ProgressedTurns` 是唯一运行进度。两种模式不复制或转换作业字段。 |
| `ScenarioDefinition.SecondsPerTurn` | 当前剧本需要说明普通行动切到即时制后，一个回合单位对应多少游戏秒。 | 数值直接属于剧本时间规则，不建立独立 SO；它与具体行动回合消耗正交，即时增量固定为 `deltaTime / SecondsPerTurn`。 |
| 当前 `GameStateSystem` / Unity 游戏时间 | 菜单状态通过 `Time.timeScale` 控制全局游戏暂停，Unity `Time.deltaTime` 自动反映全局暂停与倍速。 | 只在普通行动选择即时模式时提供缩放后的秒数增量；回合制不会因 `Update` 自动推进。 |
| EX-GAS `TurnController` / `GlobalTimer` | 插件当前提供回合计数结构，但没有普通行动每回合秒数配置；当前源码里 `TurnController` 也没有与 `GlobalTimer.Turn` 形成正式推进链。 | 不把 GAS 回合计数冒充世界回合 owner 或时间换算配置。第 5 模块建立正式世界回合流程后，再统一编排普通行动、日结和 GAS 回合推进。 |
| 实时战斗 / EX-GAS 帧链 | 战斗攻击、技能时间轴、Cooldown 和 GameplayEffect 帧持续由实时战斗与 GAS 逻辑帧推进。 | 战斗始终即时，不读取 `TurnCost` 的即时换算速度，也不进入普通行动模式开关。 |
| YokiFrame `ActionKit` | 通用延迟、插值和序列工具，自行注册静态 PlayerLoop，并维护 `ActionID`、`IAction` 状态、Controller 暂停/取消状态和对象池。 | 不用于正式行动作业。套用后仍需另存行动身份、参与者和进度，会形成两份生命周期与第二运行 ID；其异常处理还会记录后回收，不符合内部不变量立即暴露的要求。继续保留给表现和通用短序列使用。 |
| EX-GAS AbilityTask | 在 Ability 激活生命周期内推进角色技能任务并随 Ability 取消或结束。 | 角色技能继续复用 EX-GAS；地点、物品和多人牌桌行动不伪装成 AbilityTask。 |

#### 正式状态与唯一入口

1. **回合消耗属于行动作者源。** `ActionDefinition.TurnCost` 表示普通行动完成所需回合数；`0` 表示显式选择后立即完成。`DurationSeconds` 已删除，作者不能为即时制再维护一份行动耗时。
2. **一次作业只有一个状态。** `TabletopCardActionJobState` 只包含运行、暂停、完成和取消，替代 StackCraft 可同时出现暂停/取消/完成的布尔组合。它是系统完整解释的闭合生命周期，不是需要 Mod 增加成员的内容分类。
3. **即时换算只有一个规则源。** `ScenarioDefinition.SecondsPerTurn` 表示本剧本一个回合单位对应的游戏秒数。它不是第二行动耗时：行动总工作量仍只有 `TurnCost`，Mod 通过剧本作者源修改该规则。
4. **作业只保存回合进度。** `TabletopCardActionJob` 保存唯一行动内容 ID、玩家已选候选的参与槽位绑定、`TurnCost` 和 `ProgressedTurns`；回合制每次增加 `1`，即时制每帧增加 `deltaTime / SecondsPerTurn`，切换时不改存量进度。
5. **普通行动默认回合制。** `ScenarioTurnSystem.ConfirmTurn()` 确认世界回合并通过 YokiFrame `EventKit.Type` 发布事实；`TabletopCardActionSystem` 订阅该事实并在 `TurnBased` 下推进作业，不保存当前世界回合数。`UseRealTimeProgression()` 和 `UseTurnBasedProgression()` 只切换同一作业进度的推进方式。
6. **战斗始终即时且独立。** 普通行动模式开关不能暂停、离散化或换算战斗攻击、技能 Timeline、Cooldown 和实时 GameplayEffect；战斗也不能写普通行动回合进度。
7. **状态只有一个写入 owner。** `TabletopCardActionSystem` 是 `GameManager` 正式装配的 `AGameSystem`，只有它创建、推进、暂停、恢复、取消并移除作业。作业对外只读；完成和取消后立即离开活动集合，不建立历史表、完成表、UI 字典或事件镜像。
8. **非法调用直接报错。** 未启动系统、未填满候选、负回合消耗、非法每回合秒数、错误模式推进、非本系统活动作业或错误状态迁移会在调用点抛出明确异常；不返回假成功、不自动夹取作者数据，也不增加兜底状态。
9. **工位归属仍不由本步冒充。** 当前作业记录“本次行动有哪些参与卡牌”，不声称某个角色已经被固定/圆形/卡牌工位权威占用。角色唯一工位归属仍必须等真实工位状态出现后由其唯一写入口表达；本步不通过扫描活动作业伪造占用表。
10. **行动作业不额外发布镜像事件。** 世界回合确认事实已有真实消费者 `TabletopCardActionSystem`，直接走 YokiFrame `EventKit.Type`；行动开始、暂停、完成和取消仍直接观察作业状态，未为 UI、目标、音效或联机同步预建第二事件层。

#### 当前明确延后

- 材料扣除、卡牌销毁、GameplayEffect、产物、旅行和蓝图结果全部属于 4.7。
- 权威随机属于 4.8；连续执行和参与者移除后的中断属于 4.9。
- 工位运行状态、存档快照、网络命令、服务器复核和 Mod API 暴露必须按最终真实状态在 4.11 裁决；本步没有创建对应字段、接口或拒绝码。
- 第 5 模块的 5.1 已建立世界当前回合和玩家确认的唯一正式入口；NPC 意图、日结阶段和 EX-GAS 回合仍未实现。4.6 的行动进度只能消费 5.1 发布的已确认事实。
- 当前只实现普通行动内置的回合 / 即时两种推进。Mod 自定义第三种推进策略和公开 API 等 4.11 根据真实 Mod 运行入口裁决，不为它预建无消费者接口。

#### 实现与验收

- 删除 `ActionDefinition.DurationSeconds`、作业 `DurationSeconds / ElapsedSeconds` 和所有行动秒数作者数据；保留 `TurnCost`、`ProgressedTurns`，唯一换算值归 `ScenarioDefinition.SecondsPerTurn`。独立 `TurnTimingDefinition` 已在 2026-08-11 删除。
- `TabletopCardActionSystem` 默认回合制，直接消费 `ScenarioTurnConfirmedEvent`；即时制只换算同一进度。它没有引用 StackCraft、ActionKit、AbilityTask、存档、网络或战斗时钟类型。
- 回合真相 RED 为 `Temp/module46-turn-truth-red.log`，当时只因旧模型缺少 `TurnCost`、回合进度、默认模式和直接回合推进入口而失败；5.1 已删除该直接入口，改由世界回合事实驱动。定向 EditMode GREEN 为 `Temp/TestResults-Module46-TurnTruth-Green-R2.xml`，`4/4` 通过。
- MonoBehaviour 曾与纯作业同处 `TabletopCardActionJob.cs`，场景重新打开后无法恢复正式脚本引用；已按 Unity 组件资产规则拆到同名 `TabletopCardActionSystem.cs`，生成器继续严格校验而未增加兜底。最终重建日志 `Temp/module46-turn-truth-rebuild-r2.log` 退出码为 `0`。
- 测试行动只配置 `TurnCost = 2`；测试回合规则只配置 `SecondsPerTurn = 0.35`。定向 PlayMode `Temp/TestResults-Module46-TurnTruth-PlayMode.xml` 为 `2/2`：默认回合制等待现实时间不推进，确认一回合后为 `1/2`，切换即时制后继续同一份进度，并覆盖全局暂停/倍速、作业暂停/恢复、完成和取消。
- 最终回归：`Temp/TestResults-Module46-TurnTruth-GamePlayEditMode.xml` 为 `24/24`；`Temp/TestResults-Module46-TurnTruth-AllEditMode.xml` 为 `329` 通过、`1` 条既有条件不适用跳过、`0` 失败；`Temp/TestResults-Module46-TurnTruth-AllPlayMode-Final.xml` 为 `6/6`。
- 内容作者资产校验为 `3/3`；运行时代码、测试和资产残留扫描未发现 `DurationSeconds`、`ElapsedSeconds` 或 `m_durationSeconds`，普通行动目录仅 `TabletopCardActionSystem` 在即时换算分支读取一次 `Time.deltaTime`；`.spec` lint 通过。
- 4.6 阶段的 PlayMode 退出日志保留了修复前 68 笔 Persistent 分配的历史结论；后续已由 EX-GAS 正式 `Shutdown()` 修复，当前结论与验证路径见 `gamecore-gas.md`。
- 本节不把上述测试越权解释为世界回合 owner、日结、GAS 回合同步、存档、联机、结果结算、工位归属或 Mod 自定义推进已完成。

#### 4.7 牌桌卡牌结果切片（2026-08-06）

当前吸收 StackCraft 已证明且本项目已有真实状态 owner 的卡牌结果：保留参与卡牌、整张移除、使用一次并在耗尽时移除，以及在参与槽位位置生成指定内容 ID 的卡牌。`RecipeDefinition.Execute()`、特殊 Recipe 子类、直接访问 Manager、直接切场景、库存入包、蓝图发现、随机池和 GameplayEffect 均未吸收。

- `ActionDefinition.ResultIntents` 是 SO 作者源中的可序列化参数，不执行副作用；当前内置 `RemoveCardsResultIntent`、`UseCardsResultIntent` 与 `CreateCardsResultIntent`。保留不需要额外意图；没有结果类型枚举、结果标签或万能参数字典。
- `ActionResultSettlement` 先冻结并校验完整计划：空意图、未知类型、槽位、失效卡牌、重复修改、剩余次数、产物内容 ID、数量、牌桌 ID 容量与最终空间占用全部通过后，才由唯一 `Tabletop` 提交。最后一次使用直接走正式移除链，不留下零次数卡牌。
- `CardDefinition.InitialUses` 是每个新卡牌实例的作者初值，默认 `1` 表示一次性材料；`TabletopCard.RemainingUses` 是唯一运行事实。旅行保留同一卡牌对象；牌桌快照保存剩余次数，活动行动快照保存开始时冻结的使用目标。剩余次数的正式卡面 / 详情表现不照搬旧 UI 结构；若模板剩余次数表面承载玩家效果，仍需进入表面 / 动画审计。
- 零回合行动在创建作业后立即提交；有耗时行动只在进度完成时提交。候选是输入查询快照，结果行动开始前必须确认绑定卡仍属于当前牌桌，过期快照直接要求重新查询，不创建影子占用表。
- Mod 目前不能仅靠继承结果意图就获得执行能力；未知类型会明确报错。只有正式 Mod API、注册权限和唯一结算 owner 确定后，才开放新增结果类型，文档不冒充当前已经支持。
- 定向 EditMode `Logs/TestResults-GamePlay-4.7-EditMode-Final.xml` 为 `5/5`；统一场景 PlayMode `Logs/TestResults-GamePlay-4.7-AllPlayMode-Final.xml` 为 `6/6`，已验证新框架真实产物结果、牌桌状态、视图刷新和过期候选拒绝。
- 最终全量回归：`Logs/TestResults-GamePlay-4.7-AllEditMode-Final.xml` 为 335 通过、1 条条件不适用跳过、0 失败；`Logs/TestResults-GamePlay-4.7-AllPlayMode-Final.xml` 为 `6/6`，带栈 Native 泄漏检测正常退出且无泄漏报告，日志见 `Logs/GamePlay-4.7-AllEditMode-Final.log` 和 `Logs/GamePlay-4.7-AllPlayMode-Final.log`。

4.7 尚未完成库存、蓝图、地图和 EX-GAS 结果接入；这些职责仍按真实 owner 逐项裁决，不因“未来可能需要”创建空结果系统。

地图旅行已在进入 4.8 前单独裁决为延后：模板把固定场景名、保存、淡入淡出、同步结果完成、异步场景切换和参与卡跨场景复制绑定在一起；当前 `MapSystem` 的正式输入仍是资源场景地址而非地图内容 ID，行动结算也没有能等待场景切换成功的异步事务，参与角色/牌桌状态跨地区保留规则尚未成立。直接新增旅行结果会重新引入固定场景名或让作业先显示完成，因此本轮没有修改地图代码。

#### 4.8 权威随机与结果分支（2026-08-06）

- StackCraft 的真实需求是“同一条件可能产生不同结果”，不是让系统在多个完整行动候选中随机替玩家做选择。GamePlay 保留玩家对行动的显式选择，只在行动内部选择结果分支。
- `ActionDefinition` 同时允许共同结果意图和加权结果分支：共同结果每次执行，随机分支只执行一个。分支键只在所属行动内稳定，用于作业记录、结算、回放和结果说明，不是内容身份，也不进入全局内容索引。
- 权重使用大于零的整数相对权重。空分支、空键、重复键和零/负权重在行动开始时直接报错；不保留 StackCraft 总权重无效后随机选一个、浮点误差后取最后一个等兜底分支。
- `TabletopCardActionSystem` 由单局权威 owner 一次性注入非零种子，内部使用项目已安装的 Unity.Mathematics xor-shift `Random`。随机分支在 `StartAction` 时选择并写入 `TabletopCardActionJob.ResultBranchKey`，因此完成顺序、即时/回合切换和 UI 帧率不会重新决定正式结果。
- 结果结算继续由 `TabletopCardActionResultSettlement` 建立共同结果与已选分支的完整计划，再由唯一 `TabletopCards` 原子提交。SO、UI、候选解析器和结果意图都不能直接调用随机或修改牌桌。
- 当前没有新增全局随机 Manager、RuntimeContext、随机事件包装、网络 DTO 或 Mod 随机接口。单局种子来源、随机流快照、服务器同步、公开/隐藏随机可见性和断线恢复等到 4.11 根据真实运行状态收口。
- 定向 EditMode `Logs/TestResults-GamePlay-4.8-EditMode-First.xml` 为 `8/8`；统一测试场景使用固定种子和两条 `1:3` 产物数量分支。最终 `Logs/TestResults-GamePlay-4.8-AllEditMode-Final-R2.xml` 为 338 通过、1 条条件不适用跳过、0 失败，`Logs/TestResults-GamePlay-4.8-AllPlayMode-Final.xml` 为 `6/6`；对应日志均未出现 `Leak Detected` 或未释放原生集合。

#### 4.9 连续执行与中断策略（2026-08-06）

- StackCraft 在拖出卡牌时暂停任务，放回原堆时恢复，剩余卡牌仍满足配方时继续，否则取消；完成后若 `isContinuous` 为真或仍有可消费材料，则重新全局扫描配方并自动开始下一任务。
- GamePlay 只吸收“运行中的参与条件变化必须中断”这一职责，不吸收模板的卡堆位置触发、全局配方扫描和自动续作。当前牌桌卡牌位置不是正式工位归属，不能把拖离某个堆等价成离开工位。
- `TabletopCardActionSystem` 现在要求所有牌桌作业绑定当前牌桌状态和内容索引。开始作业时复核候选作者源、槽位顺序、数量、卡牌内容与动态标签；每次回合或即时推进前再次复核。参与卡缺失或不再匹配时，作业在增加进度和提交结果之前取消。
- `ActionCancellationReason` 区分显式请求、参与者失效和剧本结束。作者错误、未绑定状态、过期请求或非法状态迁移继续抛出异常，不通过取消原因吞掉内部问题。
- 纯物品/静态标签行动使用 `BindTabletopActionState`，需要角色 GAS 动态标签的行动使用 `BindTabletopActionStateWithAbilitySystem`；测试和非角色模块不因可选参数被迫引用 GAS。
- 自动重复没有正式入口。完成或取消作业离开活动集合；玩家、AI、剧本或未来工位计划若要重复，必须重新获取当前可用行动集合、重新查询候选并调用所属 `ScenarioRun.StartAction`，旧进度和旧随机分支不继承。
- 最新策划的“多人缩短耗时、成功判定取最高等级”只记录为后续工位/属性规则需求；4.9 没有用参与卡数量直接改 `TurnCost`，也没有创建倍率表、策略接口或第二进度字段。
- 定向 EditMode `Logs/TestResults-GamePlay-4.9-EditMode-R3.xml` 为 `34/34`；统一测试场景 PlayMode `Logs/TestResults-GamePlay-4.9-PlayMode-R3.xml` 为 `3/3`，覆盖参与卡移除后取消、零进度、无产物和视图刷新。
- 最终全量回归：`Logs/TestResults-GamePlay-4.9-AllEditMode-Final.xml` 共 `341` 条，其中 `340` 通过、`1` 条条件不适用跳过、`0` 失败；`Logs/TestResults-GamePlay-4.9-AllPlayMode-Final.xml` 为 `7/7`。两次运行均启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，对应日志未出现 `Leak Detected` 或未释放原生集合。

#### 4.10 发现 / 蓝图边界与作者源校验（2026-08-06）

- StackCraft 把“已发现卡牌 / 已发现配方”保存在 `CardManager` 与 `CraftingManager` 的集合中；`ResearchRecipe` 与 `PackSlot` 可以从未发现配方池里随机抽取并写入发现集合；`RecipesView` 只显示已发现配方；`RecipeDefinitionEditor` 对完全相同材料的配方显示冲突、权重和跳转按钮。
- Gameplay 只吸收其中两个地基职责：一是“当前局内发现状态会过滤可展示 / 可选择的行动”，二是“作者源进入运行时索引前应发现引用断裂和同条件歧义”。研究随机、配方卡生成、蓝图 UI、RecipesView、配方存档和创意工坊 / Mod API 均不在本步实现。
- 该切片最初用独立 `ContentDiscoveryState` 与 `ActionDiscoveryFilter` 验证发现门槛。2026-08-09 回审确认二者没有单局 owner，已经删除；当前由 `ScenarioRun` 直接持有已发现 `ContentId`，发现未知或无效 ID 直接抛错，不创建占位记录。
- `FoundationTestSceneHarness` 现在只通过当前 `ScenarioRun` 标记测试行动并查询候选；统一测试场景因此证明行动候选链路消费的是所属单局发现事实，但不代表正式蓝图、研究或 UI 列表已经完成。
- `ContentValidator` 现在校验行动槽位键、槽位数量范围、允许内容 ID、内容 / 动态 GAS 标签码、结果意图引用的槽位、产物内容、产物数量、随机分支键和权重。未知产物和非法随机权重从“作业开始后失败”前移为“内容索引构建前失败”，符合作者源错误早暴露原则。
- 同参与条件的多个行动直接作为独立行动选项进入索引；不再生成 `ACTION_CONDITION_SIGNATURE_SHARED`，也不把玩家选择错误地合并为随机结果。该警告及其字符串签名计算已从正式代码删除。
- 定向发现 / 校验 EditMode `Logs/TestResults-GamePlay-4.10-EditMode-First.xml` 为 `4/4`；GamePlay EditMode `Logs/TestResults-GamePlay-4.10-EditMode-R3.xml` 为 `38/38`；牌桌 PlayMode `Logs/TestResults-GamePlay-4.10-PlayMode-R1.xml` 为 `3/3`。
- 最终全量回归：首次全量 EditMode `Logs/TestResults-GamePlay-4.10-AllEditMode-Final.xml` 因 UnitySkills 自测在资产仍更新时先返回“正在编译或更新资产”而非时长范围错误，失败 `2` 条；资产稳定后重跑 `Logs/TestResults-GamePlay-4.10-AllEditMode-Final-R2.xml` 为 `344` 通过、`1` 条条件不适用跳过、`0` 失败。`Logs/TestResults-GamePlay-4.10-AllPlayMode-Final.xml` 为 `7/7`。两次最终通过运行均启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，日志未出现 `Leak Detected` 或未释放原生集合。

#### 4.11 请求复核、活动作业快照与统一场景收口（2026-08-07）

- StackCraft 的 `CraftingData` 只保存配方 ID 和秒数进度，并依赖当前卡堆重建任务；它没有玩家席位、联机授权、Mod 依赖、可见性或服务器复核边界。Gameplay 不吸收该存档形状，也不为了未来可能性提前建立完整存档文件、网络 DTO、重连协议或 Mod API。
- `ActionRequest` 是已确认行动计划形成的短暂提交命令，只携带行动唯一内容 ID、行动内槽位键和当前局内卡牌 ID，不持有 `ScriptableObject`、候选对象、牌桌对象或运行系统引用，也不冒充存档 / 网络 DTO。`ScenarioRun.StartAction(request)` 是唯一公开启动入口；单局复核发现权限，内部牌桌从当前 `ContentIndex` 和牌桌卡牌重建绑定并一次性复核数量、内容与 GAS 动态标签。
- 未知行动、重复 / 未知 / 缺失槽位、无效或重复卡牌、参与数量不合法、卡牌已经移除、内容或动态标签不再满足等请求都会在创建作业前明确拒绝，不建立影子占用表、旧候选缓存或自动修正分支。内部正式代码和全部测试都不再调用公开的候选启动重载，该入口已经删除。
- `ActionInstanceSnapshot` 只保存当前活动行动已有的行动 ID、回合消耗、已推进回合、运行 / 暂停状态、已选结果分支、冻结结果计划和槽位卡牌绑定。取消与完成行动不会进入活动快照，因此不保存取消原因。快照不保存文件，也不拥有随机流、完整牌桌、发现状态、玩家控制权或 Mod 包依赖；这些必须由后续 `ScenarioRun` 存档边界统一设计。
- `FoundationTestSceneHarness` 在玩家显式选择行动后先把候选转换为请求，再通过唯一请求入口启动作业。统一测试场景因此证明本地 UI 候选也不能绕过权威复核，但不代表当前已经实现服务器、网络序列化或断线恢复。
- 本步新增的测试属于实现后的公开契约 / 回归保护，不倒称为严格 TDD：覆盖过期请求拒绝、请求重新构造候选、重复卡牌绑定拒绝和活动作业快照事实。
- 定向 GamePlay EditMode `Logs/TestResults-GamePlay-4.11-EditMode-Final.xml` 为 `42/42`；最终全量 PlayMode 中 `TabletopCardFoundationPlayModeTests` 的统一测试场景 `3/3` 通过。
- 最终全量回归：`Logs/TestResults-GamePlay-4.11-AllEditMode-Final.xml` 共 `349` 条，其中 `348` 通过、`1` 条条件不适用跳过、`0` 失败；`Logs/TestResults-GamePlay-4.11-AllPlayMode-Final.xml` 为 `7/7`。两次运行均启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，日志未出现 `Leak Detected` 或未释放原生集合。

#### 4.12 真实拖拽后的行动选择 UI（2026-08-10）

- **参考与裁决**：StackCraft 的 `CraftingManager` 会在放置后自动匹配并启动制作；自动启动已明确排除。Gameplay 需要保留“拖拽产生候选、玩家显式选择、再由权威入口复核”的能力，因此把候选显示为 UIKit 面板，而不是恢复模板的自动制作链。
- **正式链路**：测试场景的 `FoundationTestSceneHarness` 只消费一次牌桌释放意图。命中目标卡牌时把当前可用行动 ID 交给所属 `ScenarioRun`；单局从本局内容索引解析正式定义并查询候选，候选非空才通过既有 UIKit 打开 `TabletopActionChoicePanel`。面板只保存本次候选、单局引用和屏幕锚点；点击候选时将 `ActionCandidate` 转为 `ActionRequest`，再调用 `ScenarioRun.StartAction`。单局复核发现权限后才交给内部牌桌创建实例，面板不保存活动行动、规则结果或第二份输入状态。
- **输入与 UI 边界**：面板临时压入既有 `GameStateSystem` 的 UI 层，正式 `GameCore.InputSystem` 把 UIKit 创建的唯一 `InputSystemUIInputModule` 绑定回同一份 `PlayerInput.actions`；面板关闭后移除自己压入的层，输入恢复为 Gameplay。没有新增输入资产、事件总线、UI 管理器或行动管理器。
- **验证范围**：`FoundationTabletop_PlayerConfirmsActionThroughUIKitAndRestoresGameplayInput` 用真实鼠标拖拽、物理卡牌命中和 UIKit 按钮点击验证“拖拽 -> 面板 -> 选择 -> 唯一行动入口 -> 输入恢复”。2026-08-10 新鲜运行 `FoundationTestScenePlayModeTests` 为 `9/9` 通过，内容索引 PlayMode 为 `3/3` 通过；面板截图 `Library/GameplayVisualEvidence/foundation-action-choice.png` 已通过图面核验。当前场景只配置一项测试行动，所以已验证单候选的完整交互闭环；多个候选的作者数据和交互内容要等真实剧本出现后再扩展，不用测试专用业务凑数量。
- **未扩展范围**：这不是正式角色详情、侧边快捷按钮、本地化皮肤、填槽弹窗、玩家授权或联网请求协议；它只证明新框架能够从真实拖拽路径显式选择行动，且没有回退到 StackCraft 自动制作。

### 5. 剧本目标 / 世界规则 / 事件日结

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `QuestManager` 监听获得、发现、击败、制作、买卖、装备、探索、时间、天数等事件；`EncounterManager` 按日期、优先级、概率、一次性和卡牌数量限制触发遭遇；`DayCycleManager` 把一天结束拆成通知、喂食、卖超额卡、遭遇、新一天。 |
| Gameplay 职责归属 | `ScenarioRun` 拥有世界回合、派生日期、任务日志和内容发现；任务由 `QuestLog` 承载。未来跨日规则必须先按真实作者源和运行生命周期决定归属，不能先建世界规则总管。 |
| 裁决 | **按职责切片吸收。** 已吸收任务生命周期、事实消费和跨日日期事实；具体 Quest 枚举、Encounter 单例和固定日结流程不吸收。 |
| 保留范围 | 目标激活 / 完成 / 解锁链，以及回合跨过日界后消费者读取已提交日期的效果。遭遇筛选、一次性记录、输入锁和关键选择只有出现真实剧本规则后再裁决。 |
| 重构方向 | 当前不预建日程 pipeline。首个真实跨日规则出现后，再判断它能否直接消费回合事实；只有确实需要暂停、等待玩家选择或异步串行结算时，才由 `ScenarioRun` 拥有可中断的日程运行对象。 |
| 排除范围 | 不吸收固定 `QuestType`；不吸收英文 modal 文案；不把 `Title/Main/Island` 或 Build Settings 场景列表当正式剧本入口。 |

#### 5.1 世界回合事实与确认（2026-08-07）

- **参考实现覆盖范围**：StackCraft `TimeManager` 同时持有实时秒数、时间倍率、当前天数和日开始 / 日结束事件；`DayCycleManager` 订阅日结束并固定执行通知、喂食、卖卡、遭遇、新一天五个阶段。这个参考证明了世界流程需要一个可被其它系统消费的时间事实，但它把时间、日结、UI、输入锁、卡牌副作用和存档绑在了多个单例里。
- **吸收**：当前 `ScenarioRun` 唯一持有已确认世界回合编号。回合制由 `ScenarioDirector.ConfirmTurn()` 进入，确认后递增并直接通过 YokiFrame `EventKit.Type` 发布 `ScenarioTurnConfirmedEvent`；即时制按本剧本 `SecondsPerTurn` 自动跨越同一世界回合边界。
- **删除 / 不保留**：不复制 `TimeManager`、`DayCycleManager` 的单例、第二实时秒数、`Time.timeScale` 写入、独立当前天数、固定五阶段、输入锁、通知弹窗、喂食、卖卡、遭遇执行、自动保存和 StackCraft 日结事件。
- **现有职责重构**：历史 `TabletopCardActionSystem` 已删除。当前 `ScenarioRun` 是回合确认和模式选择入口；回合制时直接推进所属牌桌，即时制时按本剧本 `SecondsPerTurn` 换算同一份行动进度。牌桌不持有世界当前回合数，也不代行日结。
- **系统装配**：历史 `ScenarioTurnSystem` 与 `TabletopCardActionSystem` 均已删除。`ScenarioDirector` 只编排活动 `ScenarioRun`；单局直接拥有唯一牌桌和世界回合，牌桌不保存第二回合编号。
- **事件边界**：`ScenarioTurnConfirmedEvent` 是领域事实载荷，直接使用 `EventKit.Type`，不是 `GameRuntimeEvents`、不是新的事件总线，也不是包装转发层；本事件不记录 UI、回放、联机或存档数据。
- **验收**：2026-08-11 回审后，单局时间线 `9/9`，Foundation `13/13`，全量 EditMode `431/432`、`0` 失败、`1` 条既有忽略，全量 PlayMode `30/30`。即时模式覆盖半回合、连续三回合、日期变化、事件顺序和拒绝手动双重推进。
- **下一步边界**：5.1 只建立统一世界回合时间线。5.2 继续裁决日期派生与日程边界，天气、饥饿、危机和胜负条件不能追加成 `ScenarioDirector` 固定流程。

#### 当前 5.2 天数派生与日程阶段边界（2026-08-11）

- **日期唯一真相**：`ScenarioRun` 只保存总确认回合，当前日与当日进度由 `TurnsPerDay` 推导；回合制和即时制跨越同一日界，不保存第二份当前日期。
- **提交顺序**：跨日回合先推进牌桌，再把新日期事实交给所属 `QuestLog`，最后发布已有 `ScenarioTurnConfirmedEvent`。订阅者读取到新日期时，按天任务已经完成提交。
- **真实消费者**：当前只有任务日志和 HUD。已有回合事实已经携带当前日与日内进度，不需要再造 `DayStartedEvent`、`DayEndedEvent` 或事件包装。
- **历史边界订正**：这一切片当时只证明日期派生，不等于完整复现 StackCraft 日程。模板已经提供日终通知、进食、超限处理、最多一个遭遇、新日确认和自动保存的真实顺序，因此这些玩家效果必须先用新框架复现并试玩，不能再因 CardLoop 临时策划尚未定案而排除。
- **实现约束**：不恢复模板的并列 Manager / 单例链，也不把模板数值写死成最终产品规则；日终效果作为可配置测试内容进入 `ScenarioRun` 的同一跨日生命周期，自动保存复用现有 `ScenarioDirector` 与 `SaveSystem`。
- **代码结论**：既有验证只覆盖日期派生与回合事实，不能证明完整日终已完成。原证据 `Logs/TestResults-Gameplay-Module52-DayBoundary-R2.xml` 与 `Logs/TestResults-Gameplay-Module52-Foundation-PlayMode-R1.xml` 保留其直接覆盖范围。

#### 当前 5.3 任务日志与内容发现事实（2026-08-11）

- **对象模型订正**：StackCraft 有可查询的 `QuestInstance`，2DRPGEngine 有 `QuestProgress -> IQuestTaskProgress`。Gameplay 现在以 `QuestProgress` 表达单个任务运行对象，拥有定义、状态和只读子项运行状态；`QuestLog` 只负责本局集合、前置解锁和事实分发。
- **删除第二写入口**：旧 `QuestLog.CompleteQuest(ContentId)` 可被程序集内调用方绕过任务子项直接改完成状态，现已收为只接受已完成 `QuestProgress` 的私有提交步骤。正式完成只能来自活动子项消费已提交事实。
- **进度读取**：`QuestTaskProgressSnapshot` 只保存当前值与目标值，完成状态由数值推导。`QuestProgress.Tasks` 提供只读任务子项，后续 UI 不需要复制进度状态。
- **状态刷新**：旧代码只用 `SynchronizeDiscoveredContentWithQuestLog` 反复回放发现集合，导致后置解锁的发现任务立即完成、同语义的按天任务却延迟到下一天。现改为 `ScenarioRun.RefreshQuestState` 统一刷新当前日期和已发现内容；行动完成事实仍只提交一次，不会重复累计。
- **事务顺序**：同一事实先让当时已激活的任务子项更新，再一次性提交所有完成任务与满足前置的激活任务，最后发布 `QuestStatusChangedEvent`。订阅者读取到的是本次事实已提交后的任务集合。
- **不吸收**：不恢复 `QuestType`、多个 Manager 订阅、任务中央类型工厂、任意事实总线、任务存档、任务 UI 或尚无正式来源的获得 / 击败 / 交易任务。
- **验证**：RED `Logs/TestResults-Gameplay-Module53-StateRefresh-RED-R2.xml` 为 `0/1`，精确命中后置日期任务仍活动；最终任务 / 单局定向 `Logs/TestResults-Gameplay-Module53-QuestAggregate-GREEN-R3.xml` 为 `17/17`。同轮 Foundation `13/13`、全量 EditMode `432/433`（1 条既有忽略）、全量 PlayMode `30/30`，均零失败。

#### 当前 5.4 场景旅行（2026-08-11）

- **参考实现覆盖范围**：StackCraft `GameDirector.TravelSequence` 证明场景切换可以成为剧本流程的一部分，但它用固定场景名和跨场景 traveler 数据副本搬运卡牌。CardLoop 不把场景地址当剧本 ID，也不重建普通快照来替代角色对象。
- **作者源边界**：`ScenarioRegionDefinition` 拥有地区场景地址、地区牌桌规则和默认抵达位置；`ScenarioDefinition` 只引用地区内容 ID 和初始地区。剧本级初始场景字段与剧本级牌桌配置已删除，避免一个 SO 同时承担地区和剧本两层事实。
- **运行时对象模型**：`ScenarioRun` 拥有多个 `ScenarioRegion`，地区长期拥有自己的 `Tabletop`；当前牌桌是当前地区的派生入口。旅行迁移同一个 `TabletopCard` 对象，角色卡的唯一 `AbilitySystemCell` 不变；未旅行卡牌继续留在来源地区。
- **唯一场景入口**：`ScenarioDirector.TravelAsync` 完整校验后调用 `GameCore.SceneSystem.TransitionToAsync`。加载成功回调才提交地区和卡牌迁移，失败则清除待定旅行；没有新增 `SceneManager` 业务入口、固定场景名列表或 traveler 数据副本。
- **异步事务对象**：`ScenarioTravelPlan` 冻结一次已校验的来源、目标、旅行者和落点，作为场景异步切换后的提交凭据；它不保存作者内容、不复制牌桌状态，也不是存档对象。原本没有消费者的 `ScenarioRegionChangedEvent` 已删除，地区切换不再凭空广播。
- **时间边界**：所有已创建地区共享 `ScenarioRun` 的世界回合与即时换算；地区不拥有自己的时间线。
- **验收**：定向旅行 `1/1`、Foundation `13/13`、全量 PlayMode `30/30`、全量 EditMode `433/434`（0 失败、1 条既有条件忽略）。证据分别为 `Logs/TestResults-Gameplay-Module54-Travel-PlayMode-R2.xml`、`Logs/TestResults-Gameplay-Module54-Foundation-PlayMode-R1.xml`、`Logs/TestResults-Gameplay-Module54-PlayMode-R1.xml`、`Logs/TestResults-Gameplay-Module54-EditMode-R2.xml`。

> 下方 5.2-5.9 是 2026-08-08 至 2026-08-10 的历史实施编号；当前重排后，这些任务与发现能力已经统一完成现行 5.3 回审，不表示当前 5.2 或 5.3 需要重复实现。

#### 5.2 任务定义与生命周期（2026-08-08 已验收）

- **参考实现覆盖范围**：StackCraft `Quest` / `QuestInstance` / `QuestManager` 提供锁定、激活、进度、完成和后继解锁；FantasyWord 已有 `JournalSystem -> Quest -> QuestTask -> QuestTaskProgress`，证明玩家可见任务应是父级，子目标 / 条件只是任务内部结构。
- **原方案订正**：此前新增 `ObjectiveDefinition / ObjectiveSystem` 属于抽象错位，把 Quest 父级降成了目标集合。该路线已撤销，旧验证日志只保留为历史证据，不再证明当前设计有效。
- **吸收**：新增 `QuestDefinition`，复用唯一内容 ID、展示信息和 EX-GAS 标签，声明单向 `PrerequisiteQuestIds` 与内部 `QuestTaskDefinition[]`；新增 `QuestSystem`，持有当前单局任务集合的 `Locked / Active / Completed` 状态和任务子项运行进度。
- **删除 / 不保留**：不保留 `QuestType`、目标卡牌 / 配方固定字段、`QuestsToUnlock`、活动 / 完成双列表、跨 Manager 事件订阅、即时状态回写 SO、存档字段和任务 UI。后继关系只从前置任务 ID 派生，避免作者双重更新。
- **作者源校验**：统一内容校验器在索引建立前拒绝无效、未知、错类型、重复、自引用和循环前置任务；选入本局的任务集合若缺少某个前置任务，也会在提交任何运行状态前直接报错。
- **运行时边界**：剧本 / 关卡未来负责选择本局任务集合；任务系统只接收这组唯一内容 ID 和正式内容索引，不扫描所有内容自动开任务。`CompleteQuest()` 只接收后续任务子项或剧本流程已经确认的完成事实，不解释获得、制作、击败、时间、数值或胜负规则。
- **联机 / Mod 校准**：SO 只保存不可变作者定义，局内状态只在单局任务系统中存在；未来服务器或房主权威只需同步任务集合、任务状态和任务子项进度，不需要同步 Unity 对象引用。Mod 可以新增任务定义和任务图，但新任务子项类型仍要等正式任务 API，当前不冒充已经开放。
- **验收**：本轮 Quest 重构后的有效证据为 `Logs/TestResults-Gameplay-Quest-EditMode-R4.xml`，Gameplay EditMode `53/53` 通过；旧 Objective 相关 RED / GREEN / 全量日志不再作为当前有效验收证据。

#### 5.3 任务状态变化事实（2026-08-08 已验收）

- **参考实现覆盖范围**：StackCraft `QuestManager` 分别通过 `OnQuestActivated` 与 `OnQuestCompleted` 通知 `QuestsView` 和 `TradeManager`，但事件归 Manager 私有回调列表所有，消费者继续依赖模板单例和 `QuestInstance` 运行对象。
- **吸收**：新增 `QuestStatusChangedEvent`，统一表达任务唯一内容 ID、变化前状态和变化后状态；`QuestSystem` 完成状态提交后直接通过 YokiFrame `EventKit.Type` 发布，不新增事件总线、转发器或回调注册表。
- **事务边界**：开始任务集合时，全部任务和根任务状态先提交，再发布根任务激活事实；完成任务时，完成状态与满足前置的后继激活状态先全部提交，再按“完成原因在前、解锁结果在后”的因果顺序发布。同步订阅者查询任务系统时不会读到中间态。
- **删除 / 不保留**：不保留两套激活 / 完成 C# 事件、`QuestInstance` 对象载荷、Manager 单例订阅、任务进度事件、UI 更新、交易解锁、存档或网络消息。事件没有第二状态副本，也不允许订阅者回写任务状态。
- **任务条件延后理由**：模板的进度监听写死卡牌、制作、交易、时间与 `QuestType`；当前正式领域尚未提供这些完整事实，立即建立通用条件注册表只会猜测接口。5.3 因此只吸收已有稳定状态事实，不冒充所有任务条件或进度已经完成。

#### 5.4 剧本父级与任务组合生命周期（2026-08-08 已验收）

- **参考实现覆盖范围**：StackCraft 没有正式剧本聚合根。`GameDirector` 以场景名代表当前进度，`QuestManager` 和 `EncounterManager` 分别通过单例持有自己的运行状态，`DayCycleManager` 再直接串起遭遇阶段。这是小型单剧本模板的组织方式，不适合多世界、Mod、联机和关卡编辑器。
- **原方案订正**：此前直接从 `EncounterManager` 抽出 `EncounterDefinition` 与 `EncounterSystem`，等于在父级尚未成立时复制了来源工程的目录边界。该实现、测试和“5.4 遭遇候选已完成”的知识记录均已删除。
- **吸收**：新增最小 `ScenarioDefinition`，当前只组合已实现的任务 ID；新增 `ScenarioDirector`，只持有活动剧本身份，并统一开始 / 结束 `QuestSystem` 的任务集合。这个父级边界吸收的是模板 `GameDirector` 对单局流程的聚合作用，不复制固定场景名、存档槽、旅行卡牌或单例事件。
- **显式装配**：`ScenarioDirector` 通过序列化引用明确拥有 `QuestSystem`，并声明任务系统为启动依赖；不再使用隐藏全局查询。任务集合开始 / 结束入口收窄为程序集内部，外部只能通过活动剧本改变这组状态。
- **作者源校验**：剧本任务列表拒绝无效、重复、未知、错类型和缺失前置任务。当前不添加地图、事件池、天气、世界规则或初始内容空字段，等待对应职责从参考模块中逐步成立后再组合。
- **删除 / 不保留**：不保留 `EncounterType`、`EncounterDefinition`、`EncounterSystem`、固定日期筛选、优先级、概率、友好模式、卡牌数量限制、一次性记录、通知弹窗、随机坐标、生成卡牌、镜头和粒子执行。它们不是本步已经确认的稳定模块。

#### 5.5 剧本父级接管世界回合生命周期（2026-08-08 已验收）

- **参考实现覆盖范围**：StackCraft `GameDirector` 统一开始新局、读档、旅行和返回标题，`TimeManager` 则单独持有当前天数与时间并向外发布日结束。它证明单局时间事实必须受当前游戏流程约束，但两个单例之间没有正式父子生命周期，时间仍可脱离当前剧本独立存在。
- **现有实现问题**：5.1 的 `ScenarioTurnSystem.ConfirmTurn()` 原本是公开入口，5.4 的 `ScenarioDirector` 只拥有任务集合。即使没有活动剧本，外部也能直接确认世界回合；这不是正常业务失败，而是入口职责没有收口。
- **吸收 / 重构**：`ScenarioDirector` 显式引用并依赖 `QuestSystem` 与 `ScenarioTurnSystem`。开始剧本时统一重置回合编号并开始任务集合，玩家 / UI / 网络命令只能通过 `ScenarioDirector.ConfirmTurn()` 确认当前活动剧本的回合，结束剧本时统一清除任务集合和回合编号。
- **单一真相**：`ScenarioTurnSystem` 只保存 `ConfirmedTurnIndex`，确认和重置入口为程序集内部；它不保存第二份活动剧本布尔值。活动剧本是否存在只由 `ScenarioDirector.ActiveScenarioId` 判断，避免父子模块各维护一套状态。
- **统一测试场景**：新增 `test.foundation.quest` 与 `test.foundation.scenario` 两个 SO 作者资产。`FoundationTestSceneHarness` 从正式内容索引开始测试剧本，PlayMode 通过剧本父级确认回合并驱动既有普通行动；场景生成器同时校验任务和回合两条显式引用。
- **删除 / 不保留**：不添加日期、实时秒数、暂停锁、倍速、日开始 / 结束事件、固定日结阶段、喂食、卖卡、遭遇、通知弹窗或自动保存；不新增 `ScenarioRuntimeContext`、回合包装事件或第二套回合状态。

#### 5.6 行动完成任务子项与任务推进（2026-08-08 已验收）

- **参考实现覆盖范围**：StackCraft `QuestManager` 订阅 `CraftingManager.OnCraftingFinished`、`OnExplorationFinished` 等具体事件，按固定 `QuestType` 修改 `QuestInstance.CurrentAmount` 并在达标后完成任务。它证明“已成立的玩法事实驱动活动任务进度”值得吸收，但固定类型分支和跨 Manager 单例监听不适合本项目。
- **吸收**：普通牌桌行动成功结算后，所属 `ScenarioRun` 先把行动完成事实直接交给自己的 `QuestLog`，任务状态提交后才通过 YokiFrame `EventKit.Type` 对外发布 `ActionCompletedEvent`；首个 `ActionCompletionQuestTaskDefinition` 声明具体行动 ID 与所需次数。任务日志只保存该单局的任务状态和任务子项进度。
- **因果边界**：行动结果先全部提交，再发布完成事实；结算异常、取消和参与者失效都不发布。任务处理时先确定本次事实到达前已激活的匹配任务，再完成任务与解锁后继，因此同一次行动不会同时推进刚解锁的后继任务。
- **分类与标签**：没有新增 `QuestType` 或任务条件枚举。`ActionCompletionQuestTaskDefinition` 的运行时类型表示“如何解释事实”，行动 ID 表示精确引用；若未来真实需求是“任意探索类行动”，应新增使用 EX-GAS 层级查询的具体任务子项解释器，而不是把行动类别重新做成枚举或本地标签系统。
- **作者校验**：无效 / 未知 / 错类型行动引用和非正完成次数在内容索引建立前失败。任务子项通过自身受保护校验入口检查作者数据，不建立中央任务类型注册表；派生任务可以解释已有的正式单局事实，但当前不开放 Mod 包加载、任意事实投递或 Mod 代码执行入口。
- **删除 / 不保留**：不保留模板 `QuestType`、目标卡牌 / 配方 / 时间字段、`QuestInstance` 通用整数、跨 Manager 订阅、进度 C# 事件、完成历史表、UI、存档或网络消息；不新增防重复事件 ID、已处理事件表或兜底夹取次数。
- **统一测试场景**：测试任务配置为“完成一次 `test.foundation.action`”。正式行动成功结算后任务变为完成；参与卡失效导致行动取消时任务保持活动，证明测试场景跑的是新框架同一条事实链，不是直接调用任务完成入口。
- **PlayMode 验收**：`Logs/TestResults-Gameplay-Quest-PlayMode.xml` 为 `6/6`，覆盖内容索引、SceneKit 地图切换、YooAsset 卡牌视图、行动成功推进任务和取消不推进任务。
- **下一步边界**：其它任务事实、复合任务子项、进度读取 / 变化、剧本事件、一次性历史和世界规则阶段仍需逐项裁决；不能把 `QuestManager` 余下分支一次翻译成任务条件大全。

#### 5.7 剧本日历事实与按天数任务（2026-08-10 已验收）

- **参考实现覆盖范围**：StackCraft `TimeManager` 保存当前日并发布日开始 / 结束，`QuestManager` 的 `Day` 分支以该事实推进任务；`DayCycleManager` 随后把喂食、卖卡、遭遇、通知和存档写死为五段流程。参考证明“日期属于单局时间事实，任务可消费已到达日期”，不证明这些功能应拆成三个单例。
- **吸收**：`ScenarioDefinition` 作者源新增每个游戏日的确认回合数，默认 `2`；`ScenarioRun` 继续只保存总确认回合，当前游戏日和当日已确认回合都由总回合与剧本配置推导。初始任务激活后和每次跨日后，单局直接把已到达天数交给所属 `QuestLog`；首个 `DayReachedQuestTaskDefinition` 以明确的目标天数完成任务。
- **因果与校验**：每日确认回合数和任务目标天数必须大于 `0`，内容索引建立前直接拒绝。回合确认先推进牌桌行动，跨日后提交任务状态，最后发布已有的 `ScenarioTurnConfirmedEvent`；事件订阅者不会观察到“第二天已到但任务仍未更新”的中间状态。
- **删除 / 不保留**：不新增 `TimeManager`、`DayCycleManager`、实时秒数、倍速、`Time.timeScale`、日开始 / 结束事件包装、固定日结阶段、输入锁、喂食、卖卡、遭遇、通知或自动保存。日期不额外保存成第二状态，完整单局存档时再随总确认回合统一快照。
- **验证**：跨日并完成按天任务的定向 EditMode `1/1` 通过；`ScenarioRunEditModeTests` `6/6` 通过；统一 `FoundationTestScenePlayModeTests` `7/7` 通过；本轮此前全量 EditMode 为 `388` 通过、`1` 条条件跳过、`0` 失败。覆盖当前日历事实和任务推进，不代表日结规则管线、遭遇、天气、饥饿、存档或联机同步已经实现。

#### 5.8 内容发现任务与已知事实回放（2026-08-10 已验收）

- **参考实现覆盖范围**：StackCraft `QuestManager` 的 `Discover` 分支消费配方发现事实；新任务激活时还会读取已发现配方集合立即核对，避免任务解锁顺序让既有发现永久失效。它的实现把配方集合和任务状态分散在单例中，不能整体照搬。
- **吸收**：`ScenarioRun` 已有的发现内容集合继续是唯一发现事实。`ContentDiscoveryQuestTaskDefinition` 只声明目标内容唯一 ID；单局发现新内容、初始化任务或其它事实解锁任务后，按内容 ID 稳定顺序把已发现内容交给所属 `QuestLog`。任务日志只保存该任务是否完成，不复制内容发现集合。
- **因果与校验**：目标内容必须是当前内容索引中存在的正式内容；首次发现才触发任务同步，重复发现不制造新事实。同步会收敛新解锁的发现任务，但不把行动完成等瞬时事实重放给新任务，因此保持 5.6 的因果边界。
- **删除 / 不保留**：不新增 `QuestType.Discover` 枚举、全局发现事件、发现事件去重表、任务侧发现缓存、配方专用字段或通用条件注册表。未来蓝图、地点或其它可发现内容只需使用同一内容身份与单局发现入口。
- **验证**：先发现内容、再由首日任务解锁发现任务的定向 EditMode `1/1` 通过；`ScenarioRunEditModeTests` `7/7`、`ContentReferenceAuthoringEditModeTests` `3/3`、`ActionDiscoveryAndValidationEditModeTests` `4/4` 和统一 `FoundationTestScenePlayModeTests` `7/7` 全部通过。覆盖发现任务与既有行动发现过滤共存，不代表完整研究、蓝图 UI、图鉴、事件池或 Mod 任务 API 已经实现。

#### 5.9 任务子项多态运行状态（2026-08-10 已验收）

- **问题与裁决**：旧任务日志按具体任务子项类型创建运行进度。每加一种任务，核心任务日志就必须增加类型分支；这会让未来内容或代码 Mod 修改本体。该中央工厂已删除。
- **吸收 / 重构**：`QuestTaskDefinition` 自己通过受保护入口创建对应的 `QuestTaskRuntimeState`；`QuestLog` 只创建任务定义声明的状态，并把所属 `ScenarioRun` 已提交的 `QuestTaskFact` 依次交给活动任务解释。行动、日期和内容发现三种内置任务各自保存并解释自己的进度，不再由任务日志判断任务具体类型。
- **Mod 边界**：未来代码 Mod 可以派生任务定义和运行状态，解释已有的行动完成、到达日期或内容发现事实，而不修改 `QuestLog`。这不是“任意 Mod 可发送事实”的 API：新事实只能在出现真实玩法 owner 后由该 owner 写入所属单局；Mod 包加载、权限、联机权威、存档和公开 API 仍未实现。
- **删除 / 不保留**：不保留中央运行状态工厂、按行动 / 日期 / 发现分别暴露的任务日志写入口、任务类型注册表、第二事件总线或任务侧事实缓存。
- **验证**：`QuestLogEditModeTests` `7/7`，其中派生任务消费正式行动完成事实并完成任务；关联 `ScenarioRunEditModeTests` `7/7`、行动发现与校验 `4/4`、内容引用 `3/3`、统一测试场景 `7/7` 均通过。随后 Unity Test Runner 全量 EditMode `390/391` 通过、`0` 失败、`1` 条既有条件跳过；全量 PlayMode `11/11` 通过。以上只验证当前任务切片，不等于已经开放完整 Mod 任务 API。

### 6. StackCraft 战斗 / Stats / 装备 / 职业变化（未来 GAS 边界）

#### 6.0 总裁决：替换实现，保留玩家效果

- StackCraft 的 `CombatTask` 把实时调度、AI 选人、随机目标、命中 / 暴击 / 伤害、三系克制、动画、投射物、音效、镜头、死亡、逃跑、战斗合并和回桌堆栈揉在同一个对象里。这个实现边界不吸收，但其仍属于 CardLoop 产品目标的玩家可见效果必须由正式职责复现。
- “改用 GAS”不等于把整个战斗塞进 GAS。战斗运行时仍需拥有参战者、阵营、实时调度、结束条件和权威随机；EX-GAS 拥有 Ability、Timeline、TargetCatcher、Attribute、GameplayEffect、GameplayTag 和 GameplayCue；角色 / 装备模块拥有职业、装备槽、穿脱和跨世界成长；牌桌表现只投影战斗状态。战斗过程按产品裁决不进入存档，因此不建立战斗快照职责。
- 当前已完成 6.1.1 属性作者源与资源语义，以及 6.1.2 基础 GE 伤害 / 投射物执行边界；其它标记为“待实现 / 待集成”的能力，在统一测试场景复现前都不算第六模块已吸收。

| StackCraft 玩家效果 | StackCraft 实现 | 正式职责归属 | 等效实现路径与当前状态 |
|---|---|---|---|
| 进入战斗、敌我分组、战斗结束 | `CombatManager / CombatTask` 持有两组 `CardInstance` 并直接回桌 | 牌桌聚合 + `Battle` | `Tabletop` 唯一持有活动战斗，`Battle` 直接拥有多个 `BattleSide`，每方只保存本场参战卡牌 ID；角色 GAS 阵营与敌我关系不复制进战斗。活动参战牌不能绕过牌桌移除，只剩一个有成员的战斗方时结束；不建立 `CombatSystem` 空壳。 |
| 实时行动进度和攻速排序 | `CombatTask.Update` 每帧累积 `AttackSpeed` 并选最高者 | `Battle` + EX-GAS Attribute | `Battle` 始终按真实秒数累积角色 ASC 的 `AttackSpeed`，每秒选择进度最高且配置了自动能力的角色；Ability 完成或取消后才重置该角色进度。普通牌桌行动仍独立选择回合制或即时制。**当前效果已验证**。 |
| 普攻、技能、前后摇、取消 | 协程直接播放动画后调用 `ResolveAttack` | EX-GAS Ability + Timeline / AbilityTask | `CharacterCardDefinition` 只引用 ASC 已授予的自动战斗 Ability，不复制技能数据；自动与手动请求都进入现有牌桌 Ability 激活链，前后摇、命中帧、Cost、Cooldown、结束和取消继续由 EX-GAS 承担。**基础攻击已接入**。 |
| 目标选择、随机目标、范围目标 | `CombatTask` 直接从敌方列表 `Random.Range` | `Battle` 权威随机 + Ability ActivationContext / TargetCatcher | 自动行动用战斗权威随机从其它战斗方参与者中选择一个目标，并写入本次 `AbilityActivationContext`；几何范围继续归 TargetCatcher，不使用 Unity 全局随机。**模板随机单目标已接入，复杂 AI / 范围规则按具体 Ability 扩展**。 |
| 命中、闪避、暴击 | `ResolveAttack` 写死公式并直接读 `CombatStats` | GNS/EX-GAS Attribute、Ability、GameplayEffect 与权威随机 | 模板确认存在命中、闪避和暴击玩家效果。当前已按 StackCraft 源码公式接入 `DamageSolver`；数值输入落在 GNS/EX-GAS 属性或效果参数，随机由当前战斗权威随机提供，不恢复 `CombatStats`。 |
| 伤害、防御、护盾、死亡 | `TakeDamage` 直接改 `CurrentHealth` | GameplayEffect + Attribute；牌桌处理角色离场 | 伤害由正式 GE 修改 GAS `Health`，`MaxHealth` 独立保存；自动 Ability 完成后，牌桌移除生命归零的角色卡，只剩一个有效战斗方时结束战斗。旧直接伤害入口和第二效果系统已删除；护盾仍待对应模板效果切片。 |
| 近战 / 远程 / 魔法克制 | `CombatType` 枚举和固定 RPS 分支 | EX-GAS GameplayTag + `FormalDamage.Matchups` | RPS 玩家效果已通过 `Combat.Melee / Combat.Ranged / Combat.Magic` 标签和 GE 内 `FormalDamage.Matchups` 接入：Melee > Ranged、Ranged > Magic、Magic > Melee，优势 `1.5x`，劣势 `0.75x`。旧 `CombatType` 枚举和固定战斗结构不恢复；Mod 动态标签合并仍是已登记缺口。 |
| 近战突进、投射物、命中图标、飘字、音效、镜头 | `CombatTask / CombatManager / HitUI` 直接执行 | Timeline + GameplayCue + 牌桌表现 | Timeline 在命中帧触发 Cue；Cue 只播放动画、Prefab、音频、镜头和 UI，不修改伤害。当前牌桌文本飘字已显示伤害、Miss、暴击、优势和劣势；镜头 shake 已由现有 `CameraShake` 接入纯 ASC 命中表现事件；投射物前摇、战斗音效和 `HitUI` 式图标 / punch 缩放均已完成源码接入，Unity 场景重建与 PlayMode 待编辑器空闲后补跑。 |
| 战斗冲突区和队列布局 | `CombatRect` 同时布局、碰撞、合并和清理 | `Battle` 区域状态 + `ScenarioDefinition` 阵型规则 + `TabletopView` | 战斗参与者按剧本阵型进入阵列；`Battle` 保存唯一权威区域中心，区域尺寸由阵型和参与人数派生，`TabletopBattleAreaView` 只读显示。区域重叠判断不依赖 Physics、Collider 或 UI Transform；模板的堆栈推动和直接状态修改不进入正式链路。**阵型、可见区域与自动合并已验证**。 |
| 逃跑、加入战斗、战斗合并 | `CombatTask.Flee / AddCombatants` 直接改列表和卡堆 | 战斗命令 + 规则判定 + GAS Ability / GE | `Tabletop.JoinBattle` 已支持把角色加入既有战斗的指定战斗方；`LeaveBattle` 已支持主动离开并在仅剩一方时结束战斗；参战卡通过正式拖拽释放入口落到战斗区域外时，会先预演牌桌放置，再离战并回桌放置；新战斗创建前及增援扩张后都会按派生区域重叠自动合并，默认按战斗方索引映射；`MergeBattles` 仍支持调用方为剧情和特殊规则明确映射。目标战斗身份与权威随机流保留，牌桌不根据固定 Player/Mob 或 GAS 标签猜测分组。带属性判定的逃跑仍待正式规则与操作入口。 |
| 装备加属性、标签和技能 | `CardEquipper` 将 `StatModifier` 写入本地 `CombatStats` | `CharacterCard` 装备状态 + `EquipmentCardDefinition` + GameplayEffect；Ability / Tag 等待真实装备语义 | 装备槽位和装备来源已由角色卡直接拥有：装备卡离桌、同槽替换、卸下回桌和快照恢复后重施加持续 GE 已验证；不恢复 `CardEquipper`、`CardEquipment` 或本地 `StatModifier`。装备授予 Ability / Tag 尚无已成立玩家效果，不提前接入。 |
| 武器触发职业变化 | `classChangeResult` 直接替换卡牌定义 | 角色 / 职业系统 + GAS 授予能力 | 职业系统校验转职并持有唯一当前职业，再授予属性集、标签、技能和被动；装备只能发起转职请求。**职业系统尚未设计，当前不得照搬**。 |
| 保存和联机恢复进行中的战斗 | `CombatData` 只保存双方卡牌和战斗区域，读档时重建战斗任务 | 不吸收 | CardLoop 战斗过程不进入存档，不保存参与者、调度进度、激活 Ability、临时效果或随机流。未来联机只按活动会话的真实需求设计权威同步与连接策略，不以战斗读档恢复为前提。**按产品裁决明确排除**。 |

#### 战斗命中 / 闪避 / 暴击 / 飘字源码对照吸收（2026-08-15）

- **StackCraft 源码事实**：`CombatTask.ResolveAttack` 先用 `(Accuracy - Dodge) / 100` 并钳制到 `5% - 95%` 判定命中；Miss 直接返回 `HitResult(Miss, 0)`，不会继续判暴击。命中后用 `CriticalChance / 100` 判暴击；基础伤害是 `Max(1, Attack - Defense)`；RPS 优劣倍率在暴击前生效；暴击最终用 `RoundToInt(damage * CriticalMultiplier / 100)`。
- **CardLoop 规则映射**：攻击力来源不恢复 `CombatStats`，而是 EX-GAS / GNS 的 `FlatDamage + Attack * ScalingFactor` 作为本次命中的攻击力输入；`DamageSolver` 现在按 StackCraft 的减法防御、命中差值钳制、RPS 克制倍率和命中后暴击顺序结算。`Ability 20005 -> Timeline -> GE 2003` 仍是正式执行链。
- **RPS 规则映射**：GE 通过 `FormalDamage.Matchups` 声明来源标签、目标标签、倍率和表现语义；当前内置内容配置 `Melee > Ranged`、`Ranged > Magic`、`Magic > Melee`，优势 `1.5x`、劣势 `0.75x`。规则只查询来源 / 目标唯一 ASC 的 EX-GAS 标签，不在卡牌、战斗状态或本地枚举中复制战斗类型。
- **CardLoop 表现映射**：纯 ASC 角色没有 `CharacterBase` 场景对象时，`GameplayEffectDamageSystem` 在写回 GAS `Health` 后发布 `AbilitySystemDamageResolvedPresentationEvent`；`TabletopView` 只把属于当前牌桌的目标 ASC 投影到对应 `TabletopCardView`；`TabletopCardView` 临时显示 `Miss`、实际伤害数字、暴击颜色，以及 RPS 优势 / 劣势文本和颜色；同一事件在 `ECameraShakeSources.AbilitySystemDamageResolved` 开启时由现有 `CameraShake` 播放命中镜头震动，Miss、静默伤害和 `NoCameraShake` 不触发。
- **明确不恢复项**：`CombatManager`、`CombatStats`、`CombatType` 枚举、独立 `HitUI` 生命周期、DOTween 运行时依赖和旧音频总管不进入正式链路。RPS 固定枚举结构不恢复；玩家效果通过 GNS/EX-GAS、`Battle`、`TabletopView` 和 GameCore `AudioSystem` 吸收。
- **证据口径订正**：本切片的主证据是 `CombatTask.ResolveAttack` / `HitUI.Initialize` 与 `DamageSolver` / `GameplayEffectDamageSystem` / `TabletopView` / `TabletopCardView` 的源码映射。EditMode 测试只防止公式和事件接线漂移；Foundation PlayMode 只算牌桌表现接线 smoke，不能证明模板视觉完全一致。
#### 6.1 子模块顺序

1. EX-GAS 属性作者源、属性身份投影和生命/法力资源语义。
2. 旧 `Stats/EStat` 角色作者层、UI 查询和存档快照迁移到 GAS 属性。
3. 战斗参与者、阵营、生命周期和权威随机边界。
4. 冲突区 / 战斗表现区与牌桌投影。
5. 先确立角色卡与运行时 ASC 的正式归属，再进入战斗能力调用。
6. 实时调度与普攻 Ability 激活。
7. 固定基础伤害的 GAS 结算链；命中 / 闪避 / 暴击已确认属于模板效果候选，后续按 GNS/EX-GAS 数值链复现，StackCraft 数值只作临时参数。
8. GameplayCue 战斗表现。
9. 装备槽位、穿脱、持续 GE 与装备任务事实；Ability / Tag 等待真实装备语义。
10. 职业系统和转职请求。
11. 联机活动会话、隐藏信息和连接策略；不建立战斗存档恢复。

#### 6.1.1 EX-GAS 属性作者源与资源语义（2026-08-08 已完成当前切片）

- StackCraft `CombatStats` 的可见效果要求生命、攻击、防御、攻速、命中、闪避、暴击率和暴击倍率；GameCore 原先又把生命 / 法力的基础值与当前值复用在同一个属性上，形成重算后生命恢复的真实缺陷。
- 属性作者源已通过 EX-GAS 官方属性 / 属性集网页编辑器修改，再由配置工程 `gen.bat`、EX-GAS Attribute / AttributeSet 生成入口产出 JSON 和 `XAttribute` / `XAttrSet`。保留原整数 ID，新增属性只追加 ID，不另建 GameCore 属性码表。
- `FightUnit` 当前包含 `Health`、`MaxHealth`、`Mana`、`MaxMana`、`Stamina`、`MaxStamina`、`MoveSpeed`、`Attack`、`Defense`、`AttackSpeed`、`Accuracy`、`Dodge`、`CriticalChance`、`CriticalMultiplier`。Health / Mana / Stamina 是当前资源，Max* 是独立上限；属性身份由 EX-GAS 生成代码提供，`CharacterAttributes` 只是 GameCore 读取生成身份的投影。
- `CharacterBase` 的当前资源写入改为 GAS 基础值写入并立即调用正式属性重算；已删除 GameCore 侧直接修改 `CAttributeData.CurrentValue` 的 `FormalAbilitySystemAttributeExtensions`。伤害系统在 ECS 效果查询结束后才提交角色伤害，避免查询遍历期间发生结构变化。
- `CharacterSheet` 已删除旧 `Stats` / 等级缩放作者字段，改为保存 `CharacterAttributeOverride[]`。运行时从 EX-GAS `FightUnit` 配置克隆默认值和钳制规则，只应用角色覆盖；只覆盖资源上限时，当前资源初始同步为该上限；覆盖值超出 EX-GAS 表钳制范围会直接报错。编辑器属性选择复用 EX-GAS `GeneralGasChoiceHelper.Attrs`，不复制表格读取逻辑。
- `CharacterBase` 首次初始化 ASC 时直接调用 `CharacterSheet.CreateAttributeSetConfig`，不再先生成旧属性再回写 GAS。无正式写入职责的 `AttributeBootstrapBuffer`、`CreateLegacyStatsProjection` 和 `GetLegacy*AttributeCode` 已删除；ASC 初始化前读取属性直接抛错。
- UI 属性查询读取角色公开的 EX-GAS 属性码：`UIStat`、`UIStatBar` 和 `UICharacterInfo` 的 Inspector 选择复用 EX-GAS `FightUnit` 属性下拉，刷新订阅属性码级基础值 / 当前值变化事件。当前仓库没有 prefab / scene 引用这些 UI 脚本，因此本切片没有真实截图入口。
- 2026-08-10 Unity Test Runner 重新验证：定向 `FormalDamagePipelineEditModeTests` 为 `7/7`，GameCore EditMode 为 `89/89`，Gameplay EditMode 为 `65/65`，Gameplay PlayMode 为 `9/9`，均零失败。旧日志中的 `11/11`、`88/88`、`6/6` 仅保留为当时切片证据，不能覆盖这轮实现后的回归结果。

#### 6.1.2 基础 GE 伤害与投射物执行边界（2026-08-10 已验证）

- **作者源与执行边界**：伤害数值、类型、缩放、击退和条件只来自 EX-GAS GameplayEffect 配置。`GameplayEffectDamagePayload` 与 `DamageDescriptor` 只是 GameCore 程序集内部的不可变转换数据，不提供 Inspector、Mod 或其它模块的直接作者入口。
- **投射物**：发射参数、运行状态和持久化只保存 `impactGameplayEffectId`。碰撞或爆炸时读取正式 GameplayEffect；命中方向以 `MCGameplayEffectImpactOverride` 附在本次效果实例上，不复制伤害规则。
- **删除项**：`GameplayEffectDamageApplier`、`AEffect`、`IEffect` 与绕过 GameplayEffect 的 `HealOrDamagePlayer` 已删除。伤害、治疗和效果不再存在第二条可写执行链。
- **资源入口订正**：旧 `AddOrRemoveMana` 命令也已删除。技能的消耗与恢复由 EX-GAS Ability Cost / GameplayEffect Modifier 表达；角色只在自身复活、升级等生命周期内部调整资源，不能作为内容作者的第二条效果入口。
- **属性恢复前置（订正）**：EX-GAS 2.0.4 当前没有运行时 ASC 快照、属性集枚举或正式恢复门面；Gameplay 不得直接读取 ECS Buffer。角色长期状态恢复必须先在 EX-GAS 正式 OOP 门面建立快照 / 恢复能力，再由角色卡调用；不能把“未来应重算”写成当前已有能力。
- **后续订正**：该缺口已由 6.4 正式链路解决。`ScenarioDirector` 为单局提供非零根种子，`ScenarioRun` 为各地区派生独立牌桌随机流，`Tabletop` 为战斗派生种子，`Battle` 再为每次 Ability 激活提供种子，EX-GAS Timeline 为每个 GameplayEffect 派生种子。牌桌战斗不再依赖 ECS Entity 索引决定正式掷值。旧 2D 场景能力仍保留无权威种子时的实体索引兼容路径，但它不属于牌桌联机链；未来若迁入同一正式战斗入口，必须删除该兼容分支。固定伤害类型的 Mod 扩展裁决仍未完成。

#### 6.1.3 牌桌战斗参与与生命周期边界（2026-08-10 已验证）

- **父级归属**：StackCraft 的 `CombatManager` / `CombatTask` 不进入正式链路。当前单局已经由 `ScenarioRun.Tabletop` 统一拥有卡牌，因此 `Tabletop` 直接拥有活动 `Battle` 集合；没有新增全局 `CombatSystem`、战斗单例、运行时 Context 或第二套卡牌状态。
- **最小真实对象**：`Battle` 直接拥有 `BattleSide` 集合和结束状态，每个战斗方拥有本场参战卡牌 ID；它不复制角色 GAS 阵营、生命、属性、位置、技能、攻击进度或表现状态。
- **唯一写入口**：只有 `Tabletop.StartBattle`、`LeaveBattle`、`EndBattle` 可以改变活动战斗集合。创建会拒绝不存在于当前牌桌的卡牌和重复参战；活动战斗中的参战牌不能经 `RemoveCard` 直接移除。参战者离开后只剩一个阵营或不足两人时，牌桌结束并移除该战斗。
- **存档边界**：旧活动战斗快照已删除。它只能和不含角色 GAS 的普通卡牌快照组合，而普通卡在 6.2 后没有参战资格；该恢复链是假闭环。按后续产品裁决，战斗过程不进入存档，不再重建战斗恢复。
- **GAS 标签边界**：角色所属阵营与关系是 GAS / 剧本规则事实，战斗方只是一次战斗的临时分组。后续开战入口必须从角色 `AbilitySystemCell` 与剧本关系规则解析分组，再提交给 `Tabletop.StartBattle`；战斗不建立本地阵营枚举、字符串表、标签副本或标签查询器。
- **旧阵营枚举的裁决**：`GameCore.EAlignment` 仍被 `CharacterSheet`、`CharacterAlterationRule`、`CharacterBase` 和 `CombatSolver` 共同使用；当前角色 ASC 初始化传入空基础标签，而 EX-GAS 作者表也只有 `Faction.Player` / `Faction.Enemy`，不能等价替换旧的善恶中立。此时新增一个 `FactionTagCode` 字段会制造第二份阵营真相，因此本子模块不迁移。后续必须先由剧本拥有阵营关系规则，并让角色基础阵营和临时变更分别进入 ASC 固有标签与来源可追踪的 GameplayEffect 标签生命周期，再删除旧枚举链。

#### 战斗实时调度补充审计（2026-08-14）

- StackCraft 的 `CombatTask.Update` 为全部参战者累积攻击进度，每秒选择进度最高者并随机攻击对方单位；攻击完成后清零该角色进度。该实现同时写死自动攻击、时间片、随机目标和表现协程，不作为正式结构吸收。
- Gameplay 继续使用已有 `Tabletop.RequestBattleAbilityActivation` 把施法者、目标、Ability 码和独立随机种子交给 EX-GAS，真实伤害由 Ability -> Timeline -> GameplayEffect 结算；没有恢复模板的直接伤害分支。
- `CharacterCardDefinition.AutomaticBattleAbilityCode` 是角色对 ASC 已授予 Ability 的必要引用，不是第二套技能定义。0 表示该角色不自动行动；负数或 ASC 未授予的引用在作者校验和角色创建时直接报错。测试内容明确引用 `TabletopBasicAttack(20005)`，没有把旧 `XAbility.ABILITY_Attack` 或测试常量写死进战斗。
- `Battle` 直接拥有各参战者行动进度、每秒选择窗口和当前执行 Ability 生命周期；`Tabletop` 读取 GAS 攻速、用战斗权威随机选择其它战斗方目标，并调用现有激活入口。EX-GAS 的结束 / 取消回调是进度重置的唯一时刻，不另存 Ability 时长。
- `ScenarioRun` 在平时回合制下仍把真实秒数交给活动战斗；只有普通牌桌行动受 `ProgressionMode` 控制。因此“战斗始终即时，平时可回合或即时”不再依赖两套时间配置。
- **验证**：自动攻击单条从 `0/1` RED 转为 `1/1` GREEN；统一 Foundation `23/23` 覆盖回合制世界实时战斗、攻速较高者先行动、正式 GAS 伤害、生命归零离场和战斗结束。战斗合同 `15/15`、剧本单局 `19/19`、全量 EditMode `504/505`（零失败、1 条环境条件跳过）、全量 PlayMode `47/47`。
- **冲突区裁决**：StackCraft `CombatRect` 同时写死两排布局、直接修改卡牌 Transform、夹取桌面边界并推动其它卡堆。CardLoop 由 `ScenarioDefinition` 内嵌 `BattleFormationRules` 按战斗方顺序声明队列，`Battle` 保存区域中心，`Tabletop` 根据参与人数、卡牌尺寸和阵型边距派生区域与卡牌姿态，再由唯一 `TabletopView` 投影 Transform 和 `TabletopBattleAreaView`。不保存第二套卡牌坐标，不让 UI / Physics 决定玩法碰撞。阵型支持每排容量、前后多排和剧本级偏移，Mod 新增阵营无需修改阵型。
- **作者源与刷新边界**：阵型和战斗区域都不是独立内容，不新增 ID；牌桌视图只绑定一个 `Tabletop`，同时观察卡牌修订和活动战斗修订。战斗基础渲染排序和区域视图预制体只在 `TabletopViewSettings` 配置一次；测试场景生成器通过正式菜单写入测试剧本阵型和区域视图资源，不手改场景 YAML。
- **验证订正**：本段最初的 `5/5` 战斗合同与 `7/7` Foundation 只证明阵型投影，不能证明自动合并。补齐区域和两个自动触发点后，`BattleEditModeTests` 为 `15/15`、`BattleFormationEditModeTests` 为 `3/3`；随后实时调度接入后的完整 `FoundationTestScenePlayModeTests` 为 `23/23`。统一场景同时验证区域重叠自动合并和实时自动攻击；正式命中规则与带判定逃跑仍是独立未完成效果。

#### 6.1.4 角色卡 GAS 归属与实时调度前置审查（2026-08-10）

- StackCraft 的 `CardInstance` 将 `CombatStats`、`CardCombatant`、生命和战斗 Transform 状态直接挂在同一个 Unity 卡牌对象上，所以 `CombatTask` 可以直接选择攻击者并调用其战斗状态。
- `CharacterCardDefinition : CardDefinition` 只引用一个 EX-GAS ASC 预设；`Tabletop.CreateCard` 根据内容定义自动创建直接持有唯一 `AbilitySystemCell` 的 `CharacterCard`。普通物品、地点和事件定义仍创建普通 `TabletopCard`，不会得到 ASC，也不能加入战斗。公开手工 `CreateCharacterCard(..., AbilitySystemCellConfig)` 已删除，行动候选和运行复核直接读取角色卡自身状态。
- GameCore `CharacterBase` 虽然也拥有一个 `AbilitySystemComponent`，但它是 `Movable` / Unity 场景角色，依赖 Transform、Rigidbody2D、控制器、动画、交互和旧角色数据。它与 `CharacterCard` 可以作为不同逻辑角色同时存在；但同一逻辑角色不得同时以两者实现，否则才会产生双 ASC。当前牌桌不引用 `CharacterBase`，不建立桥接器。
- 生成的 `XAbility` 中存在 `ABILITY_Attack` 常量，但生成常量不是攻击作者配置；EX-GAS 的正式激活还要求 ASC 持有 AbilitySpec，并通过 `AbilityActivationContext` 提供本次目标。该前置缺口后来由正式 `TabletopBasicAttack(20005)` 作者链、角色卡自动能力引用和 `Battle` 即时进度解决，不再是当前缺口。
- **6.3 前置证据**：EX-GAS 存在 `Attack(20001)`，但其 Timeline 使用 `CatchAreaPolygon2D`，命中链依赖 2D 场景角色的 `Movable`、姿态和物理层；纯 `CharacterCard` 只有 `AbilitySystemCell`。当前 ASC 预设也没有授予该 Ability，因此它不能作为牌桌攻击假装复用。
- **裁决**：本子模块不新增手写攻击状态机、伤害调用、Ability 转发层或用 `Battle.Update` 复制 Cost / Cooldown / Timeline。下一步先通过 EX-GAS 正式作者表建立使用 `AbilityActivationContext.MainTarget + CatchTarget` 的牌桌攻击 Ability，再让战斗聚合提交施法者卡、目标卡与 Ability 码；普通牌桌行动继续使用自身回合 / 即时换算，战斗保持独立即时时钟。

### 7. 存档 / UI 框架 / 作者工具

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `GameData` / `SceneData` 保存卡堆位置、卡牌动态数值、装备、任务进度、战斗、商店、遭遇、时间、发现卡牌和发现配方；`InfoPanel`、`ProgressUI`、`QuestsView`、`RecipesView` 形成可玩反馈；`RecipeDefinitionEditor` 能检测同材料配方冲突。 |
| Gameplay 职责归属 | GameCore / YokiFrame SaveKit 候选 + Gameplay 正式 `RunSave`、`MetaSave`、`ScenarioState`、`ModDependencySnapshot`；正式 UI 框架 + 关卡编辑器 + 内容校验器。 |
| 裁决 | **存档只参考状态范围；UI 框架模式和编辑器校验体验可吸收；素材只作原型。** |
| UI 说明 | 旧文档里的“UI 投影”口径不准确。这里要审查的是 UI 框架本身：界面如何订阅运行时状态、如何分优先级显示信息、如何把任务进度贴到桌面对象、如何让目标/配方列表随事件更新。 |
| 保留范围 | 局内卡牌实例、任务进度、目标进度、遭遇一次性记录、发现内容、角色装备和场景/地点状态的保存范围；信息请求优先级、任务/配方列表刷新、配方冲突提示、同条件多结果概率展示、点击定位冲突资产、进度条贴牌桌。 |
| 排除范围 | 不吸收 `Application.persistentDataPath` 全目录扫档、不吸收文件名槽位逻辑、不把场景名作为存档主键、不吸收 StackCraft 数据编辑器为正式关卡编辑器，不让 UI 直接拥有规则真相。 |

#### 模块 4 既有能力：牌桌行动进度表现（2026-08-10 已验证）

- **参考职责**：StackCraft 的 `CraftingTask` 保存制作进度和暂停状态，`CraftingManager` 同时创建 / 更新 / 销毁 `ProgressUI`。可吸收的是“桌面行动正在推进、暂停或完成”的可见反馈，不是其全局制作管理器、自动制作链或旧 UI 状态。
- **正式等效实现**：当前 `ActionInstance` 保存权威进度与运行状态，`Tabletop` 统一拥有活动行动；`TabletopView` 只读 `ActiveActions`，把每项活动行动投影到首个参与卡牌下方。启动、暂停、恢复、完成和结算仍走既有行动入口，进度条不拥有第二份行动状态。
- **资源与配置**：进度预制体仅由 `TabletopViewSettings` 配置一次，牌桌视图经现有 `ResourceSystem` 创建并释放资源句柄；没有新增第二套 YooAsset 地址、加载器、UI 事件总线或 UI 框架。
- **验收结果**：统一 `FoundationTest` 场景已真实验证半回合进度、暂停颜色、恢复推进和完成后视图释放；2026-08-10 在当前已打开的 Unity 编辑器中运行 `FoundationTestScenePlayModeTests`，结果 `8/8` 通过、`0` 失败。测试蓝色卡面仅是原型素材，不能据此宣称正式 UI 框架、自动制作或完整存档 UI 已经完成。

#### 7.1 牌桌可读状态与卡牌详情（2026-08-12 已验证）

- **参考职责**：StackCraft 的 `CardInstance` 在悬浮时向全局 `InfoPanel` 提交名称与描述，离开时撤销；`InfoPanel` 用请求者字典和优先级决定最终文字。吸收“悬浮即可读、其它持续焦点可在悬浮结束后恢复”的玩家效果，不吸收卡牌自行组装字符串、全局单例、请求者字典或 UI 内第二套优先级真相。
- **正式 owner**：当前牌桌的本地交互状态继续属于既有 `TabletopView`。它记录瞬时悬浮卡和持久选中卡，最终可读卡遵循“悬浮覆盖选择”；这些状态不是 Gameplay 规则，不进入 `Tabletop`、存档或联机权威状态。`TabletopCardDragInput` 是既有唯一指针解释组件，只负责更新该状态。
- **UIKit 边界**：新增 `TabletopCardInfoPanel` 作为当前牌桌的常驻只读投影。面板直接绑定 `TabletopView`，名称与描述来自正式 `CardDefinition`，局内对象来自当前 `TabletopCard`；没有新增 `InfoSystem`、UI Context、事件包装或第二套资源入口。
- **旧 UI 裁决**：GameCore `UISystem/UIManager` 仍承担旧 2D RPG 菜单与存档生命周期候选，不接管牌桌 hover/selection。UIKit 继续是唯一面板宿主；测试 HUD 和本次生成的详情布局只是统一场景验收夹具，不冒充最终美术方案。
- **生命周期**：卡牌被权威牌桌移除时，`TabletopView` 在同一次视图刷新中清理悬浮/选择；详情面板清空标题、描述和显示卡牌 ID。解绑或关闭面板时解除直接订阅，不保留旧卡牌引用。
- **验证**：可读状态 RED 精确命中缺少 `HoveredCardId` / `SelectedCardId` / `ReadableCardId`；悬浮覆盖选择与 UIKit 作者文本链 `1/1` 通过；移除可读卡牌的旧文本 RED `0/1`，修正后 `1/1`；最终 Foundation 全组 `17/17` 通过。证据分别见 `Logs/TestResults-Gameplay-Module71-ReadableCard-RED.log`、`Logs/TestResults-Gameplay-Module71-CardInfo-GREEN-R1.xml`、`Logs/TestResults-Gameplay-Module71-RemoveReadable-RED.xml`、`Logs/TestResults-Gameplay-Module71-RemoveReadable-GREEN.xml` 与 `Logs/TestResults-Gameplay-Module71-Foundation-PlayMode-Final.xml`。`Logs/Gameplay-Module71-CardInfo-PASS-Candidate.png` 只保留为当时确认测试详情可读的诊断图，不是现行视觉门禁证据。

#### 7.2 行动选择与填槽 UI（2026-08-12 已验证）

- **参考职责**：吸收 StackCraft 从牌桌交互得到可执行操作、显示进度和让玩家明确确认的效果；不吸收“合堆即自动制作”、`InfoPanel` 操作按钮或 UI 直接调用全局制作管理器。
- **正式 owner**：`TabletopInteraction` 解释一次牌桌释放并打开 UIKit；`TabletopActionChoicePanel` 只回传候选选择。未完整候选由牌桌拥有的 `ActionPlan` 保存绑定，`TabletopActionPlanPanel` 只投影和提交，UI 不保存第二份槽位状态。
- **扩展与生命周期**：同一牌桌可拥有多个计划，但 UIKit 只使用一个计划面板切换投影；移除卡牌会同步解除所有计划绑定，待计划参与者不能跨地区旅行。行动、内容、EX-GAS 条件和卡牌归属均在提交时由正式 owner 复核。
- **验证**：最终 Foundation `19/19`、相关 EditMode `23/23`。选择态与缺员填槽态的既有 GameView 图片只证明当时测试入口可读，不参与模块完成裁决。

#### 7.3 角色卡状态投影（2026-08-12 已验证）

- **参考职责**：StackCraft `CardInstance` 在角色卡面显示 `CurrentHealth`，并在受伤后刷新。吸收“玩家在牌桌上直接读到角色当前生命”的效果；不吸收卡牌自身持有生命、`CombatStats`、直接扣血、装备组件或卡牌销毁副作用。
- **唯一状态来源**：`CharacterCard` 直接读取自身唯一 EX-GAS `AbilitySystemCell` 中 `FightUnit/Health` 与 `MaxHealth`。`TabletopCardView` 只比较并投影这两个当前值，不建立生命副本、属性表、伤害链或 Gameplay 事件包装。
- **牌堆与战斗表现**：普通牌堆只显示顶牌角色状态，避免同堆文字重叠；进入战斗阵型的参战卡显示自身状态。普通卡牌不显示角色状态。测试状态条由正式测试场景作者源生成；既有 GameView 图片只记录顶牌 `100/100` 与独立牌动态 `73/100` 在当时可读，不是正式视觉验收。
- **延期范围**：模板没有 CardLoop 设计稿所需的职业、技能、经历侧栏；模板装备面板也依赖尚未成立的装备领域。完整角色侧栏必须等待对应领域对象、作者源和唯一写入口成立，不能由 UI 先创造临时数据。
- **验证**：RED 精确命中视图缺少角色状态契约；单条 GREEN `1/1`，最终 Foundation `20/20`，卡牌视图 EditMode `2/2`，均零失败、零跳过。证据为 `Logs/TestResults-Gameplay-Module73-CharacterHealth-GREEN-R4.xml`、`Logs/TestResults-Gameplay-Module73-Foundation-Final-R1.xml` 与 `Logs/TestResults-Gameplay-Module73-CardView-EditMode-Final-R1.xml`。

### 7.4 HUD 与交互反馈

- **参考职责裁决**：StackCraft `ProgressUI` 已由模块 4 的行动进度视图吸收，`ScreenFader` 已由模块 0.4 的转场职责承担，行动选择 / 确认已由 7.2 承担。`CardStatsUI` 的营养、货币和卡牌容量在阶段 C 已重新裁决为必须复现的玩家可见 HUD 效果；当前由 `ScenarioRun.GetTabletopStats()` 与 `ScenarioTurnPanel` 承担，不恢复 `StatsSnapshot` 管理器、`CardManager.OnStatsChanged` 或 `CardCategory.Currency`。`ModalWindow` 不另建第二弹窗系统，UIKit 继续作为唯一面板宿主。
- **正式归属**：日期、确认回合和即时流逝仍只属于当前 `ScenarioRun`。`TurnsPerDay`、推进模式和 `NormalizedDayProgress` 都是同一单局事实的只读投影；回合制与即时制共享每日回合和每回合秒数配置，不建立 `TimeSystem` 或 HUD 自持计时器。
- **玩家可见效果**：常驻 `ScenarioTurnPanel` 显示“第 N 天  x/y”和日内进度；对应 Prefab 作者源已迁入 `Assets/Art/Prefabs/UI/ScenarioTurnPanel.prefab`，文件名保留类型名是因为 YokiFrame YooAsset 面板加载器按 `UIPanel` 类型名取地址。`CardStatsUI` 的三项卡牌相关 HUD 反馈按模板拆成图标 + 数字：营养使用 `Stats_Nutrition.png`，货币使用 `Stats_Currency.png`，卡牌容量使用 `Stats_Card.png`；三张图都迁入 `Assets/Art/Sprites/StackCraft/` 作为项目自有副本，图片字节与 StackCraft 源文件一致，但 GUID 不复用参考目录；三项数字标签使用 StackCraft 同款 `LiberationSans SDF` 字体和材质，不能用项目中文测试字体替代。回合制显示可点击的“推进回合”；即时制显示不可点击的“即时推进中”，进度随正式剧本时间更新。禁用态使用明确的中性深灰，与回合制青色主操作区分。
- **验证**：完整统一场景 `Logs/TestResults-Gameplay-Module74-Foundation-PlayMode-Final.xml` 为 `21/21`，零失败、零跳过；同轮日志没有内存泄漏、悬空引用、未处理异常或编译错误。既有回合制与即时制 GameView 图片只作为测试投影可读性的历史诊断记录，不是模块门禁。
- **模块结论**：7.1-7.4 当前地基范围完成；下一步进入模块 8 单局快照与存档恢复。阶段 C 仍需等待模块 8-9，不能据此宣称 StackCraft 完整吸收完成。

### 8.1-8.2 内容集合与整局领域快照

- **模板效果与结构裁决**：StackCraft 的玩家效果是存档槽恢复场景、牌堆、卡牌、制作、任务与时间；旧实现由多个 Manager 在保存回调中拼装同一个 `GameData`。CardLoop 保留效果，删除多 Manager 写同一数据对象的结构；`ScenarioRun.CreateSnapshot()` 一次性聚合整局事实。
- **内容依赖**：快照保存本局冻结内容集合的全部唯一 `ContentId`，按序稳定输出。当前安装内容可以增加，但不能缺少旧存档依赖；缺失时一次列出全部 ID。当前尚无内容到来源 Mod 包的可靠映射，因此不伪造包名、版本或 hash；这部分等待正式 Mod 清单。
- **对象职责**：`ScenarioRun` 聚合地区、发现、日程和任务；`ScenarioRegion` 聚合自己的 `Tabletop`；`Tabletop` 保存卡牌 / 牌堆、活动行动与随机流；`QuestLog` 保存任务及其多态子项状态。没有新增全局快照系统、存档 Manager 或恢复 Resolver。
- **唯一编号**：不同地区共享的下一卡牌实例号属于单局，只在 `ScenarioRunSnapshot` 保存一次。地区牌桌快照不再各自保存编号；读档后所有地区继续引用同一序列，并拒绝跨地区重复卡牌 ID。
- **中间状态边界**：未提交填槽计划是本地临时交互，读档后取消；活动战斗不存档，存在时直接拒绝；旅行事务尚未提交时也拒绝。当前不会把临时 UI、半场战斗或异步场景事务伪装成可恢复事实。
- **任务 Mod 扩展**：每个 `QuestTaskRuntimeState` 自己生成 / 恢复派生快照，`QuestLog` 不按类型中央分发。代码 Mod 可扩展任务状态；若未实现存档契约，生成快照时直接报出类型，不静默清零。
- **验证**：整局 JSON 往返 `1/1`，`ScenarioRun` 全组 `13/13`，任务 / 活动行动 / 牌桌 / 内容相关回归 `42/42`，活动战斗存档边界 `8/8`；全量 EditMode `441/442`（零失败、1 条既有忽略），统一 Foundation PlayMode `21/21`。证据分别为 `Logs/TestResults-Gameplay-Module82-ScenarioSnapshot-GREEN-R6.xml`、`Logs/TestResults-Gameplay-Module82-ScenarioRun-R1.xml`、`Logs/TestResults-Gameplay-Module82-Related-R1.xml`、`Logs/TestResults-Gameplay-Module82-BattleSaveBoundary-R1.xml`、`Logs/TestResults-Gameplay-Module82-AllEditMode-R1.xml` 和 `Logs/TestResults-Gameplay-Module82-Foundation-PlayMode-R1.xml`。
- **8.3 历史判断订正**：此前“必须先给 EX-GAS 增加完整 ASC 导出门面”的判断已废止。按 UE GAS 职责校准后，ASC 是运行时能力聚合，不是通用 SaveGame；当前产品又不保存战斗中状态。角色快照只保存已成立的长期事实：ASC 等级与当前预设声明属性的 `BaseValue`，恢复时用现行 `CharacterCardDefinition` 的 ASC 预设重建并校验结构。没有修改 EX-GAS，没有读取 ECS Buffer，也没有复制标签、效果或 Ability 状态。

### 8.4 GameCore 槽位与模板存档 UI 吸收裁决

- **模板玩家流程**：标题界面提供 `Load Saved Games`，列表按槽位显示场景、任务进度和最后保存时间；每条有 `[Load]`、`[Delete]`，删除前弹确认；列表有删除全部与关闭；局内提供 `Quit to Title & Save`。这组流程来自 `Title.unity`、`SavedGamesUI`、`SavedGameSlot`、`ModalWindow` 和 `UIRoot.prefab`，不是整个正式游戏 UI。
- **UI 验收范围**：模板存档界面只用于确认玩家需要看到槽位摘要、读取、删除、清空、关闭和确认反馈。CardLoop 不复刻模板的具体尺寸、颜色、字体或布局；UIKit 只要通过正式职责完整承载相同操作结果、信息可读且交互可用即可。脚本与旧 `GameDirector` 引用必须替换，确认弹窗也不复制其全局单例。
- **槽位显示事实**：模板场景名改为剧本显示名 / 当前地区显示名；模板 `QuestProgress` 百分比不能伪造为通用任务完成度。只有当前剧本定义出正式进度口径后才显示百分比，否则槽位只显示剧本、地区和最后保存时间。槽位编号与保存时间直接来自 SaveKit `SaveMeta`，不要求玩家手填文件名。
- **正式职责**：GameCore `SaveSystem` / SaveKit 只拥有文件、槽位、版本、元数据和模块容器；`ScenarioDirector` 拥有活动单局快照创建、内容加载、场景切换和恢复发布。GameCore 不引用 Gameplay，Gameplay 不直接操作文件路径。
- **现有 GameCore 订正方向**：当前 `SaveSystem` 把旧 RPG `SaveDataBlock` 当作唯一模块，并把地图 / 标记 / 玩家 / 持久对象加载顺序写死。应重构为可保存 / 读取 SaveKit `SaveData` 模块容器；旧 RPG 世界块只是可选模块，不再抢占整个游戏存档根。不得新增 `GameplaySaveSystem`、第二文件格式或通用参与者注册表。
- **加载事务**：`ScenarioDirector` 先从槽位读取 `ScenarioRunSnapshot`，校验内容集合与剧本定义，再通过正式 `SceneSystem` 切换到快照当前地区，最后原子发布恢复后的 `ScenarioRun`。校验或场景切换失败时保持原活动单局不变；不能先销毁当前局再尝试恢复。
- **UIKit**：存档列表、槽位项和确认弹窗都由 UIKit 承载。UI 只读取 SaveKit 元数据和单局快照摘要，Load / Delete / Clear / Close 通过正式命令入口执行；UI 不保存第二份槽位集合、剧本状态或文件路径。
- **当前实施状态**：GameCore 槽位容器、`ScenarioDirector` 原子读档和角色 ASC 长期状态快照均已完成当前地基范围；统一场景已用角色卡验证真实文件、YooAsset 内容加载、候选单局、场景和可见牌桌改绑链路。`ScenarioSavePanel` 已用 UIKit 复现动态列表、覆盖 / 读取、删除确认、清空全部、关闭和保存并退出；端到端 `2/2`，功能链通过后模块 8 当前地基范围关闭。空列表、已有槽位和删除确认的既有 GameView 图片只作为过程诊断记录。活动战斗不保存。
- **文件层实施结果**：GameCore `SaveSystem` / `SaveFileStorageRuntime` 已改为创建、保存、读取 SaveKit `SaveData` 模块容器和槽位 `SaveMeta`；旧 RPG `SaveDataBlock` 只是一个模块。槽位 API 统一使用整数 `slotId`，删除 `SAVEFILE_A/B/C`、文件名后缀解析和哈希分配，UI 不再手填字符串 key。独立领域模块与旧世界块同槽往返 `1/1`，GameCore 全组 `96/96`；证据为 `Logs/TestResults-Gameplay-Module84-SaveContainer-GREEN-R2.xml` 与 `Logs/TestResults-Gameplay-Module84-GameCore-R1.xml`。
- **导演接入结果**：`ScenarioDirector` 负责活动单局快照注册、槽位读取、内容校验、候选单局构造、目标地区场景切换和最终活动单局替换。槽内其它模块保留；加载失败不销毁旧单局。活动单局提交变化后通过 YokiFrame `EventKit.Type` 发布 `ScenarioRunChangedEvent`，场景级牌桌表现 owner 统一改绑视图、交互与输入，不建立第二套运行状态。
- **验证边界**：导演 EditMode `7/7`、整局与文件回归 `14/14`、剧本内容 PlayMode `8/8`、统一地基 PlayMode `21/21` 是模块 8 中途证据；最终角色长期 GAS 快照、角色文件读档和模板等价 UIKit 存档界面均已完成，当前完整证据见本节“当前实施状态”和 `progress.md` 的模块 8 收口记录。
- **列表文件能力**：模板动态存档列表所需的有效槽位元数据、单槽删除和删除全部直接复用 SaveKit 正式 API，由 GameCore 文件 owner 暴露按整数槽位排序的只读结果。不存在槽位的删除返回失败，清空全部返回实际删除数量；不维护第二份存档字典、字符串文件名或目录扫描器。槽位合同 `2/2`，GameCore 回归 `97/97`。

#### 8.5 PauseMenu 暂停菜单补充吸收（2026-08-16）

- **参考职责**：StackCraft `PauseMenu` 在 `CancelWasPressedThisFrame` 且非日终结算时切换 CanvasGroup 显隐，继续按钮恢复，设置按钮打开 `GameOptionsUI`，返回标题按钮调用 `GameDirector.Instance.BackToTitle`；真实暂停通过 `TimeManager.SetExternalPause` 写入。
- **正式等效实现**：CardLoop 不恢复旧 `PauseMenu`、`TimeManager`、固定标题场景或 `GameDirector` 单例。`ScenarioPauseInput` 只把正式 `GameCore.InputSystem.OpenGameMenu` 转成 `EMenu.Pause` 菜单请求；`ScenarioPausePanel` 通过 `UISystem/UIManager` 菜单栈执行继续、设置和保存退出；暂停状态由既有 `GameStateSystem.Menu` 统一压栈，`Time.timeScale` 由同一个状态层负责。
- **设置与保存边界**：设置面板继续复用 GameCore `UISettings`，作为同一 UIKit 菜单栈的上层面板，不保存第二份暂停状态。保存并退出复用 `ScenarioDirector.SaveActiveRunToSlot(activeSlot)` 和 `EndScenarioAsync()`，不直接 `SceneManager.LoadScene`。
- **当前验证状态**：新增 `FoundationMenu_PauseSettingsAndContinueUseFormalMenuStack` 覆盖 Esc 打开、设置压栈、设置关闭、Esc 关闭、再次打开和继续恢复；2026-08-25 已补 `gameplay-static-preflight` 静态守卫，检查 `ScenarioPauseInput` 只订阅正式 `OpenGameMenu` 输入、`ScenarioPausePanel` 三按钮只走正式 UIKit 菜单栈 / `ScenarioDirector` 存档退出、正式 Prefab 三个按钮字段均绑定到现有 Button，并禁止旧 `StackCraftInput`、`TimeManager`、`GameDirector.Instance` 和旧 `PauseMenu` 链路回流。当前通过的是源码 / Prefab / 静态守卫证据；Unity 编译、PlayMode 和截图仍不由本条证明。

### 9. 作者工具与关卡编辑支撑吸收结果

- **参考真实范围**：StackCraft 没有独立关卡编辑器。它的作者工具由各 Definition Inspector、配方同材料提示、枚举堆叠矩阵、任务辅助 Inspector 和资源管线切换菜单组成。
- **内容校验**：CardLoop 继续以 `ContentValidator` 为唯一规则校验入口；Unity 菜单扫描全部 `ContentAsset`，问题携带来源对象供 Console 定位。当前 Unity 扫描 11 个作者资产零错误零警告。没有新增第二个校验窗口或编辑器索引真相。
- **配方冲突裁决**：相同参与条件的多个行动是玩家可选项，不是冲突；随机概率只属于单个行动内部结果分支。因此不恢复 StackCraft 的“同材料即冲突”签名和概率表，只保留断裂引用、无效权重、重复隐藏 key 等真正作者错误的校验。
- **剧本与地区作者入口**：剧本 SO 通过类型受限选择器组合地区和任务；地区 SO 唯一拥有 YooAsset 场景地址、牌桌规则和抵达位置。现有 Inspector 已是正式作者入口，不另建重复窗口。
- **牌桌作者入口**：地区内嵌的牌桌边界、禁放区域、卡牌尺寸和堆叠步进已具备中文字段名与说明。StackCraft 的 `StackingRulesMatrix` 依赖已排除的分类枚举，不进入正式架构；卡牌结构合并与行动可用性继续由牌桌对象和行动条件分别负责。
- **扩展边界**：YooAsset 按 `ContentAsset` 派生类型收集；代码 Mod 派生内容、行动结果和任务子项可通过各自校验 / 运行 / 快照入口接入，中央索引不按具体类型分发。本模块只证明 Unity 专业作者路径与代码扩展边界，不把未来游戏内编辑器、Mod 包协议、构建发布或创意工坊说成已完成。

### 阶段 C：StackCraft 机制与代表业务等价验收

> 2026-08-23 当前阅读口径：本节“无缺口 / 已收口”只限定为机制、规则结算、代表业务链路和已审计源码参数的编码缺口，不等于表面 / 动画最终一致性已经验收。卡面比例、文字落点最终观感、连续拖拽 / 移动 / 反馈手感、整体验收截图 / 录像和用户试玩确认，以 [`stackcraft-visual-animation-parity.md`](stackcraft-visual-animation-parity.md) 为当前真相；在该专项未收口前，不得说“和模板完全一致”“完整复刻”或“可以删除模板”。

- **已由新框架复现 / 接管**：`FoundationTitleTest -> FoundationTest` 已覆盖标题页新游戏、读取存档、设置、退出确认和友好模式开局，并通过唯一进程根进入统一地基场景。统一 `FoundationTest` 已覆盖内容加载、卡牌创建与拖拽、行动选择 / 填槽 / 推进 / 结算、按槽位打开卡包、完整日终与自动保存、日程 HUD、任务推进、地区旅行、EX-GAS 战斗、卡牌详情、角色生命、动态存档列表与恢复、内容校验和 SO 作者入口。完整日终 / 新日与跨日自动保存、战斗区域重叠自动合并、实时自动战斗、卡包逐槽打开、卡包商贩购买与任务完成数达标解锁提示、Buy 任意卡包任务事实、Sell 任意卡牌任务事实、箱子存币 / 取币 / 付款、装备穿脱 / 替换 / GE 效果和装备任务事实、行动与日终遭遇生成卡牌任务事实、战斗击败任务事实、当前牌桌状态任务事实、探索任务事实、研究发现任务事实、当前天数任务事实和时间切换任务事实均已由新框架接管。命中 / 闪避 / 暴击 / RPS 克制规则结算、牌桌必要文本飘字、投射物前摇、战斗音效、非战斗通用反馈音效、`HitUI` 式命中反馈、卡牌烟雾，以及牌桌相机中键平移 / 滚轮缩放 / 空间反馈聚焦已经接入正式链路。StackCraft `QuestsView` 的玩家效果由 `ScenarioJournalPanel` 任务页复现；`RecipesView` 不恢复独立配方系统，而由同一面板的“已发现配方 / 行动”页读取 `ScenarioRun` 的发现事实；新内容红点和首次查看已读状态由 `ScenarioRunSnapshot` 保存。
- **明确排除并说明理由**：固定 `CardCategory` / `QuestType` / `CombatType`、全局 Manager / 单例链、固定场景名、`Resources.LoadAll`、未种子化随机、JSON 目录扫档、模板旧输入、模板 UI 外观克隆、模板 `AudioManager` / `AudioId`、`CombatManager` / `CombatStats`、DOTween 运行时依赖、旧 Editor 抽屉和同材料配方冲突签名不进入新架构，因为它们会制造第二套身份、第二套资源 / 事件 / 数值真相或限制 Mod 扩展。战斗存档是用户已明确排除的玩家效果；活动战斗不进入普通单局存档。
- **尚未完成 / 阻止机制阶段通过**：`node .spec/tools/gameplay-static-preflight.mjs` 当前通过，没有发现未登记的 StackCraft 运行脚本，也没有发现正式 `Gameplay` / 测试 / 项目素材直接依赖 StackCraft 旧运行时或旧素材路径；2026-08-16 后补的屏幕效果、设置面板、推进模式按钮、任务 / 配方菜单、牌桌相机、卡牌烟雾、音效、自动移动和相关回归用例已经由 Unity 独占后的 `FoundationTestScenePlayModeTests`、`ScenarioContentPlayModeTests`、`ScenarioTitleScreenPlayModeTests` 与全量 PlayMode 覆盖。阶段 C 三张清单当前版在机制与代表业务链路范围内已收口；当前没有已登记的“必须继续编码后才能覆盖 StackCraft 已审计机制效果”的缺口。表面 / 动画最终一致性仍按视觉专项验收，不能用本节通过替代。原创主菜单业务、原创任务与配方内容、游戏内关卡编辑器、Mod 包协议、创意工坊和联机不属于 StackCraft 等价验收完成条件，只作为后续阶段扩展目标。

#### 阶段 C 三张清单（当前版）

| 清单 | 当前条目 | 证据口径 |
|---|---|---|
| 已复现 / 已接管 | 标题入口四命令、友好模式开局、日长滑条、场景进入、内容加载、卡牌创建、拖拽、拆堆 / 合堆 / 放置、候选高亮、行动选择、填槽、进度、暂停 / 恢复、行动结算、材料使用次数、Research 随机发现、Travel 地区切换、日程 HUD、回合 / 即时推进切换、完整日终、进食、超限售卡、遭遇、新日确认、自动保存、Game Over、动态存档列表、读取 / 删除 / 清空 / 保存退出、设置面板、暂停菜单、灰阶 / 暗角反馈、卡牌详情、行动剩余时间、牌堆摘要、角色生命、装备可读反馈、任务 / 配方日志、新内容红点、卡包逐槽打开、商贩解锁提示、购买 / 出售 / 存币 / 取币 / 付款、装备穿脱 / 替换 / GE 效果、任务事实 Have / Food / Coins / Capacity / Obtain / Craft / Defeat / Explore / Discover / Day / Time / Buy / Sell、实时战斗、增援、离战、战斗区域重叠合并、GAS 攻击结算、命中 / 闪避 / 暴击 / RPS 克制、投射物、HitUI 式反馈、战斗音效、非战斗音效、卡牌烟雾、牌桌相机平移 / 缩放 / 聚焦、CardAI 非敌对周期产出、随机巡逻、敌对追击、攻击半径开战、围栏容量、LimitBooster 容量扩展。 | 矩阵逐项记录 + 统一测试场景 + `FoundationTestScenePlayModeTests` `26/26` + 全量 PlayMode `59/59` + 静态预检。 |
| 明确排除 | 模板 UI 外观克隆、旧 `Manager` / 单例链、固定场景名、`Resources.LoadAll`、旧输入系统、未种子化随机、JSON 全目录扫档、模板枚举标签体系、同材料配方冲突签名、模板本地战斗数值系统、模板 `AudioManager` / `AudioId`、DOTween 运行时依赖、旧 Editor 抽屉、旧 `InfoPanel` 队列、旧装备面板漂浮卡、旧 `WorldCanvas` 单例、旧 `GameData` DTO、战斗存档。 | 结构冲突裁决 + 用户已裁决战斗不存档 + 当前正式 owner 已接管玩家效果。 |
| 仍缺失 / 后续可选迁移 | 当前没有已登记的“必须继续编码后才能覆盖 StackCraft 已审计机制效果”的缺口；剩余 StackCraft 卡牌 / 卡包 / 配方 / 任务 / 遭遇 `.asset` 尚未全部转换成 CardLoop 作者源，但按当前用户口径不属于代表性验收完成条件。表面 / 动画最终一致性仍未收口，必须继续按视觉专项逐项验收。 | 源码脚本名覆盖对账全部命中；静态预检无旧路径依赖，并已自动检查整个 `Assets` 根、ProjectSettings 和 Packages 不得引用 StackCraft 资产 GUID、`Assets/StackCraft` 资源路径或 `CryingSnow` 源码命名空间；额外 GUID 扫描确认 `Assets/StackCraft` 下 708 个旧模板 GUID 没有被参考目录外正式文本资源引用；代表性业务审计覆盖 Starter / Beginning 竖切。自动化只能证明机制覆盖项和代表性业务参数，不替代玩家试玩、表面 / 动画最终验收、用户删除授权和删除后 Unity 编译 / PlayMode 验证。 |

#### 卡包逐槽打开补充吸收（2026-08-14）

- StackCraft 的 `PackDefinition` 按槽位保存普通加权卡池、配方候选与配方概率；`PackInstance` 每次点击只抽取下一个槽位，优先尝试尚未发现的配方，未命中或没有可用配方时回退普通卡池，最后一次后移除卡包。
- StackCraft `PackInstance.prefab` 使用独立 `Pack.fbx` 和 `0.9 × 1.3000002` 碰撞尺寸，不能用普通 `Card.fbx` 缩放模拟。CardLoop 仍由同一个 `TabletopCardView` 承接卡牌表现，但绑定 `CardPackDefinition` 时切换到 `Assets/Art/Models/卡包.fbx` 自有副本网格，并同步候选高亮网格与碰撞 footprint。
- `CardPackDefinition` 作为明确的 `CardDefinition` 子类拥有有序槽位。卡包初始使用次数直接由槽位数量派生，作者不再同步维护第二份次数；普通条目、配方行动和配方卡都使用现有唯一内容 ID 与内容校验。
- `OpenCardPackResultIntent` 只声明使用哪张参与卡包。真正抽取由牌桌行动结算读取当前槽位，使用牌桌权威随机选择结果，并复用既有卡牌生成、使用次数和单局内容发现提交；没有 `PackManager`、第二随机源、卡包运行时子类或直接修改牌桌的 SO。
- `ActionDefinition.CanStartFromClick` 是显式作者能力：只有开启的行动会响应卡牌点击，普通卡牌点击继续用于选择和详情，拖拽行动保持原入口。测试卡包的点击仍先经过新输入系统、行动候选和 UIKit 选择，不因单候选自动执行。
- 本切片只完成卡包打开；卡包商贩的解锁、分批付款、成交生成、收藏显示和购买任务事实在紧随其后的交易子模块中接管。
- **验证**：缺少卡包类型与结果意图的编译 RED 成立；卡包领域 `3/3` 覆盖槽位顺序、同种子加权重放和未发现配方筛选；玩家点击入口从候选 `0` 的 RED 转为 `1/1` GREEN。行动结算回归 `15/15`、统一 Foundation `24/24`、全量 EditMode `507/508`（零失败、1 条环境条件跳过）、全量 PlayMode `48/48`。

#### 卡包商贩购买补充吸收（2026-08-14）

- StackCraft 的 `PackVendor` 解锁门槛、分批投入货币、付款状态保存、满价后生成卡包、付款归零、收藏进度和购买任务事实已拆为 Gameplay 的 `PackVendorDefinition` / `PackVendorCard`、现有牌桌行动和现有 `QuestLog` 事实。
- 售卖关系拥有价格、出售卡包和完成任务数门槛；商品 `CardPackDefinition` 只拥有自己的抽取内容，不保存商贩价格。`PackVendorCard` 作为 `TabletopCard` 派生对象拥有唯一付款进度，解锁状态由任务日志完成数和作者门槛实时推导，不保存 `isUnlocked` 副本。
- `PurchaseCardPackResultIntent` 只声明商贩槽位和付款槽位。现有 `ActionResultSettlement` 在行动计划中冻结付款事实，提交前复核商贩和货币仍有效；非满价只移除本次货币并累计付款，满价才生成卡包并清零付款。没有 `TradeManager`、商店单例、第二经济系统或第二事件总线。
- StackCraft `TradeManager.PlayActivationSequence` 的解锁提示序列不恢复旧输入锁定、时间暂停和全局信息队列。当前由 `ScenarioRun.RefreshQuestState` 在任务完成数跨过商贩门槛时发布只读牌桌表现提示：先聚焦商贩位置，再对局内商贩卡短暂高亮。
- 卡牌派生类型通过 `CardDefinition` 的运行时工厂和 `TabletopCardRuntimeStateSnapshot` 自己创建 / 恢复状态；牌桌不再中央硬编码角色类型，Mod 派生卡牌可沿同一入口接入自己的实例状态。
- `CardPackDefinition.GetCollectionProgress` 读取现有 `ScenarioRun` 内容发现集合；行动首次生成卡牌内容时一并提交发现事实，因此购买后打开卡包会真实推进收藏，而不是由 UI 私自计数。
- **验证**：商贩领域与完整单局快照 `8/8`；行动结算回归 `15/15`；统一 Foundation 真实拖拽购买 `1/1`；全量 EditMode `515/516`（零失败、1 条既有环境跳过）；全量 PlayMode `49/49`；`spec-lint` 与规范测试 `2/2`。2026-08-16 补充解锁提示后，新鲜 `PackVendorEditModeTests` `10/10` 通过；该验证覆盖商贩门槛、购买链和提示代码编译，不代表阶段 C 完成。

#### Buy 任意卡包任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Buy` 在 `TargetCard` 为空时统计任意已购买卡包，非空时只统计指定卡包。Gameplay 保留这个玩家效果，不恢复 `QuestType` 枚举、`QuestManager` 或全局交易事件。
- `CardPackPurchaseQuestTaskDefinition` 仍是现有 `QuestLog` 的任务子项：目标卡包 ID 留空表示任意卡包，填写时必须解析为 `CardPackDefinition`。运行时只解释 `CardPackPurchasedQuestTaskFact`，事实仍由正式购买结算提交。
- **验证**：新增任意卡包运行时测试 `1/1`、新增作者源校验测试 `1/1`、`PackVendorEditModeTests` `10/10`、`QuestLogEditModeTests` `16/16`、`ScenarioRunEditModeTests` `25/25`；Unity 编译空闲且 Console `0` 错误。该验证只覆盖 Buy 任务的任意卡包语义，不代表阶段 C 完成。

#### Sell 出售任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Sell` 在 `TargetCard` 为空时统计任意已售卡牌，非空时只统计指定已售卡牌。Gameplay 保留这个玩家效果，不恢复 `QuestType` 枚举、`QuestManager`、`TradeManager` 或全局售卡事件。
- `CardSaleQuestTaskDefinition` 仍是现有 `QuestLog` 的任务子项：目标卡牌 ID 留空表示任意卡牌，填写时必须解析为 `CardDefinition`。运行时只解释 `CardsSoldQuestTaskFact`，事实仍由正式出售行动结算提交。
- 正式出售行动链由 `SellCardsResultIntent` 和 `ActionResultSettlement` 承担：结算成功后移除出售槽位内的卡牌，按卡牌 `SellValue` 生成货币，并把本次已售内容 ID 返回给所属 `ScenarioRun`。失败、取消、非正式移除和测试夹具清理不会发布出售事实。
- 2026-08-19 对账订正：StackCraft `CardBuyer.CanTrade` 的“整堆所有卡都可售，且箱子必须为空”由 `CardSaleSourceAvailableCondition` 承担；出售槽位作者源不再固定测试石头卡，而是允许任意内容进入，再由条件按 `CardDefinition.SellValue > 0` 和空箱规则过滤。开卡包、购买卡包、存币和取币行动不得写入出售条件；静态预检已覆盖这个作者源边界。
- 同次对账发现并修正出售生成位置：StackCraft `TradeZone.spawnPosition` 是“收购点位置 + 交易区生成偏移”，因此 `CardBuyerDefinition` 现在声明 `CurrencySpawnOffset`，`ActionResultSettlement` 用收购点槽位作为货币生成锚点，并传入该偏移；不再把货币锚定到被出售卡牌。
- **验证**：刷新 AssetDatabase 后新增测试进入 Unity TestRunner；`ScenarioRunEditModeTests.CompletedSaleAction_AdvancesCardSaleQuest` `1/1`、完整 `ScenarioRunEditModeTests` `27/27` 通过；Unity 编译空闲且 Console `0` 错误。该验证覆盖正式出售行动推进 Sell 任务，不代表阶段 C 完成。

#### Discover 与 Day 任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Discover` 由 `QuestManager.HandleRecipeDiscovered(recipeId)` 推进，本质是“配方被发现后完成任务”。Gameplay 不恢复独立 Recipe 系统或 `CraftingManager` 全局事件，改由研究行动成功提交发现内容事实。
- `ResearchDiscoveryResultIntent` 在行动开始时冻结研究候选；行动完成后从尚未发现的候选中用牌桌权威随机选择，生成对应配方卡，并由 `ScenarioRun.DiscoverContent` 写入当前单局发现集合。`ContentDiscoveryQuestTaskDefinition` 只读取 `ContentDiscoveredQuestTaskFact`，不关心发现来源。
- StackCraft 的 `QuestType.Day` 由 `QuestManager.HandleDayChanged(currentDay)` 推进，本质是“当前天数达到目标后完成任务”。Gameplay 的当前天数由 `ScenarioRun` 的确认回合 / 日终流程唯一推导，并在 `RefreshQuestState` 中提交 `DayReachedQuestTaskFact`；后继任务激活时也会立即读取当前日期事实。
- **验证**：新增研究完成推进发现任务回归 `ScenarioRunEditModeTests.CompletedResearchAction_AdvancesContentDiscoveryQuest` `1/1`，完整 `ScenarioRunEditModeTests` `27/27`；Unity 编译空闲且 Console `0` 错误。该验证结合既有日期边界测试，覆盖 Discover / Day 任务事实，不代表阶段 C 完成。

#### 箱子存币与付款补充吸收（2026-08-15）

- StackCraft 的箱子存币效果吸收为 Gameplay 的 `ChestCardDefinition` / `ChestCard`。箱子是 `TabletopCard` 派生对象，唯一拥有本局存币数量和容量；它不是通用库存、背包、局外仓库或第二经济系统。
- 存币、取币和用箱子付款全部进入现有牌桌行动链。`DepositCurrencyIntoChestResultIntent` 将货币卡移除并增加箱子存币；`WithdrawCurrencyFromChestResultIntent` 从箱子取出一张货币卡；`PurchaseCardPackResultIntent` 可从非空箱子扣款，箱子自身不被移除。
- 付款来源仍由购买行动的付款槽位声明，货币卡和箱子共用同一槽位；货币卡身份由 `CurrencyCardQuery` 从当前内容集合中的收购点、箱子和售卡结果声明推导，不新增 `CardCategory.Currency` 枚举、标签或第二配置。`CardPaymentSourceAvailableCondition` 只判断付款来源是否可用，不保存付款状态；普通非货币卡不会进入付款候选，结算阶段若出现会直接报错。没有新增 `TradeManager`、库存系统、经济总管、事件包装或第二付款状态。
- UI 必要反馈只读投影箱子状态：`TabletopCardInfoPanel` 显示“存币：当前/容量”。详情面板不拥有存币状态，也不参与结算。
- 候选条件语义订正：候选探测中槽位未填或对象不匹配属于正常不可用，返回 false；只有绑定了不存在的牌桌卡才抛出异常。这样避免无关购买行动在“货币拖到箱子”时污染候选，同时保留内部状态损坏的快速暴露。
- 统一 `FoundationTest` 真实玩家链已覆盖：创建箱子和货币 -> 拖拽货币到箱子 -> 通过行动选择面板存币 -> 单击箱子取币 -> 再存满 -> 拖拽箱子到卡包商贩 -> 用箱子存币付款生成卡包 -> 箱子仍在且存币归零 -> 详情面板显示 `0/2`。
- 2026-08-25 静态守卫补强：`gameplay-static-preflight` 现在直接读取 `ChestLogic.cs`、`ChestDefinition.cs` 和 `Card_Chest_WoodenChest.asset`，证明 StackCraft 箱子来源包含存币、取币、价格文本显示、Coins / Coin 音效和 Wooden Chest 容量 50；同时守卫当前 `ChestCardDefinition`、`ChestCard`、存取币行动结果、付款过滤、详情面板只读显示、Foundation 箱子测试资产和 `ChestCardEditModeTests` 四条回归。当前 Foundation 快速箱子容量仍是 2，只用于短链路验证，不冒充模板 Wooden Chest 的 50 容量。
- 新鲜验收：`ChestCardEditModeTests` `4/4`，`PackVendorEditModeTests` `8/8`，新增 Foundation 箱子玩家链 `1/1`，完整 `FoundationTestScenePlayModeTests` `26/26`。源码定向 `git diff --check` 通过；Unity 生成器已重建箱子、存币行动、取币行动和 Foundation 测试场景。

#### 装备穿脱与装备任务事实补充吸收（2026-08-15）

- StackCraft 的装备玩家效果拆为两部分吸收：一是装备卡离桌、占用角色槽位、同槽替换、卸下回桌和装备效果影响角色；二是 `QuestType.Equip` 在装备指定卡牌后推进任务进度。
- Gameplay 的作者源是 `EquipmentSlotDefinition` 和 `EquipmentCardDefinition`。装备槽位是内容 ID，可由内容包扩展，不使用枚举；装备卡只引用 EX-GAS `GameplayEffect` 表达当前已成立的持续效果，不复制 `CombatStats`、`StatModifier`、Ability 配置或标签表。
- 运行时装备状态由 `CharacterCard` 直接拥有。`Tabletop` / `TabletopCards` 负责装备卡离桌、替换旧装备回桌、卸下回桌和快照恢复后重施加装备 GE；装备 / 卸装仍通过正式行动候选、行动计划、结算和快照链执行。
- 装备任务事实不恢复 `QuestManager`、`QuestType` 枚举或全局事件链。`ActionResultSettlement` 在装备提交成功后返回被装备内容 ID，`ScenarioRun` 把 `CardEquippedQuestTaskFact` 交给当前单局 `QuestLog`，`CardEquipQuestTaskDefinition` 自己累计指定装备卡次数。
- **验证**：装备效果定向从 `4/4` 扩展到装备任务事实 `5/5`；`QuestLogEditModeTests` `11/11`、`ScenarioRunEditModeTests` `19/19`、`ActionResultSettlementEditModeTests` `15/15`、完整 `FoundationTestScenePlayModeTests` `26/26`；Unity 编译完成且 Console `0` 错误。

#### 生成卡牌任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Obtain` 监听 `CardManager.OnCardCreated`，`QuestType.Craft` 监听 `CraftingManager.OnCraftingFinished(resultCard)`。CardLoop 吸收“成功产出指定卡牌后推进任务”的玩家效果，不恢复固定任务枚举、全局卡牌创建事件、制作管理器事件或任务中央分发器。
- `CardsCreatedQuestTaskFact` 只由成功提交的行动结果产生。`ActionResultSettlement` 在真实创建卡牌后返回本次创建的内容 ID，`ScenarioRun` 将该事实交给当前单局 `QuestLog`，`CardCreationQuestTaskDefinition` 自己按指定卡牌内容 ID 累计数量。
- 该切片明确不把所有 `Tabletop.CreateCard` 都视为 Obtain：购买、日终遭遇、手动创建和测试夹具生成各有不同现实来源，若要作为任务事实，必须沿对应来源单独提交，而不是恢复 StackCraft 的全局 `OnCardCreated`。
- **验证**：`QuestLogEditModeTests` `11/11`、`ScenarioRunEditModeTests` `19/19`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity Console `0` 错误。该验证覆盖行动产物推进任务，不代表其它 Quest 事实或阶段 C 已完成。

#### 日终遭遇生成任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Obtain` 由 `CardManager.OnCardCreated` 推进，因此日终遭遇等正式游戏来源创建出的普通卡，也属于“获得指定卡牌”的玩家效果。CardLoop 保留该效果，但不恢复全局卡牌创建事件，不把测试夹具、读档恢复或手动布景混算进任务。
- `ScenarioRun.ResolveDayEncounter` 是日终遭遇卡牌创建的唯一正式来源。它在真实创建遭遇卡后收集本次创建内容 ID，提交 `CardsCreatedQuestTaskFact` 给当前单局 `QuestLog`，再刷新状态型任务。日终流程仍归 `ScenarioRun`，没有新增 `DayCycleManager`、事件包装或第二任务系统。
- **验证**：刷新 AssetDatabase 后新增测试进入 Unity TestRunner；`ScenarioRunEditModeTests.DayCycle_CreatedEncounterCardsAdvanceCardCreationQuest` `1/1`、`ScenarioRunEditModeTests` `26/26`、`QuestLogEditModeTests` `16/16`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity 编译空闲且 Console `0` 错误。第一次单条执行曾因新增测试尚未被发现而卡在 `starting` 90 秒，刷新后已恢复。该验证只覆盖日终遭遇生成推进 Obtain / Craft 任务事实，不代表阶段 C 完成。

#### 击败卡牌任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Defeat` 由 `QuestManager.HandleCardKilled(CardInstance killedCard)` 推进，它监听战斗击杀事实。CardLoop 吸收“战斗死亡清理后指定卡牌被击败会推进任务”的玩家效果，不恢复 `QuestManager`、固定 `QuestType`、全局 `CardKilled` 事件或把所有卡牌移除都混成击败。
- `Tabletop.ResolveDefeatedParticipants` 只在战斗结算发现角色生命归零、并由死亡清理正式移除角色卡后，收集被击败卡牌内容 ID。普通 `RemoveCard`、出售、旅行、测试夹具清理和非战斗移除不会提交该事实。
- `ScenarioRun` 是任务日志所属的单局 owner，接收 `ScenarioRegion` / `Tabletop` 透传的击败事实后，将 `CardsDefeatedQuestTaskFact` 提交给当前 `QuestLog`。`CardDefeatQuestTaskDefinition` 自己按指定 `CardDefinition` 内容 ID 与目标数量累计进度。
- `Tabletop` 构造入口显式要求击败事实回调，没有可选空兜底；如果某个单局牌桌不能把击败事实接回剧本，应在构造时暴露，而不是静默丢任务进度。测试夹具显式传入空回调，表示该测试不验证任务事实。
- **验证**：`QuestLogEditModeTests` `11/11`、`ScenarioRunEditModeTests` `19/19`、`BattleEditModeTests` `15/15`、`BattleFormationEditModeTests` `3/3`、`ActionCandidateEditModeTests` `8/8`、`ActionInstanceEditModeTests` `16/16`、`TabletopCardsEditModeTests` `12/12`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity 编译空闲且 Console `0` 错误。该验证覆盖战斗击败推进 Defeat 对应任务，不代表其它 Quest 事实或阶段 C 已完成。

#### 当前牌桌状态任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Have / Food / Coins / Capacity` 由 `QuestManager.HandleStatsChanged(StatsSnapshot stats)` 推进。`StatsSnapshot` 来自 `CardManager.GetStatsSnapshot()`，会统计当前所有卡牌、食物营养、货币、箱内存币和卡牌上限。
- CardLoop 吸收“当前牌桌状态达到要求后推进任务”的玩家效果，但不恢复 `StatsSnapshot` 管理器、`CardManager.OnStatsChanged`、`CardCategory.Currency` 枚举或任务中央分支。状态事实由当前单局 `ScenarioRun` 在 `RefreshQuestState` 中从全部地区牌桌即时生成。
- `TabletopStateQuestTaskFact` 重复记录当前卡牌内容 ID，统计 `FoodCardDefinition.NutritionPerUse * RemainingUses`，读取 `ChestCard` 中按 `ChestCardDefinition.CurrencyCardId` 存储的货币数量，并用剧本基础容量加所有卡牌 `CardLimitBonus` 得到当前容量。
- 对应任务子项为 `CardPossessionQuestTaskDefinition`、`FoodNutritionQuestTaskDefinition`、`CurrencyAmountQuestTaskDefinition` 和 `CardCapacityQuestTaskDefinition`。货币任务必须指定具体货币卡内容 ID，避免把金币变成项目全局单例真相；箱中货币和牌桌上同内容卡牌会合并统计。
- 状态型任务未完成前按当前状态刷新进度，完成后不回退。后继任务被激活后，`ScenarioRun.RefreshQuestState` 会在同一刷新循环重新提交当前状态事实，因此不需要第二事件总线或防重复表。
- **验证**：RED 为新增测试首次编译失败，缺少 `TabletopStateQuestTaskFact` 与四个任务子项；GREEN 为 `QuestLogEditModeTests.TabletopStateQuestTasks_SetProgressFromCurrentStateFact` `1/1`、`ScenarioRunEditModeTests.ActivateInitialQuests_EvaluatesCurrentTabletopStateTasks` `1/1`、既有 `QuestLogEditModeTests` `11/11`、既有 `ScenarioRunEditModeTests` `19/19` 通过；Unity 编译空闲且 Console `0` 错误。该验证覆盖状态型任务事实，不代表阶段 C 完成。

#### 探索与时间任务事实补充吸收（2026-08-15）

- StackCraft 的 `QuestType.Explore` 由 `QuestManager.HandleExplorationFinished(CardDefinition areaCard)` 推进，只在目标卡牌与被探索区域 / 地点一致时完成。CardLoop 保留“指定卡牌被探索后推进任务”的玩家效果，不恢复 `QuestManager`、`CraftingManager` 全局回调、专用 `ExplorationRecipe` 子类或固定 `QuestType` 枚举。
- `ExploreCardsResultIntent` 只声明行动成功后哪个参与槽位代表“已探索卡牌”。行动开始时 `ActionResultSettlement` 从牌桌读取绑定卡牌并冻结内容 ID；行动成功提交后，`ScenarioRun` 才把 `CardsExploredQuestTaskFact` 写给当前 `QuestLog`，失败、取消、参与对象失效都不会发布事实。
- 冻结的探索内容进入 `ActionResultPlanSnapshot`。未完成探索行动读档后恢复同一探索事实，不重新读取已经可能变化的行动作者资产；旧快照缺少该字段时按空集合兼容，不破坏既有存档。
- StackCraft 的 `QuestType.Time` 由 `TimeManager.CycleTimePace` 推进，它表达的是玩家执行一次时间控制动作后的任务反馈。2026-08-25 之后，CardLoop 已在 `ScenarioRun.ScenarioTimePace` 内承接 `Paused / Normal / Fast` 三档速度；旧的“只做推进模式切换、不承接速度档”口径已失效。
- 当前已存在的任务事实 `ProgressionModeChangedQuestTaskFact` 仍只记录 `ActionProgressionMode.TurnBased / RealTime` 切换，这是早期用“推进模式切换”近似承接 `QuestType.Time` 的实现。它不是 `ScenarioTimePace` 档位变化事实，也不能和 HUD 速度循环混成同一套任务入口。
- 时间任务不会因初始默认回合制或快照恢复自动完成；当前只有玩家通过正式推进模式入口完成一次模式切换才推进任务。若后续要一比一对齐 StackCraft `QuestType.Time` 的 HUD 速度点击任务，应新增或重构为“速度档切换事实”，并删除旧近似或把它明确降级，不能并行保留两套任务真相。
- **验证**：`QuestLogEditModeTests` `16/16`、`ScenarioRunEditModeTests` `25/25`、`ActionResultSettlementEditModeTests` `15/15` 通过；Unity 编译空闲且 Console `0` 错误；`.spec` lint 通过，规范测试 `2/2` 通过。该验证覆盖 Explore / Time 任务事实，不代表阶段 C 完成。

#### 完整日结效果补充审计（2026-08-14）

- StackCraft 的 `DayCycleManager` 顺序执行日终通知、喂食、强制出售超限卡牌、最多一个遭遇、新日确认和自动保存。该顺序证明“跨日不是只改一个整数”，但它的饥饿数值、容量经济、遭遇筛选和单槽存档策略不属于通用时间对象。
- 当前 `ScenarioRun.AdvanceWorldTurn` 已统一推进所有地区行动，跨日后更新按天任务，并发布同一单局的回合事实；回合制确认和即时制换算都进入这一个入口。因此日数、回合边界和新日事实已经复现。
- 强制处理超限卡牌、进食、遭遇与新日确认都先作为模板实验效果复现；模板参数只属于测试内容，不自动成为 CardLoop 最终规则。实现必须回到 `ScenarioRun` 的同一跨日生命周期，不能新增空 `DayCycleManager`、事件包装或平行状态。
- 自动保存复用现有 `ScenarioDirector` 与 `SaveSystem`。先按模板效果建立可配置的自动槽位覆盖策略并验证跨日后可读取恢复，再由试玩决定最终产品策略；不能用“临时策划尚未确定”继续延期。
- **实现结果**：`ScenarioRun` 持有单次日终运行对象与阶段，食物恢复通过 EX-GAS GE `2005` 结算；超限卡牌只能通过现有行动链处理，售卡行动原子移除被售卡并生成货币。货币不计入上限，卡牌可提供上限加成；超限归零后生成最多一个遭遇并进入新日确认，没有新增日终 Manager、交易 Manager 或第二套容量状态。
- **自动保存结果**：新日确认继续走 `ScenarioDirector.ContinueDayCycle`，阶段结束后由现有 `SaveSystem` 覆盖当前活动整数槽位。日终规则与保存职责仍分属单局和导演，没有把文件存储塞回日终对象。
- **真实链路验收**：统一 `FoundationTest` 已通过“进食 -> EX-GAS 生命 `20 -> 70` -> 超限 -> 拖拽售卡 -> 行动选择 -> 生成 2 枚货币 -> 生成遭遇卡 -> HUD 摘要 -> 开始第 2 天并自动保存”。全量 EditMode `500/501`（零失败、1 条环境条件跳过），全量 PlayMode `46/46`；HUD 布局返工后的日终定向 PlayMode `1/1`。
- **过程诊断证据**：`Assets/Screenshots/StackCraft-DayCycle-Sell-Encounter-Final.png` 只记录货币卡详情、遭遇摘要和新日入口在当前测试界面可读。它不是正式视觉验收图，不参与日终模块完成裁决，也不代表测试皮肤、临时素材或整体 UI 已进入稳定候选。
- **新鲜验收**：既有全量 EditMode `452` 条中 `451` 通过、`0` 失败、`1` 条环境条件跳过；既有全量 PlayMode `42/42` 通过。标题入口定向 PlayMode 已复验为 `4/4`，覆盖标题命令和友好模式开局；清空控制台后的真实 Play -> Ready -> Stop 为 `0` 错误。标题、设置、读取存档和退出确认的既有 GameView 图片只保留为当时测试入口可读的诊断记录，不是地基完成条件。
- **后续验收口径**：继续吸收模板时以玩家操作产生的规则结果为主，例如卡牌生成 / 消耗、行动开始 / 暂停 / 完成、状态变化、旅行、战斗和存档恢复；旧 UI 结构和皮肤不照搬，但入口、反馈、卡面表面和动作动画中承载模板玩家效果的部分必须进入专项对账，不再用“只要能操作”替代表面 / 动画一致性。

#### 战斗区域重叠自动合并补充吸收（2026-08-14）

- StackCraft 在创建新战斗前检查潜在战斗区域，并在已有战斗加入成员、区域扩张后再次检查；矩形相交时清理两块旧区域，再按固定 Player / Mob 阵营重建第三场战斗。CardLoop 完整保留“可见战斗区域重叠后自动成为一场并重新排阵”的玩家效果，不吸收全局 `CombatManager`、固定两阵营、物理碰撞真相或重建随机状态。
- `Battle` 直接拥有唯一权威区域中心 `AreaCenter`；区域尺寸由战斗方数量、各方参战人数、牌桌卡牌尺寸和阵型边距派生。`Tabletop.StartBattle` 在创建前检查潜在区域，`Tabletop.JoinBattle` 在提交增援前检查扩张后的区域；多个重叠目标按活动战斗集合顺序确定性合并。
- 自动合并按战斗方索引建立映射，不读取或猜测 GAS 阵营标签；战斗方数量不一致时直接报错。`Tabletop.MergeBattles(destination, source, sourceSideToDestinationSide)` 仍是同一正式原子命令，供剧情、Ability 和特殊规则显式指定其它分组。
- 目标 `Battle` 对象、战斗 ID 和已推进的权威随机流保持不变；来源战斗结束并移除，没有创建第三场战斗、第二套阵营状态、合并管理器或新事件。`TabletopBattleAreaView` 只读投影派生区域，不参与重叠判定，也不保存第二份玩法状态；`TabletopView` 按战斗投影版本创建、刷新和释放区域视图。
- 两条有效 RED 分别证明新战斗创建前不会自动合并、增援扩张后仍保留两场战斗。GREEN 后自动创建合并 `1/1`、扩张后自动合并 `1/1`、`BattleEditModeTests` `15/15`、`BattleFormationEditModeTests` `3/3`；统一 `FoundationTest` 真实显示两个战斗区域，经连续增援扩张发生重叠后自动合并为一个区域，战斗结束后区域清空并恢复原牌堆表现，定向 `1/1`、完整 Foundation `22/22`。最终全量 EditMode `504/505`（零失败、1 条环境条件跳过），全量 PlayMode `46/46`。

#### 行动中断、战斗占用与特殊配方补充审计（2026-08-14）

- StackCraft 在拖拽开始时立即拆堆，所以需要暂停制作，放回原堆后恢复，落到别处则重新校验或取消。Gameplay 拖拽期间不修改牌桌权威卡牌 / 牌堆，只在释放后提交放置或行动意图，因此不需要复制 `OriginalCraftingStack`、拖拽暂停标记或自动恢复分支。
- 参与对象真的被移除时，Gameplay 已在行动推进前复核参与条件，以 `ParticipantInvalidated` 取消，且不推进、不提交结果、不发布行动完成事实。显式暂停 / 恢复 / 取消继续由同一 `ActionInstance` 生命周期承担。
- 审计发现原公开入口允许同一角色一边执行普通行动一边加入即时战斗。现由 `Tabletop.StartBattle`、`JoinBattle` 和行动启动入口直接读取现有活动集合并拒绝冲突；不新增占用表、忙碌标签、影子状态或自动取消。调用方必须先明确完成 / 取消行动或离开 / 结束战斗。定向战斗合同 `11/11`、行动实例 `16/16`、统一 Foundation `21/21` 通过。
- `GrowthRecipe` 的玩家结果可由现有保留参与卡、使用次数和生成卡牌结果组合表达；`ExplorationRecipe` 可由地点参与条件、权威随机结果分支与生成结果表达。两者不需要恢复专用配方子类、类别枚举或直接世界副作用。
- `ResearchRecipe` 的候选池现在由行动作者源显式声明“待解锁行动 + 对应配方卡”，不扫描全局类型，也不恢复特殊 Recipe 子类。行动开始时冻结候选池，完成时从 `ScenarioRun` 当前尚未发现项中使用牌桌权威随机流选择，生成配方卡并写回唯一发现集合；全部发现后行动照常结算但不再生成新配方卡。模板其它自动发现入口仍需按实际效果继续复现和验证，不能因临时蓝图设想直接排除。
- **Research 验收**：行为用例覆盖已发现项过滤、活动行动快照冻结候选、对应配方卡生成、发现事实写入和全部发现后不再生成；行动结算 `14/14`、行动实例 `16/16`、剧本单局 `14/14`、全量 EditMode `493/494`（零失败、1 条环境条件跳过）、全量 PlayMode `45/45`。
- **新鲜验收**：战斗合同 `11/11`、行动实例 `16/16`、发现合同 `8/8`、统一 Foundation `21/21`；最终全量 EditMode `492/493`（零失败、1 条环境条件跳过），最终全量 PlayMode `45/45`。PlayMode 首轮的一次真实鼠标点击超时未在单条 `1/1` 和随后全量 `45/45` 中复现，因此只登记为测试输入稳定性观察项，不宣称已定位或修复根因。

## 激进重构优先清单

| 优先级 | 要先处理的旧实现 | 处理方式 |
|--------|------------------|----------|
| P0 | `Resources.LoadAll` 内容入口 | 第一模块替换为 Gameplay 作者源契约和派生索引；后续资源 / Mod 模块再把 YooAsset 包发现接入作者源，参考素材可临时导入，但正式运行不从 `Resources` 扫内容。 |
| P0 | `CardDefinition` 大一统字段 | 先拆出 Gameplay 最小 schema：身份、显示、资源引用、分组 GAS 标签、对象种类、运行时状态边界。 |
| P0 | `CardCategory` / `QuestType` / `CombatType` 等枚举语义 | 用 EX-GAS GameplayTag 引用/查询和规则模块替换；技术层枚举只保留稳定基础类型。 |
| P0 | `CardInstance` 多职责运行时 | 第三模块拆为 Card View + Runtime Entity Projection；表现不保存规则真相。 |
| P1 | `CardManager` 上帝类和单例链路 | 整体不吸收；内容索引复用现有 owner，牌桌状态、视图生成和空间解算按真实职责建立，不允许一个 Manager 继续兜底全部功能。 |
| P1 | 堆叠自动触发全部规则 | 第三模块只提交空间意图，第四模块再解释可行动作；堆叠只是输入姿态。 |
| P1 | 固定 QuestType / Encounter / DayCycle 类型 | `QuestType` 枚举排除；Quest 父级生命周期吸收为 Gameplay 任务系统。Encounter / DayCycle 继续等待剧本事件与世界规则 pipeline 裁决。 |
| P2 | JSON 扫档和场景名存档 | 存档模块按 run/meta/scenario/mod dependency 重做。 |

## 联机架构约束（StackCraft 没有，搬迁时必须补）

知识库已在《卡牌生存：无限》中记录“单机或联机都成立”和“联机可加入叛徒机制”。因此搬 StackCraft 架构时，不能把它的单机假设带进正式地基。

| 约束 | StackCraft 现状 | Gameplay 搬迁要求 |
|------|----------------|------------------|
| 控制权 | 本地玩家默认能直接拖动和操作所有可交互卡牌，代码里没有玩家、席位、控制者和授权命令边界。 | 行动请求必须能表达发起玩家、控制者、目标对象、授权结果和拒绝原因；单机只是一个玩家拥有友方控制权的特例。 |
| 同步与回放 | Manager 直接改运行时状态，`RecipeDefinition.Execute`、战斗、遭遇和随机产出会立即产生副作用。 | 正式规则优先产生命令 / 事件 / 效果结果；状态变化可同步、可回放、可调试。 |
| 随机 | 多处直接使用 `Random.Range`，没有剧本种子、玩家可见随机和服务器权威随机边界。 | 剧本随机、战斗随机、探索随机、叛徒/秘密目标随机要能种子化，并能区分公开随机和隐藏随机。 |
| 可见性 | UI 和 Manager 默认能访问完整局内状态。 | UI 只能读取当前客户端可见信息；秘密目标、隐藏身份、未公开事件和私人手牌/道具不能通过全局状态泄漏。 |
| 叛徒与秘密目标 | 无对应模块。 | 先作为架构约束进入 Quest / WorldRule / Save / UI 边界，不在当前阶段实现完整玩法。 |
| 断线与恢复 | 存档是本地 JSON 槽位，未区分局内快照、联机会话和玩家重连。 | 存档和运行时快照要能表达剧本状态、玩家席位、Mod 依赖、可见性和未完成命令。 |

## 明确可吸收清单

| 优先级 | 保留项 | 吸收方式 |
|--------|--------|----------|
| P0 | 拖拽、拆堆、合堆、桌面边界和重叠解算 | 进入 Gameplay `Tabletop Runtime`，重构为表现/运行时分离。 |
| P0 | 桌面行动进度、暂停/恢复、进度 UI | 进入 Gameplay 的桌面行动进度小模块，服务探索、采集、建造、研究、危机处理等行动。 |
| P0 | 配方冲突检测和概率提示 | 进入关卡编辑器 / 内容校验器，升级为 GAS 标签条件、世界规则和蓝图条件冲突检测。 |
| P1 | 研究蓝图、探索产出、允许额外材料、消耗模式 | 进入行动 / 配方设计，改为可解释的 GAS 标签条件和行动条件解释小模块。 |
| P1 | 目标监听事件流和目标解锁链 | 进入剧本目标模块，扩展为通关目标、当前目标、危机目标、秘密目标和成就。 |
| P1 | 跨日规则结算 | 首个真实规则出现后按其作者源、状态和交互需求裁决；不预建世界规则 pipeline。 |
| P2 | 冲突区 / 工作区视觉组织 | 作为桌面表现参考，支持战斗、狩猎、机甲工位、多人协作行动等。 |
| P2 | 临时卡牌素材、Prefab、进度条和音效 | 原型阶段经适配层使用，不写入正式内容 ID。 |

## 明确不吸收清单

| 不吸收对象 | 原因 | 正式职责归属 |
|------------|------|------------|
| StackCraft 的 tag-like 枚举体系 | `CardCategory` / `QuestType` / `CombatType` 等不是可扩展标签系统，Mod 会被迫改代码。 | EX-GAS GameplayTag + Gameplay GAS 标签查询 / 内容标签。 |
| StackCraft 战斗结构 | `CombatManager`、`CombatStats`、`CombatType`、本地命中 / 暴击结算和战斗状态会与 GNS/EX-GAS 的属性、Ability、GameplayEffect、Tag 和 Cue 职责冲突。 | GNS/EX-GAS 数值与效果链 + Gameplay 牌桌战斗对象；模板数值可临时映射，旧结构不吸收。 |
| `Resources.LoadAll` 内容入口 | 不支持 YooAsset 内容包、Mod 依赖、覆盖顺序、版本和热更边界。 | YooAsset 内容包加载 + 内容作者源清单。 |
| 大一统 `CardDefinition` | 字段混合身份、显示、战斗、食物、装备、交易、职业变化，未来 100 个剧本会互相污染。 | 组合式内容 schema。 |
| `RecipeDefinition.Execute` 直接改世界 | 作者源配置直接执行运行时副作用，不利于校验、联机、回放和解释。 | 行动条件解释小模块产生命令 / 事件 / GAS 效果请求。 |
| GameCore 旧 `Recipe` / `CraftingStation` 作为正式配方系统 | 只覆盖背包制作站交易，不覆盖桌面角色行动、地点工位、GAS 标签条件、蓝图门槛和世界规则；与 StackCraft 配方会形成双职责。 | Gameplay 新行动 / 配方职责；旧实现只参考交易原子性和 UI 反馈。 |
| 全局单例串联核心状态 | 多个 `public static Instance` 默认本地全权控制，缺玩家席位、授权、同步和测试边界。 | 进程级启动唯一归 `GameManager`；牌桌、行动、目标、存档等状态由后续具体模块分别建立正式 owner，不新增总管单例。 |
| 未种子化随机 | 多处直接 `Random.Range`，无法区分公开随机、隐藏随机和可回放随机。 | Scenario RNG / Rule RNG / Hidden RNG。 |
| 固定场景名旅行 | `Title/Main/Island` 和 Build Settings 不应成为剧本 ID。 | ScenarioDefinition + 场景模板映射 + YooAsset 场景加载。 |
| JSON 全目录扫档 | 缺局内/局外分层、Mod 依赖快照、内容缺失处理和版本迁移。 | RunSave / MetaSave / ScenarioState。 |
| 模板 URP / Graphics / PlayerPrefs 设置 | 只能作为模板自带配置或视觉参考，不能接管 Gameplay 全局设置。 | Gameplay 项目设置职责。 |

## 近期执行口径

- 第二模块已经通过本次“未照搬 StackCraft”回审；正式 owner 是 `GameManager` / `AGameSystem` / `EventKit` / `SceneKit` / `ResourceSystem`，不恢复已删除的 `RuntimeContext` 小框架。现有 GameCore 宽职责不冒充最终最佳实践，但不在没有真实阻塞时另起第二入口。
- 第一模块的回审订正已完成：通用内容基类不再提供卡面，`GameplayInteractableDefinition` 已删除，卡牌专用卡面只属于 `CardDefinition`；基础包全量加载和跨包重复 ID 仍是后续资源 / Mod 职责的明确缺口。
- 第三模块当前 3.1-3.4 已按最新执行卡完成回审；现有测试只证明可堆叠卡牌状态、空间解算、牌桌视图、正式输入拖拽和真实 YooAsset 链路成立，不越权解释成全部牌桌形态模型。
- 第四模块 4.1-4.12 已完成当前吸收切片：已经形成单一行动作者源、参与条件、显式候选选择、唯一请求启动入口、回合消耗唯一进度真相、牌桌状态原子结果、权威随机、参与条件失效中断、发现状态过滤、行动作者源校验，以及普通运行 / 暂停行动的可恢复快照；战斗实时链保持独立。完整能力边界与角色 GAS 快照限制以 `gameplay-foundation-reaudit.md` 的“活动行动快照恢复实施结果”为准。库存、完整蓝图系统、地图、EX-GAS 结果、正式文件存档、网络传输、玩家授权和 Mod API 仍需按真实 owner 逐项裁决，不能从现有卡牌切片越权推导。

### 模块 10.3 权威随机复审（2026-08-13）

- 不建立新的网络随机服务。权威随机已经属于 `ScenarioRun`、地区 `Tabletop`、活动 `Battle` 和行动实例各自的真实生命周期，不应再由模块 10 复制一套全局随机状态。
- 行动随机分支在行动开始时选定并写入行动实例；牌桌快照保存地区随机流状态，恢复后继续同一序列。战斗从牌桌随机流派生独立种子，并为每次 Ability / GameplayEffect 提供确定性种子；战斗按产品裁决不进入存档。
- FishNet 接入后由服务器 / 主机推进这些随机流并提交正式结果。公开结果可发送给所有可见客户端；秘密目标、叛徒信息或隐藏检定只发送给有权查看的席位。客户端不得通过同步根种子自行重演隐藏规则，也不得用本地预测决定正式结果。
- 当前代码已经满足单机、确定性测试、非战斗存档恢复和未来服务器权威接管所需的 owner 边界，因此 10.3 当前地基范围完成。真正尚未实现的是 10.2 的席位 / 可见性事实和 10.4 的 FishNet 消息、快照与重连协议。
- 第四模块统一测试场景已使用新框架和唯一请求入口复现当前已审计的模板功能，并通过定向与全量回归；模板结构中的自动制作、旧候选直接执行、固定场景名、`CraftingManager`、`CraftingTask` 和 `isContinuous` 没有进入正式链路，但对应玩家效果仍须按模板完整清单逐项验证，不能因旧结构被排除而自动删除。第五模块当前重排的 5.1 世界回合时间线与 5.2 天数 / 日程边界已完成回审；文中旧 5.2-5.9 记录的是任务生命周期、状态事实、剧本归属、按天任务、内容发现和任务子项自建状态的历史实施切片，统一等待现行 5.3 回审。错误的独立遭遇系统和任务中央类型工厂均已删除；其它任务事实、剧本事件、一次性历史、真实跨日规则与 Mod API 仍未吸收。
- Gameplay 正式卡牌、行动和后续节点模块不得直接依赖 StackCraft 的 `CardManager`、旧单例链、固定场景名或 `Resources` 作为真相。
- 当前主线是 **StackCraft 架构搬迁 / 吸收审查**；Gameplay 的职业、技能树、叛徒和原创生存内容只作为边界约束，不作为本阶段实现目标。
- 需要吸收参考模板素材时，必须先迁入项目自有资源目录并改成中文现实名称；只有仍留在参考区的原件才允许保留旧路径，且不能被正式 Gameplay 链路直接读取。
- 每个模块开工前，都要补一份“旧实现替换清单”：参考来源、重构范围、删除/隔离对象、临时适配删除条件、验收方式。
- 参考模板可以保留在 `Assets/StackCraft/` 用来对照手感，但 Gameplay 正式实现必须迁入自己的正式职责入口。
