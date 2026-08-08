---
name: stackcraft-system-reference-matrix
description: StackCraft 架构吸收审查表：按依赖顺序重排模块，先建立 GamePlay 数据定义与内容契约，再逐块重构吸收 StackCraft，并纳入 UI 框架与联机约束。
metadata:
  type: feature
  status: 设计中
---

# StackCraft 模块吸收审查表

> 命名迁移说明（2026-08-07）：正式模块已从历史 `GamePlay` 迁移为 `Gameplay`，并拆分为 Content、Actions、Tabletop、Scenarios 命名空间。本文中旧日志、已删除脚本和历史验收记录保留其原始名称，不能作为当前作者入口或 API 命名依据。

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

## 正确模块顺序

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
- 本表后续吸收 StackCraft 时，以玩家可见功能对照为主：参考拖拽手感、跨阶段行动、目标监听、日结节奏、UI 反馈和编辑器校验，但每项都要重新裁决是否进入当前游戏。参考类、字段和流程不自动保留；大一统 `CardDefinition`、枚举分类、`Resources.LoadAll`、直接副作用执行、固定场景名和 RPS 战斗规则明确排除。

按依赖顺序，GamePlay 吸收 StackCraft 时应拆成 10 个一级审查模块；一级模块只用于排序，真正开工时必须继续拆成可逐项判断的小子模块：

| 顺序 | 架构审查模块 | 为什么排在这里 | StackCraft 主要参考 | 总裁决 |
|------|--------------|----------------|--------------------|--------|
| 1 | 内容定义总包：作者源 / 加载 / 查询缓存 / 校验 | 所有运行时对象、牌桌表现、行动、目标、存档和 UI 都依赖统一 ID、类型、标签、资源引用和包边界；本模块必须继续拆成 1.1-1.10 小步审查。 | `CardDefinition`、`PackDefinition`、`RecipeDefinition`、`Quest`、`EncounterDefinition`、`Resources` 目录。 | **基础链与 2026-08-05 回审订正已完成；通用内容基类不再承担卡面，可交互空壳类型已删除。** |
| 2 | 启动流程 / 系统协作 / 单局状态边界 | StackCraft 用多个单例 Manager 串联流程；当前项目已有 `GameCore.GameManager`、`AGameSystem` 生命周期、`EventKit`、资源/Mod/GAS 初始化，必须先审查这些职责能否重构承接。 | `GameDirector`、`CardManager`、`CraftingManager`、`QuestManager`、`TimeManager`、`DayCycleManager`。 | **已收口：复用正式 owner，排除单例链。** |
| 3 | 可堆叠卡牌运行时 / 卡牌视图 / 堆栈交互 | 有了内容契约和启动/单局状态边界后，才能安全实例化卡牌视图、堆栈和桌面卡牌。 | `Board`、`CardController`、`CardInstance`、`CardStack`、`CardPhysicsSolver`、`StackingRulesMatrix`。 | **3.1-3.6 已跑通 StackCraft 可堆叠卡牌体验；回审后明确不得把该模型扩张为全部牌桌对象的通用状态。** |
| 4 | 行动选择 / 配方条件 / 桌面行动进度 | 依赖内容定义和牌桌投放事件，解释“谁对什么对象做什么行动”，以及这段行动如何计时、暂停、取消和完成。 | `CraftingManager`、`CraftingTask`、`RecipeDefinition`、`ExplorationRecipe`、`ResearchRecipe`、`TravelRecipe`、`GrowthRecipe`。 | **4.1-4.10 已完成替换清单、单一作者源、参与条件、候选选择、回合真相 / 即时换算、原子结果、权威随机、参与条件失效中断、发现过滤和作者源校验；4.11 尚未开始。** |
| 5 | 剧本 / 目标 / 时间 / 世界流程 | 依赖行动结果和内容事件，组织胜负条件、危机、日结和多世界规则；模板 Encounter 只作为剧本事件触发问题的参考片段。 | `QuestManager`、`EncounterManager`、`DayCycleManager`、`TimeManager`、`GameDirector` 的流程片段。 | **先建立父级归属，再改造吸收流程；不照搬 Manager 或类型枚举。** |
| 6 | 交易 / 卡包 / 经济闭环 | 这是 StackCraft 成品闭环的一部分，但在 GamePlay 中属于剧本可选规则，不能抢内容包和商店职责。 | `PackDefinition`、`PackSlot`、`PackVendor`、`TradeManager`、`CardBuyer`、`TradeZone`。 | **参考闭环，延后接管。** |
| 7 | 战斗 / 冲突区 / Stats / 装备 / 职业变化 | 这里审查的是 StackCraft 现有战斗和装备架构，不是实现 GamePlay 的职业技能系统。 | `CombatManager`、`CombatTask`、`CombatRect`、`CardCombatant`、`CombatStats`、`StatModifier`、`classChangeResult`。 | **规则排除，表现参考。** |
| 8 | UI 框架 / 界面状态绑定 / 作者工具 | UI 框架本身属于架构：它决定反馈如何订阅状态、行动如何确认、作者如何发现配置冲突。 | `InfoPanel`、`ProgressUI`、`CardStatsUI`、`QuestsView`、`RecipesView`、`ModalWindow`、各 Definition Editor。 | **吸收模式和工具体验，重做数据绑定。** |
| 9 | 存档 / 运行时状态恢复 | 存档应在运行时边界清楚后接入，否则会把 StackCraft 的场景名、卡牌 ID 和 JSON 扫档固化成债务。 | `GameData`、`SceneData`、`StackData`、`CardData`、`SaveSystem`。 | **只参考状态范围，重做存档职责。** |
| 10 | 联机适配约束 | StackCraft 没有联机模块，但 GamePlay 明确支持联机；搬架构时必须预留控制权、同步、随机、可见性和秘密目标边界。 | 无直接模块；反向审查单例、随机、全局可见状态和直接副作用。 | **新增硬约束，不做玩法实现。** |

## 第一模块小步拆分

第一个一级模块不能再作为一个“大数据系统”一次性开工，按下面小步逐项审查和落地：

| 子模块 | 审查问题 | StackCraft 参考 | GamePlay 裁决 |
|--------|----------|----------------|---------------|
| 1.1 唯一 ID | 一个内容对象到底用什么身份被引用、存档、Mod 覆盖和编辑器校验？ | `Quest.id` 会自动生成 GUID 字符串，`RecipeDefinition.Id` 被任务和发现列表引用。 | 只保留一个作者可控内容 ID；Unity GUID、YooAsset 地址和文件路径只作定位，不作第二 ID。 |
| 1.2 SO 作者源 / 内容元信息 | 哪些字段属于多个作者源共同需要，哪些字段必须拆到具体类型？ | `CardDefinition` 把显示、战斗、食物、装备、交易、职业变化等塞在一起。 | 保留 SO 作者源，并使用狭窄技术基类统一稳定身份、最小展示信息和标签；它不是所有玩法数据的业务父类。卡牌专用卡面由 `CardDefinition` 提供，其它表现资源留在对应真实作者源。 |
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
| 1.2 SO 作者源 / 内容元信息 | `ContentAsset` | 唯一 ID、最小展示信息、标签和索引接入方向保留；通用卡面字段已删除，技术基类不再承担牌桌表现。 |
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
| 2.2 单局状态边界 | 一局游戏里哪些服务、状态和引用应该集中，哪些应留给已有系统？ | `GameDirector.GameData`、`CardManager` 的卡堆列表、`CraftingManager` 的任务列表、`QuestManager` 的目标状态。 | **已完成架构裁决，暂不新增代码容器。** 服务生命周期与单局状态生命周期分开；只有出现真实新局状态和正式流程入口时，才创建具体的单局状态对象。 |
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
| 2.2 单局状态边界 | 已完成架构裁决和立即可做的生命周期重构 | 不预建泛化上下文；系统装配范围、状态启停幂等和读档完成时机已修正，`AGameSystem` 职责拆分继续留给 2.4。 |
| 2.3 内容索引接入 | `GameCore.ResourceSystem.LoadAssetsByAssetTagAsync<T>`、`ContentRegistrySystem`、`FoundationTest` | 资源发现和包优先级归 `ResourceSystem`；内容索引由真实的 `AGameSystem` 持有加载句柄并在启动时构建；不新增手工内容清单、`ContentCatalog` 或 GamePlay 加载器。 |
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
3. **`ContentRegistrySystem` 是内容索引的生命周期持有者。** 它是现有 `AGameSystem` 的真实子系统，不是启动壳：在资源和 Mod 初始化完成后加载内容，调用 `ContentIndex.Build`，在销毁时释放加载句柄。`GameManager` 不保存索引副本，也不直接依赖 GamePlay 类型。
4. **`gameplay-content` 只是 YooAsset 构建清单标签。** 它用于资源发现，不是 EX-GAS GameplayTag，不是内容 ID，不参与规则查询、存档或联机引用。
5. **构建期自动收集取代人工登记。** `ContentAssetFilterRule` 以 `ContentAsset` 继承关系过滤 `Assets` 下的作者资产，写入 `gameplay-content` 标签；作者只创建一次 SO，不需要同时登记内容清单、地址或 GUID。
6. **内容资源禁用地址生成。** YooAsset 收集规则使用 `AddressDisable`，因为内容唯一身份已经由 `ContentId` 承担；YooAsset 地址只在需要加载具体表现资源时出现，不能成为内容定义的第二 ID。
7. **跨包重复内容 ID 直接失败。** 默认包和 Mod 包的内容会一起进入 `ContentIndex.Build`；如果 Mod 与基础包声明相同 ID，校验失败，不静默覆盖。覆盖 / 替换语义留到 Mod 冲突规则真正裁决时再设计。
8. **当前内容会话随进程启动建立。** 本小步不实现启用 / 禁用 Mod 后的运行时索引重建，也不宣称支持 Mod 热重载；动态刷新需要先有明确的 Mod 会话生命周期和索引替换时机，不能让一部分旧索引和一部分新包并存。

#### 本轮实现与删除

| 类型 | 内容 |
|------|------|
| 新增 | `ResourceSystem.LoadAssetsByAssetTagAsync<T>` 和其跨包句柄状态；`ContentRegistrySystem`；YooAsset 自定义 `ContentAssetFilterRule` 过滤规则；地基测试场景中的 `地基测试卡牌` 作者资产；真实 PlayMode 测试。 |
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
| 进程级基础 / 表现 | `ContentRegistrySystem`、`AudioSystem`、`InputSystem`、`TransitionSystem`、`UISystem` | 继续由 `GameManager` 显式装配和启停；不接收地图父类回调。 |
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
5. **增加一次性的最终释放阶段。** `OnSystemStop` 仍服务组件禁用 / 重新启用时的监听注销；`OnSystemShutdown` 只在进程关闭或启动失败时释放初始化阶段持有的长期资源。`ContentRegistrySystem` 的 YooAsset 内容句柄已从私有 `OnDestroy` 转到该统一阶段。
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
| 战斗 / 冲突区 | `CombatManager`、`CombatTask`、`CombatRect`、`CardCombatant`、`CombatStats`、`HitResult` | 把敌我卡牌拖入战斗矩形，按攻速、命中、暴击和三系克制持续结算。 | 战斗规则是 StackCraft 自己的小系统；与 GamePlay 的 GAS 能力、效果、Tag 和 TargetCatcher 职责重叠。 |
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
| `CardManager` | 同时承担内容扫描、定义索引、Prefab 选择、实例工厂、堆栈注册、重叠解算、发现记录、存档、旅行恢复、装备恢复、营养统计、进食日结和事件源，是典型上帝类。 | 活跃牌桌对象需要统一登记、创建/移除反馈、视图工厂和解算调度这一事实可以保留。 | 整个类不作为单元吸收。内容索引继续归 `ContentRegistrySystem`；资源与 Prefab 加载归 `ResourceSystem`；牌桌状态、视图生成、空间解算分别按真实职责建立；发现、存档、日结、装备和战斗延后到各自模块。不得保留 `public static Instance`。 |
| `StackingRulesMatrix` | 用 `CardCategory x CardCategory` 枚举矩阵决定 `None / CategoryWide / SameDefinition`，新增 Mod 类型必须改代码和矩阵尺寸。 | 只保留“拖拽时需要查询候选是否可接收，并据此高亮”的交互体验。 | 矩阵和 `StackingRule` 枚举不进入正式架构。牌桌不自行解释 GAS 标签或配方；后续行动/交互模块返回可接受关系，牌桌只展示候选并提交投放意图。 |
| `Highlight` / `ProgressUI` | 高亮临时创建子物体和材质实例；进度条直接读取 `CraftingTask` 并跟随堆栈世界位置。 | 可接受目标的即时高亮、世界锚点进度反馈。 | 高亮归卡牌视图状态，不能每次临时制造规则对象；进度条属于第四模块行动进度与第八模块 UI 绑定，本模块只保留世界锚点参考，不引用 `CraftingTask`。 |

#### 与当前项目职责对比

| 职责 | 当前正式 owner 或候选 | 3.1 裁决 |
|------|-----------------------|----------|
| 静态卡牌内容 | 当前实现为 `CardDefinition`、`ContentIndex`、`ContentRegistrySystem` | 唯一内容身份、索引和正式加载入口继续复用；卡牌专用 `CardArt` / `Artwork` 已收口到 `CardDefinition`，其它内容不再被迫提供卡面。 |
| 资源和 Prefab 加载 | `GameCore.ResourceSystem`、`SoftAssetReference` | 直接复用正式加载入口。卡牌视图 Prefab 的作者字段在视图工厂真正实施时裁决，不在 3.1 提前加字段。 |
| 原始玩家输入 | `GameCore.InputSystem`、Unity 新 Input System | 继续作为唯一输入 owner。当前 `Click` 仍带 FantasyWord 点击移动语义，后续牌桌输入接入必须重构正式 owner，而不是增加 `StackCraftInput`。 |
| 领域事实事件 | YokiFrame `EventKit` | 直接使用。没有真实订阅者和结果语义前不新增“卡牌已投放”空事件。 |
| 旧世界交互 | GameCore `IInteraction` / `IInteractionTarget` | 目前绑定 `CharacterBase` 和旧世界命令执行，不足以接管卡牌对地点、技能对卡牌或填槽交互；保留为候选对比，第四模块再决定重构、迁移或删除，不做桥接。 |
| 牌桌卡牌运行时状态 | 3.1 审查时尚无；3.2 已由 `TabletopCardState` 接管 | 已建立局内卡牌、底到顶堆栈顺序、牌桌位置和放置锁定的唯一真相；控制权与玩家身份等正式命令消费者出现后再扩展，不提前加空字段。 |
| 卡牌视图与空间解算 | `TabletopCardLayout`、`TabletopCardView`、`TabletopCardViewProjector`、`TabletopCardOverlapSolver` | 3.3-3.4 已建立正式 owner；视图只表现状态，布局和空间解算保持纯算法，资源创建/释放继续复用 `ResourceSystem`。 |

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

#### 下一小步

3.2 只裁决牌桌卡牌运行时状态与堆栈模型：局内卡牌的最小状态、实例身份消费者、堆栈顺序与拆分/合并不变量，以及谁有权提交位置变化。它不实现输入、视图动画、行动、配方、战斗或存档。

### 3.2 牌桌运行时状态与堆栈模型（2026-08-05）

#### 当前项目对比与正式裁决

| 候选来源 | 当前能力 | 3.2 裁决 |
|----------|----------|----------|
| StackCraft `CardInstance` / `CardStack` | 直接用 `MonoBehaviour` 引用表示成员关系，没有独立局内卡牌 ID；堆栈同时调用 Manager、制作、销毁和 Tween。 | 只吸收有序堆栈、拆分与合并行为；不吸收视图引用、单例、副作用和隐含顺序。 |
| GameCore `Persistable` / `Entity` | 用场景对象、Prefab、持久化标识和旧 `CharacterBase` 交互支撑 FantasyWord 世界实体。 | 不复用为牌桌对象基类。它绑定 MonoBehaviour、场景恢复和旧交互职责，无法作为纯牌桌状态；也不新增包装或桥接。 |
| `ContentId` | 作者维护的唯一静态内容身份。 | 继续作为卡牌引用的内容定义 ID；同一内容可以创建多张不同局内卡牌。 |
| GamePlay 3.2 新增状态 | 此前不存在。 | `TabletopCardId`、`TabletopCard`、`TabletopCardStack`、`TabletopCardState` 已完成卡牌专用命名订正，共同承担可堆叠卡牌实例、成员关系和位置真相；不再声称覆盖所有牌桌形态。 |

#### 实现入口

| 类型 | 唯一职责 | 关键边界 |
|------|----------|----------|
| `TabletopCardId` | 当前标识一局可堆叠卡牌状态中的一张卡牌。 | 由当前状态从 `1` 开始自动分配，`0` 无效；作者不填写，不替代 `ContentId`，不复用 Unity InstanceID 或随机 GUID。是否成为未来其它桌面形态的共享实例 ID，要等非卡牌消费者出现后裁决。 |
| `TabletopCard` | 保存当前局内卡牌 ID 与静态内容 ID。 | 不保存生命、装备、行动、战斗、可见性、存档块或视图引用；固定工位和圆形节点不能因位于牌桌上就塞进本类型。 |
| `TabletopCardStack` | 保存一个卡牌堆栈的底到顶顺序、牌桌二维位置和位置锁定状态。 | 顺序唯一规定为索引 `0` 是底部、最后一个是顶部；成员列表对外只读，位置没有公开 setter。 |
| `TabletopCardState` | 一局可堆叠卡牌状态的唯一写入口，维护卡牌、堆栈和“卡牌属于哪个堆”的派生索引。 | 纯 C#、非单例、不继承 `AGameSystem`，不发空事件；未来由真实单局流程持有。所有拆堆、合堆和位置提交都必须经过它。 |

#### 已锁定行为

1. 同一个 `ContentId` 可以创建多张局内卡牌，每张卡牌得到不同的 `TabletopCardId`。
2. 新卡牌先形成一个独立单卡堆栈；位置属于堆栈，不在每张卡牌上重复维护。
3. 合堆时目标成员和顺序保持在下方，来源堆按原顺序整体追加到上方，最终位置使用目标堆位置。
4. 从某张卡牌拆堆时，该卡牌及其上方卡牌形成新堆；下方卡牌留在原堆，两个堆继承拆分前的位置。
5. 从底部卡牌开始拆分等价于移动整个现有堆栈，不制造第二个空堆或临时哨兵卡牌。
6. 位置锁定只禁止整个卡牌堆栈移动或作为合堆来源；锁定底牌上方的卡牌仍可拆成新的未锁定堆栈。它只表达固定卡堆，不再被解释成固定工位或固定地点模型。
7. 合并和拆分后，由 `TabletopCardState` 同步更新唯一成员索引；调用方和视图不允许维护第二份堆栈关系。

#### 联机、Mod 与存档边界

- `TabletopCardState` 是状态提交边界，不等于已经实现服务器、玩家席位或网络传输。未来命令层先校验玩家控制权，再调用它提交变化；客户端拖拽预览不能直接写入正式状态。
- `TabletopCardId` 当前只保证单个 `TabletopCardState` 作用域内唯一。存档恢复、网络快照和事件回放出现后，扩展的是权威状态的创建/恢复入口，不新增第二种局内卡牌 ID。
- Mod 作者继续只维护 `ContentId` 和内容资产；局内卡牌 ID 由运行时分配，避免 Mod 内容和每局实例双重登记。
- 本小步不建立存档 DTO、网络命令、玩家 ID、随机源、可见性字段或 Mod 状态容器；没有消费者前不冻结这些合同。

#### 验收证据

- `TabletopCardStateEditModeTests` `5/5` 通过，覆盖同内容多实例、合堆顺序与目标位置、中间拆堆、固定底座可拆离、锁定来源禁止合堆。
- `GamePlay.Runtime` 和 `GamePlay.EditModeTests` 使用 Unity `6000.5.4f1` 当前 Bee/Roslyn 响应文件编译通过。
- 全量 EditMode 共 `310` 项：`309` 通过、`1` 条既有条件跳过、`0` 失败，证据为 `TestResults-Module32-Final.xml`；跳过项是 UnitySkills 对缺失可选包的既有条件忽略，与 3.2 无关。
- 正式实现不引用 `CryingSnow.StackCraft`、`Persistable`、`Entity`、`EventKit`、`GameManager`、`MonoBehaviour`、`GameObject` 或 Tween。

#### 下一小步

3.3 只处理牌桌二维空间、可玩边界、禁放区域和纯重叠解算：吸收 `Board` / `CardPhysicsSolver` 的几何算法并修正同中心零位移、确定性顺序和未收敛结果。它仍不实现卡牌视图、输入拖拽、行动或配方。

### 3.3 牌桌二维空间、边界与重叠解算（2026-08-05）

#### 吸收与重构结果

| StackCraft 参考 | 吸收内容 | 正式实现差异 |
|-----------------|----------|--------------|
| `Board.ClampToBounds` | 按完整卡牌占地夹取，而不是只检查中心点。 | `TabletopCardPlacementArea` 使用纯二维 `Rect`，不依赖 SkinnedMesh、Transform、CardManager 或顶部固定边距。 |
| `Board` 顶部禁放区 | 可玩区域内存在不可放置区域。 | 禁放区改为任意数量矩形，供 HUD 保留区、固定工位、冲突区或地图节点布局使用。 |
| `CardPhysicsSolver` AABB 最小穿透轴 | 选择较小穿透轴、锁定对象只推动另一侧、双方可动时平均分摊。 | `TabletopCardOverlapSolver` 只处理值类型快照，输出不可变结果，不调用 Board、Tween、战斗区或牌桌状态。 |
| StackCraft 有限迭代 | 避免对象链式推动时无限循环。 | 结果显式返回 `Converged` 和迭代次数；锁定冲突或次数耗尽不会被误报为完成。 |

#### 正式合同

- `TabletopCardPlacementArea` 是牌桌二维边界和禁放矩形的不可变配置，拒绝非有限坐标和无效宽高。
- `TabletopCardSpatialBody` 是一次解算使用的卡牌占地快照：局内卡牌 ID、中心位置、尺寸和锁定状态。
- `TabletopCardOverlapSolver` 先按 `TabletopCardId` 排序，再处理边界、禁放区和卡牌间重叠；调用方输入顺序不会改变结果。
- 两张卡牌中心完全重合时，低 ID 稳定向负 X、高 ID 向正 X，修复 StackCraft `Mathf.Sign(0)` 返回零位移但仍报告已处理的问题。
- 锁定卡牌只作为权威阻挡，不为了视觉整齐被解算器偷偷移动；两张锁定卡牌重叠时返回未收敛。
- 解算结果不会自动写回 `TabletopCardState`。后续牌桌运行时协调者必须先确认权威，再通过 `MoveStack` 提交位置；客户端预览不能越过这一边界。

#### 注释与维护边界

- 所有公开类型和公开方法已补中文 XML 注释，说明输入限制、失败方式、返回语义和副作用。
- 同中心稳定方向、锁定卡牌不移动、最终收敛复核和分离向量语义均在对应复杂分支旁说明原因；简单循环和赋值不写流水账注释。

#### 验收证据

- `TabletopCardOverlapSolverEditModeTests` `5/5` 通过：完整占地夹取、同中心稳定分离、锁定/可动对象分离、双锁定未收敛、禁放区推出。
- 正式空间代码不引用 StackCraft、MonoBehaviour、GameObject、Transform、Physics、Collider、CombatRect 或 Tween。

#### 下一小步

3.4 只建立卡牌视图投影、堆栈布局和视图创建职责：视图读取 `TabletopCardState` 与内容索引，展示卡面和堆栈位置，但不拥有运行时状态，也不处理输入、投放规则或行动结算。

### 3.4 卡牌视图投影、堆栈布局与视图创建（2026-08-05）

#### StackCraft 吸收与排除

| 参考来源 | 吸收内容 | 正式实现差异 |
|----------|----------|--------------|
| `CardSettings.StackStep` | 同一堆栈按固定三维步进展示顶部成员。 | 收敛为 `TabletopCardLayoutParameters.StackVisualStep`；允许按相机方向配置正负深度，只影响表现，不回写牌桌位置。输入阈值、经济、AI、粒子和战斗设置不进入本配置。 |
| `CardInstance.Initialize` | 绑定内容显示名、卡面资源和视图对象。 | `TabletopCardView` 只保存局内卡牌 ID 与内容 ID，拒绝不一致绑定；不保存生命、营养、装备、制作、战斗、堆栈或存档状态。 |
| StackCraft 卡面 Mesh / `_OverlayTex` 用法 | 允许临时原型继续使用 Renderer 材质属性显示卡面纹理。 | 正式视图使用可配置 Shader 属性名和 `MaterialPropertyBlock`，不复制材质实例，也不把 StackCraft Shader 属性写死为 GamePlay 默认。纯 Sprite 预制体可直接使用 `SpriteRenderer`。 |
| `CardManager` 类别到 Prefab 映射与 `Instantiate` | 牌桌需要统一创建、同步和回收视图。 | 不吸收类别枚举、Prefab 字典或上帝类。`TabletopCardViewProjector` 只按局内卡牌创建一个通用视图预制体；差异化卡牌表现等真实需求出现后再建立明确扩展点。 |

#### 正式职责

- 卡牌投影现在只接受 `CardDefinition`，并使用其卡牌专用 `Artwork`；独立卡面未配置时回退到通用 `Icon`。通用 `ContentAsset` 不再提供牌桌首选图片，非卡牌内容也不会被投影器静默当成卡牌。
- `TabletopCardPresentationSettings` 是牌桌表现配置作者源，只保存现有 `SoftAssetReference<GameObject>` 预制体地址、每层三维视觉步进和基础排序值；它不是内容目录、Prefab 注册表或第二资源加载器。
- `TabletopCardLayout` 是纯计算入口，把 `TabletopCardStack` 的二维位置和“底部索引 0”顺序转换为局部三维位置与渲染顺序。
- `TabletopCardView` 是 Unity 表现组件，只接受身份一致的 `TabletopCard` 与 `CardDefinition`，只修改自身 Transform、SpriteRenderer 或 Renderer 属性块。
- `TabletopCardViewProjector` 只读 `TabletopCardState.Stacks` 与 `ContentIndex`；内容 ID 若指向非卡牌作者源会显式失败。视图实例统一通过 `ResourceSystem.InstantiateAsync` 创建，实例和图片句柄由投影器持有并通过 `ResourceSystem` 释放。
- `TabletopCardState.Stacks` 是实时只读视图；视图和输入层不能维护第二份堆栈成员关系，也不能通过 Transform 反向修改正式位置。

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
| `CardController` 指针按下、拖动、释放流程 | 按下位置、拖拽距离阈值、相机射线到牌桌平面投影、释放时形成一次结果。 | `TabletopCardDragSession` 是设备无关状态机；`TabletopCardDragInput` 只监听正式输入 owner，不直接读取 `Mouse.current`、`Pointer.current` 或 `UnityEngine.Input`。 |
| `CardStack` 拖拽尾段与阻尼 | 从选中成员到堆顶形成临时拖拽预览，首张立即跟随，尾牌指数阻尼追赶。 | 预览只改 `TabletopCardView` 的 Transform；不拆堆、不改 `TabletopCardState`，释放后恢复权威布局。 |
| `CardController` 最近目标与高亮 | 拖拽时排除来源堆，在其它卡牌视图中选择空间命中候选并显示高亮。 | 高亮只表示空间候选，不调用 `StackingRulesMatrix`，也不宣称行动、配方或规则接受该目标。 |
| `CardController.DropCard` 的投放结果 | 释放时保留来源卡牌、按下位置、释放位置、是否拖拽和候选卡牌。 | 收敛为 `TabletopCardPointerReleaseIntent`，由显式传入的真实消费者接收；没有消费者时拒绝绑定，不发送空事件，也不直接合堆、交易、装备、开战或执行配方。 |

#### 正式职责与边界

- `TabletopCardDragSession` 只判定一次主指针交互是点击还是拖拽，并产出不可变释放意图；它不依赖 Unity 输入设备、场景对象或事件系统，可供鼠标、触屏、回放和未来联机命令入口复用同一阈值规则。
- `TabletopCardDragInput` 负责输入监听、卡牌视图命中、牌桌坐标投影、拖拽预览和空间候选高亮。它不能提交正式牌桌变化，也不能解释目标可接受性。
- `TabletopCardState.TryGetStackContaining` 只为输入和视图处理“对象可能刚被权威状态移除”的正常竞态；正式状态提交仍只能经过 `TabletopCardState` 的明确操作。
- `TabletopCardPointerReleaseIntent.TargetCardId` 是候选卡牌，不是已批准目标。第四模块的行动解析或未来权威命令消费者负责校验控制权、规则条件和最终状态变化；非卡牌目标不会被硬塞进这个字段。
- 所有新增公开类型、公开方法、复杂生命周期和 Inspector 字段均使用中文注释说明职责、失败方式和副作用；不以流水账注释重复代码。

#### 验收证据

- `TestResults-Module35-GamePlay.xml`：GamePlay EditMode `18/18` 通过，其中新增拖拽会话测试覆盖点击阈值、拖拽阈值和来源堆排除后的候选目标。
- `TestResults-Module35-InputAsset.xml`：输入作者源合同 `1/1` 通过，确认正式动作图为 `Gameplay` / `UI` / `None`，且不存在重复动作或绑定 ID。
- 正式输入与牌桌代码不引用 `CryingSnow.StackCraft`、`StackCraftInput`、`UnityEngine.Input`、`Mouse.current`、`Pointer.current`、第二事件层或第二输入资产。
- 真实鼠标/触屏交互、YooAsset 卡牌视图实例化、碰撞命中、高亮和释放消费者仍留到 3.6 统一测试场景验收，不能以 EditMode 通过代替。

#### 下一小步

3.6 只收口统一测试场景：在 `FoundationTest` 中装配真实卡牌视图 Prefab、牌桌表现配置、内容索引、正式输入和只记录释放意图的测试消费者，验证完整交互链路；仍不实施行动、配方、装备、交易、战斗或原创剧本规则。

### 3.6 统一测试场景与真实牌桌链路（2026-08-05）

#### 场景与作者资产

- 统一测试场景仍是 `Assets/Scenes/FoundationTest.unity`，没有新增正式启动场景、StackCraft `Title` 入口或第二套测试框架。
- 场景装配 `PlayerInput`、`GameCore.InputSystem`、`ContentRegistrySystem`、`TabletopCardViewProjector`、`TabletopCardDragInput`、正交相机和卡牌 `BoxCollider`，完整走当前项目正式输入、内容和资源职责。
- 新增真实测试作者资产 `Assets/GamePlay/Tests/牌桌/牌桌测试表现设置.asset` 与 `Assets/GamePlay/Tests/牌桌/牌桌测试卡牌视图.prefab`；卡面临时复用 StackCraft 的 `Assets/StackCraft/Sprites/Square.png`，只作为可替换原型素材，不成为 GamePlay 内容身份或正式美术路径规范。
- 场景、测试 Prefab 和临时图片都由现有 YooAsset Collector 收集；运行时仍通过 `SoftAssetReference` / `ResourceSystem` 创建视图和加载图片，没有新增 GamePlay 资源加载封装。
- `GamePlay/地基/重建测试场景` 会重建固定测试场景和资产、写入 Build Settings 与 YooAsset 测试收集项，并在保存后回读关键引用；它只服务地基验收，不是关卡编辑器或正式剧本入口。

#### StackCraft 手感吸收结果

- `FoundationTestSceneHarness` 创建三张卡组成的来源堆和一张独立候选卡，验证同一内容 ID 可以产生多张独立局内卡牌。
- 正式拖拽从来源堆的中间卡牌开始，首张立即跟随，顶部尾牌按表现配置阻尼追赶；没有在预览阶段拆分 `TabletopCardState`。
- 指针移动到独立卡牌时只显示空间候选高亮，释放后形成“来源卡牌、按下位置、释放位置、是否拖拽、候选卡牌”的 `TabletopCardPointerReleaseIntent`。
- 测试消费者只记录和输出释放意图。释放后正式牌桌仍保持两个堆栈，证明牌桌输入没有越权执行合堆、行动、配方、装备、交易或战斗。

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
- `Assets/StackCraft/` 继续作为参考源码和临时原型素材区；是否删除参考脚本要在正式能力覆盖与素材依赖清单完成后单独裁决，不能因 3.6 通过直接整目录删除。

## 模块 1-3 回审裁决（2026-08-05）

| 模块 | 是否存在生搬或错误泛化 | 证据 | 最新裁决 |
|------|------------------------|------|----------|
| 1. 内容定义 | **回审发现的问题已完成代码订正。** 此前为了给卡牌投影提供统一图片，把模板的“内容天然有卡面”假设写回了数据根，并把卡牌表现与可交互能力做成平行继承类型。 | `ContentAsset` 已删除通用卡面；`GamePlayInteractableDefinition` 已删除；`CardDefinition` 只保留卡牌专用 `CardArt` / `Artwork`，投影器只接受卡牌作者源。`ContentRegistrySystem` 当前仍按单一资源标签加载全部内容，`ContentIndex` 对跨包重复 ID 直接失败。 | 唯一 ID、SO、EX-GAS 标签、`ResourceSystem` 和派生索引继续保留。当前基础包链路已收口；剧本按需加载、Mod 依赖与覆盖规则仍留给正式 Mod / 资源职责，不得因本轮通过而宣称已解决。 |
| 2. 启动与生命周期 | **没有发现 StackCraft 生搬。** | 正式入口没有 `GameDirector`、并行 `RuntimeContext`、StackCraft Manager 单例链、固定 `Main` 场景或 `Resources.LoadAll`；场景走 `SceneKit`，事件走 `EventKit`，资源走 `ResourceSystem`。 | 本次回审通过“未照搬”检查。`GameManager` 静态系统访问和 `MapSystem` 的宽职责仍是现有 GameCore 债务，不等于最终最佳实践；只有真实流程证明阻塞时再重构，不为审查形式新增第二入口。 |
| 3. 牌桌与卡牌 | **回审发现的错误泛化风险已完成代码订正。** | 状态、空间解算、表现、输入意图、测试和场景装配均已改为 `TabletopCard*` 卡牌专用合同，旧通用名称和兼容别名已清除。`TabletopCardState.CreateCard` 仍建立独立单卡堆栈，因为这是可堆叠卡牌状态的不变量。 | 当前能力正式限定为“可堆叠卡牌子系统”。固定工位、圆形节点、连通节点和其它非卡牌表现不得塞进 `TabletopCardStack`；后续目标形态由真实行动消费者驱动设计。 |

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
| `ResearchRecipe` | 从全局配方列表排除三个具体子类，随机挑未发现配方，生成配方卡并写发现集合。 | 模板证明研究可能与“未发现内容池”发生关系。 | 特殊子类、类型排除列表、全局单例和直接写发现状态全部删除。4.10 只吸收发现状态过滤；研究/蓝图业务归后续世界规则、目标或作者工具裁决。 | 蓝图/发现候选边界归 4.10 与后续目标/存档模块；随机归 4.8。 |
| `TravelRecipe` | 保存场景名字符串列表，完成后消费输入并调用 `GameDirector.InitiateTravel`。 | 模板证明某种行动结果可能请求地区迁移并携带参与者。 | 特殊子类、固定场景名循环和 SO 直接切场景全部删除。只有“行动可产生旅行请求”被选入功能清单时，才接入现有 `MapSystem` / `SceneKit`。 | 4.7 旅行请求候选；正式执行复用 `MapSystem` / `SceneKit`。 |
| `GrowthRecipe` | 用具体子类和结果卡识别种植设施/种子，直接消耗、拆堆、平移、解重叠并生成结果。 | 模板证明一次结果可能包含多个状态变化。 | 特殊子类和直接牌桌操作全部删除。成长功能若被选中，只能作为通用结果组合的验收案例，不能为它恢复 Recipe 继承树。 | 4.7 组合结果候选，由各正式状态系统提交。 |
| `ProgressUI` / `RecipesView` | Manager 创建世界进度条；UI 直接读取任务、配方和发现集合。 | 模板证明玩家需要知道当前行为、剩余阶段或发现内容，并且世界锚点反馈有体验价值。 | UI 类型、Manager 数据绑定和枚举栏目全部不保留。若第四模块最终没有通用进度，就不为了复刻 `ProgressUI` 创建一套进度状态。 | 第 8 模块表现投影；第四模块只提供最终被证明需要的可观察状态。 |
| `RecipeDefinitionEditor` | 扫描项目内全部配方，对“具体卡牌 + 数量”完全相同的资产显示冲突、权重和跳转按钮。 | 模板证明作者需要发现歧义、查看候选概率并定位冲突资产。 | 编辑器实现和“具体卡牌 + 数量”签名全部删除。只有正式作者源和条件模型确定后，才重新设计对应校验。 | 4.10 作者校验与语义签名。 |
| `CraftingData` | 每个卡堆只保存配方 ID 和进度，读档时依赖当前卡堆重新创建任务。 | 模板证明跨阶段行为若存在，就需要恢复。 | 该保存结构全部删除。只有 4.6 确认存在正式跨阶段作业后，4.11 才按真实状态设计快照；不能先假设一定要保存进度。 | 4.11 根据最终运行状态裁决快照边界。 |

#### 当前项目与成熟框架校准

| 现有或成熟职责 | 当前能力 | 第四模块裁决 |
|----------------|----------|--------------|
| `TabletopCardPointerReleaseIntent` | 只记录来源卡牌、按下/释放位置、是否拖拽和候选卡牌，不修改牌桌状态。 | 继续作为空间输入事实；4.4 才把来源与候选解释成可选行动，不直接合堆或启动任务。 |
| `ContentAsset` / `ContentIndex` | 提供唯一内容 ID、展示资源和内容标签码的正式作者/查询入口。 | 后续行动作者源必须复用唯一 ID 和内容索引，不建 `Resources/Recipes` 扫描或第二内容目录。是否建立独立行动 SO 由 4.2 单独裁决。 |
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
10. **功能等价验收是一级模块门禁。** 第四模块全部子模块完成后，必须先在 `FoundationTest` 用新框架复现最终明确选择吸收的 StackCraft 玩家可见功能，并证明已排除的旧结构没有进入正式链路；通过后才能开始第五模块。

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
3. **身份与展示不重复。** 行动直接继承 `ContentAsset`，复用唯一 `ContentId`、显示名、描述、图标和一组 EX-GAS 标签码；不新增 `ActionId`、Recipe ID、分类枚举、第二标签集或手工登记表。
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
- 4.10 已把行动作者源基础形状和同条件多行动提示接入 `ContentValidator`。运行查询仍保留对失效输入的拒绝，但不能再用运行时静默失败代替作者源校验。

### 4.4 行动候选与玩家选择（2026-08-05）

#### StackCraft 参考结论

- `CardController.OnPointerUp` 和 `CardInstance` 的放置链先改变物理卡堆，再调用 `CraftingManager.CheckForRecipe` 扫描全部 Recipe；卡牌拆堆、AI 移动和活动任务变化也会重新扫描。
- 多个 Recipe 匹配时，`CraftingManager` 直接按权重随机选一个并创建 `CraftingTask`。玩家看不到完整候选，也没有明确选择步骤；“空间上能堆”被当成“规则上应该执行”。
- 这套链路只证明放下卡牌后需要反馈可用行为，不证明应该先合堆、全局扫行动、随机替玩家选择或立即开工。GamePlay 的释放意图必须先保持为只读输入事实。

#### 正式查询与选择合同

1. **输入层不拥有行动。** `TabletopCardDragInput` 继续只通过绑定回调提交 `TabletopCardPointerReleaseIntent`；它不引用行动定义、不扫描内容索引，也不新增 EventKit 包装事件。绑定该回调的交互组合 owner 才消费释放事实。
2. **可用行动集合必须显式提供。** `TabletopCardActionCandidateResolver` 只查询调用方传入的 `availableActions`，不遍历 `ContentIndex.AllAssets`。后续地点、工位、剧本、权限和秘密信息 owner 可以先裁剪当前可见行动，再复用同一解析器；测试场景只显式提供 `test.foundation.action`。
3. **来源与目标都是待分配参与者。** 解析器从 `TabletopCardState` 读取局内卡牌，再通过唯一内容 ID 从 `ContentIndex` 取得作者资产；所有输入卡都必须分配进某个 4.3 槽位，否则该行动不是候选。
4. **不新增 Source/Target 内容枚举。** 解析器先处理拖拽来源、再处理命中目标，并在所有合法槽位分配中选择缺少参与者最少的结果；条件和缺口相同的歧义由行动槽位作者顺序稳定裁决。因此同类角色可以用第一个槽位表示发起者、第二个槽位表示目标，异类对象则由内容 ID/标签条件自然区分。
5. **允许待填充候选。** 已提供卡牌全部能进入槽位，但仍未达到某些最少数量时，候选保留并给出 `MissingParticipantCount`。这为《苏丹的游戏》式弹窗补卡提供规则数据；未就绪候选不能提交为行动。
6. **零个、一个和多个候选使用同一返回结构。** 没有匹配时返回空数组；一个候选仍只是一个选项；多个候选保持调用方提供的稳定顺序，供第 8 模块显示按钮或选择面板。重复提供同一行动 ID 时只保留第一次，避免重复按钮。
7. **选择必须引用唯一行动 ID。** `TabletopCardActionCandidateSelector.TrySelect` 只能从本次候选快照按 `ContentId` 取回候选；任意其它行动 ID 都会失败。没有 `CandidateId`、选择索引身份或第二套 Action ID。
8. **单候选不等于自动执行。** 解析器不隐式选择第一个候选，也没有 `IsDefault`、`AutoExecute`、优先级或随机权重字段。默认行为取决于地点/交互提供者当前上下文；自动行为必须由后续明确规则、控制权和权威复核证明，不能成为行动定义的全局布尔值。
9. **候选与选择都没有副作用。** 不移动、拆堆、合堆、锁卡、扣材料、创建任务、发送完成事件或执行结果；候选就绪只表示参与条件已经满足，可以交给后续正式行动入口。
10. **角色 GAS 依赖按需暴露。** 纯物品/地点/静态符号使用 `FindCandidates`，调用方不需要引用 GAS；只有角色运行时 owner 使用 `FindCandidatesWithAbilitySystem` 并提供 `TabletopCardId -> AbilitySystemCell` 解析。两条入口共享同一候选算法，不是两套规则。

#### 当前没有抢先建立的 owner

| 职责 | 4.4 裁决 |
|------|----------|
| 地点/工位提供哪些行动 | 当前具体地点作者源尚未建立，因此不把行动列表塞进 `CardDefinition`，也不全局扫描。未来提供者把已按剧本、控制权和可见性裁剪的行动集合传给解析器。 |
| 默认行动与按钮排序 | 属于提供者上下文和 UI 投影，不是行动自身永久属性；第 8 模块根据候选与提供者数据表现。 |
| 控制权、联机授权、秘密行动 | 候选只是本地可见查询快照，不是服务器授权。4.11 必须按发起玩家和权威状态重新查询，不能同步一个客户端候选就直接执行。 |
| 无行动时移动或合堆 | 属于牌桌放置/堆叠命令策略，不能被“零候选”隐式决定；本步保持原权威牌桌状态不变。 |

#### 实现与验收

- `TabletopCardActionCandidates.cs` 新增不可变槽位绑定、候选快照、确定性解析器和显式选择入口；没有新增 Manager、MonoBehaviour 单例、事件包装、候选 ID、全局行动索引或执行接口。
- `TabletopCardState.TryGetCard` 只开放牌桌局内引用的只读解析，所有成员关系写入仍归原状态 owner。
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
- 遵循全局 `D:\codex-home\AGENTS.md` 的“禁止无依据防护性架构”红线；具体裁决见 `D:\codex-home\skills\improve-codebase-architecture\SKILL.md`。

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
| `TurnTimingDefinition` | 当前世界 / 剧本需要说明普通行动切到即时制后，一个回合单位对应多少游戏秒。 | `SecondsPerTurn` 是唯一换算配置，与具体行动回合消耗正交；即时增量固定为 `deltaTime / SecondsPerTurn`。 |
| 当前 `GameStateSystem` / Unity 游戏时间 | 菜单状态通过 `Time.timeScale` 控制全局游戏暂停，Unity `Time.deltaTime` 自动反映全局暂停与倍速。 | 只在普通行动选择即时模式时提供缩放后的秒数增量；回合制不会因 `Update` 自动推进。 |
| EX-GAS `TurnController` / `GlobalTimer` | 插件当前提供回合计数结构，但没有普通行动每回合秒数配置；当前源码里 `TurnController` 也没有与 `GlobalTimer.Turn` 形成正式推进链。 | 不把 GAS 回合计数冒充世界回合 owner 或时间换算配置。第 5 模块建立正式世界回合流程后，再统一编排普通行动、日结和 GAS 回合推进。 |
| 实时战斗 / EX-GAS 帧链 | 战斗攻击、技能时间轴、Cooldown 和 GameplayEffect 帧持续由实时战斗与 GAS 逻辑帧推进。 | 战斗始终即时，不读取 `TurnCost` 的即时换算速度，也不进入普通行动模式开关。 |
| YokiFrame `ActionKit` | 通用延迟、插值和序列工具，自行注册静态 PlayerLoop，并维护 `ActionID`、`IAction` 状态、Controller 暂停/取消状态和对象池。 | 不用于正式行动作业。套用后仍需另存行动身份、参与者和进度，会形成两份生命周期与第二运行 ID；其异常处理还会记录后回收，不符合内部不变量立即暴露的要求。继续保留给表现和通用短序列使用。 |
| EX-GAS AbilityTask | 在 Ability 激活生命周期内推进角色技能任务并随 Ability 取消或结束。 | 角色技能继续复用 EX-GAS；地点、物品和多人牌桌行动不伪装成 AbilityTask。 |

#### 正式状态与唯一入口

1. **回合消耗属于行动作者源。** `ActionDefinition.TurnCost` 表示普通行动完成所需回合数；`0` 表示显式选择后立即完成。`DurationSeconds` 已删除，作者不能为即时制再维护一份行动耗时。
2. **一次作业只有一个状态。** `TabletopCardActionJobState` 只包含运行、暂停、完成和取消，替代 StackCraft 可同时出现暂停/取消/完成的布尔组合。它是系统完整解释的闭合生命周期，不是需要 Mod 增加成员的内容分类。
3. **即时换算只有一个规则源。** `TurnTimingDefinition.SecondsPerTurn` 表示一个回合单位对应的游戏秒数。它不是第二行动耗时：行动的总工作量仍只有 `TurnCost`，换算规则可以被剧本 / Mod 选择和替换。
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

- 删除 `ActionDefinition.DurationSeconds`、作业 `DurationSeconds / ElapsedSeconds` 和所有行动秒数作者数据；新增 `TurnCost`、`ProgressedTurns` 与 `TurnTimingDefinition.SecondsPerTurn`。
- `TabletopCardActionSystem` 默认回合制，直接消费 `ScenarioTurnConfirmedEvent`；即时制只换算同一进度。它没有引用 StackCraft、ActionKit、AbilityTask、存档、网络或战斗时钟类型。
- 回合真相 RED 为 `Temp/module46-turn-truth-red.log`，当时只因旧模型缺少 `TurnCost`、回合进度、默认模式和直接回合推进入口而失败；5.1 已删除该直接入口，改由世界回合事实驱动。定向 EditMode GREEN 为 `Temp/TestResults-Module46-TurnTruth-Green-R2.xml`，`4/4` 通过。
- MonoBehaviour 曾与纯作业同处 `TabletopCardActionJob.cs`，场景重新打开后无法恢复正式脚本引用；已按 Unity 组件资产规则拆到同名 `TabletopCardActionSystem.cs`，生成器继续严格校验而未增加兜底。最终重建日志 `Temp/module46-turn-truth-rebuild-r2.log` 退出码为 `0`。
- 测试行动只配置 `TurnCost = 2`；测试回合规则只配置 `SecondsPerTurn = 0.35`。定向 PlayMode `Temp/TestResults-Module46-TurnTruth-PlayMode.xml` 为 `2/2`：默认回合制等待现实时间不推进，确认一回合后为 `1/2`，切换即时制后继续同一份进度，并覆盖全局暂停/倍速、作业暂停/恢复、完成和取消。
- 最终回归：`Temp/TestResults-Module46-TurnTruth-GamePlayEditMode.xml` 为 `24/24`；`Temp/TestResults-Module46-TurnTruth-AllEditMode.xml` 为 `329` 通过、`1` 条既有条件不适用跳过、`0` 失败；`Temp/TestResults-Module46-TurnTruth-AllPlayMode-Final.xml` 为 `6/6`。
- 内容作者资产校验为 `3/3`；运行时代码、测试和资产残留扫描未发现 `DurationSeconds`、`ElapsedSeconds` 或 `m_durationSeconds`，普通行动目录仅 `TabletopCardActionSystem` 在即时换算分支读取一次 `Time.deltaTime`；`.spec` lint 通过。
- 4.6 阶段的 PlayMode 退出日志保留了修复前 68 笔 Persistent 分配的历史结论；后续已由 EX-GAS 正式 `Shutdown()` 修复，当前结论与验证路径见 `gamecore-gas.md`。
- 本节不把上述测试越权解释为世界回合 owner、日结、GAS 回合同步、存档、联机、结果结算、工位归属或 Mod 自定义推进已完成。

#### 4.7 牌桌卡牌结果切片（2026-08-06）

当前只吸收 StackCraft 已证明且本项目已有真实状态 owner 的两种结果：移除本次参与槽位中的牌桌卡牌，以及在参与槽位位置生成指定内容 ID 的卡牌。`RecipeDefinition.Execute()`、特殊 Recipe 子类、直接访问 Manager、直接切场景、库存入包、蓝图发现、随机池和 GameplayEffect 均未吸收。

- `ActionDefinition.ResultIntents` 是 SO 作者源中的可序列化参数，不执行副作用；当前内置 `TabletopCardRemoveResultIntent` 与 `TabletopCardCreateResultIntent` 两种具体结构，没有结果类型枚举、结果标签或万能参数字典。
- `TabletopCardActionResultSettlement` 先建立完整计划并校验空意图、未知类型、槽位、失效卡牌、重复移除、产物内容 ID、数量和牌桌 ID 容量；全部通过后才由唯一 `TabletopCardState` 删除和创建，非法作者配置直接抛出异常，不提交半份结果。
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
- 结果结算继续由 `TabletopCardActionResultSettlement` 建立共同结果与已选分支的完整计划，再由唯一 `TabletopCardState` 原子提交。SO、UI、候选解析器和结果意图都不能直接调用随机或修改牌桌。
- 当前没有新增全局随机 Manager、RuntimeContext、随机事件包装、网络 DTO 或 Mod 随机接口。单局种子来源、随机流快照、服务器同步、公开/隐藏随机可见性和断线恢复等到 4.11 根据真实运行状态收口。
- 定向 EditMode `Logs/TestResults-GamePlay-4.8-EditMode-First.xml` 为 `8/8`；统一测试场景使用固定种子和两条 `1:3` 产物数量分支。最终 `Logs/TestResults-GamePlay-4.8-AllEditMode-Final-R2.xml` 为 338 通过、1 条条件不适用跳过、0 失败，`Logs/TestResults-GamePlay-4.8-AllPlayMode-Final.xml` 为 `6/6`；对应日志均未出现 `Leak Detected` 或未释放原生集合。

#### 4.9 连续执行与中断策略（2026-08-06）

- StackCraft 在拖出卡牌时暂停任务，放回原堆时恢复，剩余卡牌仍满足配方时继续，否则取消；完成后若 `isContinuous` 为真或仍有可消费材料，则重新全局扫描配方并自动开始下一任务。
- GamePlay 只吸收“运行中的参与条件变化必须中断”这一职责，不吸收模板的卡堆位置触发、全局配方扫描和自动续作。当前牌桌卡牌位置不是正式工位归属，不能把拖离某个堆等价成离开工位。
- `TabletopCardActionSystem` 现在要求所有牌桌作业绑定当前牌桌状态和内容索引。开始作业时复核候选作者源、槽位顺序、数量、卡牌内容与动态标签；每次回合或即时推进前再次复核。参与卡缺失或不再匹配时，作业在增加进度和提交结果之前取消。
- `TabletopCardActionCancellationReason` 区分显式请求、参与者失效和系统关闭。作者错误、未绑定状态、过期候选或非法状态迁移继续抛出异常，不通过取消原因吞掉内部问题。
- 纯物品/静态标签行动使用 `BindTabletopActionState`，需要角色 GAS 动态标签的行动使用 `BindTabletopActionStateWithAbilitySystem`；测试和非角色模块不因可选参数被迫引用 GAS。
- 自动重复没有正式入口。完成或取消作业离开活动集合；玩家、AI、剧本或未来工位计划若要重复，必须重新获取当前可用行动集合、重新查询候选并调用 `StartAction`，旧进度和旧随机分支不继承。
- 最新策划的“多人缩短耗时、成功判定取最高等级”只记录为后续工位/属性规则需求；4.9 没有用参与卡数量直接改 `TurnCost`，也没有创建倍率表、策略接口或第二进度字段。
- 定向 EditMode `Logs/TestResults-GamePlay-4.9-EditMode-R3.xml` 为 `34/34`；统一测试场景 PlayMode `Logs/TestResults-GamePlay-4.9-PlayMode-R3.xml` 为 `3/3`，覆盖参与卡移除后取消、零进度、无产物和视图刷新。
- 最终全量回归：`Logs/TestResults-GamePlay-4.9-AllEditMode-Final.xml` 共 `341` 条，其中 `340` 通过、`1` 条条件不适用跳过、`0` 失败；`Logs/TestResults-GamePlay-4.9-AllPlayMode-Final.xml` 为 `7/7`。两次运行均启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，对应日志未出现 `Leak Detected` 或未释放原生集合。

#### 4.10 发现 / 蓝图边界与作者源校验（2026-08-06）

- StackCraft 把“已发现卡牌 / 已发现配方”保存在 `CardManager` 与 `CraftingManager` 的集合中；`ResearchRecipe` 与 `PackSlot` 可以从未发现配方池里随机抽取并写入发现集合；`RecipesView` 只显示已发现配方；`RecipeDefinitionEditor` 对完全相同材料的配方显示冲突、权重和跳转按钮。
- Gameplay 只吸收其中两个地基职责：一是“当前局内发现状态会过滤可展示 / 可选择的行动”，二是“作者源进入运行时索引前应发现引用断裂和同条件歧义”。研究随机、配方卡生成、蓝图 UI、RecipesView、配方存档和创意工坊 / Mod API 均不在本步实现。
- 新增 `ContentDiscoveryState`，只保存已发现的 `ContentId`；发现未知或无效 ID 直接抛错，不创建占位记录。新增 `ActionDiscoveryFilter`，只按发现状态过滤调用方已经提供的可用行动集合，不全局扫描内容索引，也不判断位置、工位、队伍权限或世界规则。
- `FoundationTestSceneHarness` 现在先把测试行动标记为已发现，再把过滤后的行动传给候选解析器；统一测试场景因此证明行动候选链路已消费发现门槛，但不代表正式蓝图、研究或 UI 列表已经完成。
- `ContentValidator` 现在校验行动槽位键、槽位数量范围、允许内容 ID、内容 / 动态 GAS 标签码、结果意图引用的槽位、产物内容、产物数量、随机分支键和权重。未知产物和非法随机权重从“作业开始后失败”前移为“内容索引构建前失败”，符合作者源错误早暴露原则。
- 同参与条件的多个行动只生成 `ACTION_CONDITION_SIGNATURE_SHARED` 警告，不阻止索引建立；这是为了保留“拖拽后弹出多选项按钮”的核心交互，而不是把所有同条件行动强行合并成随机结果。
- 定向发现 / 校验 EditMode `Logs/TestResults-GamePlay-4.10-EditMode-First.xml` 为 `4/4`；GamePlay EditMode `Logs/TestResults-GamePlay-4.10-EditMode-R3.xml` 为 `38/38`；牌桌 PlayMode `Logs/TestResults-GamePlay-4.10-PlayMode-R1.xml` 为 `3/3`。
- 最终全量回归：首次全量 EditMode `Logs/TestResults-GamePlay-4.10-AllEditMode-Final.xml` 因 UnitySkills 自测在资产仍更新时先返回“正在编译或更新资产”而非时长范围错误，失败 `2` 条；资产稳定后重跑 `Logs/TestResults-GamePlay-4.10-AllEditMode-Final-R2.xml` 为 `344` 通过、`1` 条条件不适用跳过、`0` 失败。`Logs/TestResults-GamePlay-4.10-AllPlayMode-Final.xml` 为 `7/7`。两次最终通过运行均启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，日志未出现 `Leak Detected` 或未释放原生集合。

#### 4.11 请求复核、活动作业快照与统一场景收口（2026-08-07）

- StackCraft 的 `CraftingData` 只保存配方 ID 和秒数进度，并依赖当前卡堆重建任务；它没有玩家席位、联机授权、Mod 依赖、可见性或服务器复核边界。Gameplay 不吸收该存档形状，也不为了未来可能性提前建立完整存档文件、网络 DTO、重连协议或 Mod API。
- `TabletopCardActionRequest` 只保存行动唯一内容 ID、行动内槽位键和当前局内卡牌 ID，不持有 `ScriptableObject`、候选对象、牌桌对象或运行系统引用。`TabletopCardActionSystem.StartAction(request)` 是唯一公开行动启动入口；系统必须用当前 `ContentIndex`、`TabletopCardState` 和可选 GAS 动态标签重新构造候选并复核。
- 未知行动、重复 / 未知 / 缺失槽位、无效或重复卡牌、参与数量不合法、卡牌已经移除、内容或动态标签不再满足等请求都会在创建作业前明确拒绝，不建立影子占用表、旧候选缓存或自动修正分支。内部正式代码和全部测试都不再调用公开的候选启动重载，该入口已经删除。
- `TabletopCardActionJobSnapshot` 只导出当前真实作业已有的行动 ID、回合消耗、已推进回合、生命周期状态、取消原因、已选结果分支和槽位卡牌绑定。快照不提供恢复或写回，不保存文件，不拥有随机流、牌桌状态、发现状态、玩家控制权或 Mod 包依赖；这些必须由后续正式 owner 按完整状态统一设计。
- `FoundationTestSceneHarness` 在玩家显式选择行动后先把候选转换为请求，再通过唯一请求入口启动作业。统一测试场景因此证明本地 UI 候选也不能绕过权威复核，但不代表当前已经实现服务器、网络序列化或断线恢复。
- 本步新增的测试属于实现后的公开契约 / 回归保护，不倒称为严格 TDD：覆盖过期请求拒绝、请求重新构造候选、重复卡牌绑定拒绝和活动作业快照事实。
- 定向 GamePlay EditMode `Logs/TestResults-GamePlay-4.11-EditMode-Final.xml` 为 `42/42`；最终全量 PlayMode 中 `TabletopCardFoundationPlayModeTests` 的统一测试场景 `3/3` 通过。
- 最终全量回归：`Logs/TestResults-GamePlay-4.11-AllEditMode-Final.xml` 共 `349` 条，其中 `348` 通过、`1` 条条件不适用跳过、`0` 失败；`Logs/TestResults-GamePlay-4.11-AllPlayMode-Final.xml` 为 `7/7`。两次运行均启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，日志未出现 `Leak Detected` 或未释放原生集合。

### 5. 剧本目标 / 世界规则 / 事件日结

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `QuestManager` 监听获得、发现、击败、制作、买卖、装备、探索、时间、天数等事件；`EncounterManager` 按日期、优先级、概率、一次性和卡牌数量限制触发遭遇；`DayCycleManager` 把一天结束拆成通知、喂食、卖超额卡、遭遇、新一天。 |
| Gameplay 职责归属 | 剧本定义、目标模块、局内事件流和世界规则阶段管线共同承担。剧本定义胜负目标、当前目标、危机目标、秘密目标、事件池、地点池、规则模块和日/回合阶段。 |
| 裁决 | **改造吸收。** 事件监听和日结阶段节奏值得吸收，但具体 Quest / Encounter 类型不吸收。 |
| 保留范围 | 目标激活/完成/解锁链、统一事件监听、剧本事件触发问题、日结阶段锁输入和弹出关键选择。遭遇筛选与一次性记录只有在剧本事件模型成立后才决定归属。 |
| 重构方向 | 日结不写死为喂食/卖卡/遭遇，而是剧本装配的 pipeline：饥饿、寒冷、污染、精神、秘密目标、危机倒计时、地图刷新都作为规则模块插槽。 |
| 排除范围 | 不吸收固定 `QuestType`；不吸收英文 modal 文案；不把 `Title/Main/Island` 或 Build Settings 场景列表当正式剧本入口。 |

#### 5.1 世界回合事实与确认（2026-08-07）

- **参考实现覆盖范围**：StackCraft `TimeManager` 同时持有实时秒数、时间倍率、当前天数和日开始 / 日结束事件；`DayCycleManager` 订阅日结束并固定执行通知、喂食、卖卡、遭遇、新一天五个阶段。这个参考证明了世界流程需要一个可被其它系统消费的时间事实，但它把时间、日结、UI、输入锁、卡牌副作用和存档绑在了多个单例里。
- **吸收**：Gameplay 建立 `ScenarioTurnSystem`，只持有当前已确认的世界回合编号；`ConfirmTurn()` 是唯一确认入口，编号从 0 开始，确认后递增并直接通过 YokiFrame `EventKit.Type` 发布 `ScenarioTurnConfirmedEvent`。
- **删除 / 不保留**：不复制 `TimeManager`、`DayCycleManager` 的单例、实时秒数字段、`Time.timeScale` 写入、当前天数、固定五阶段、输入锁、通知弹窗、喂食、卖卡、遭遇执行、自动保存和 StackCraft 日结事件。目标、遭遇、日结和世界规则仍未进入正式代码。
- **现有职责重构**：`TabletopCardActionSystem` 删除公开 `AdvanceTurn()`，在 `AGameSystem` 启停生命周期中直接注册 / 注销 YokiFrame 类型事件；回合制时消费世界回合事实推进普通行动，即时制时继续只按 `TurnTimingDefinition.SecondsPerTurn` 换算同一份回合进度。行动系统不再持有世界当前回合数，也不代行日结。
- **系统装配**：`TabletopCardActionSystem` 声明 `ScenarioTurnSystem` 为真实启动依赖，统一测试场景生成器把两个系统装配到同一个 `GameManager`。测试和正式代码都不再通过行动系统的旧直接入口推进回合。
- **事件边界**：`ScenarioTurnConfirmedEvent` 是领域事实载荷，直接使用 `EventKit.Type`，不是 `GameRuntimeEvents`、不是新的事件总线，也不是包装转发层；本事件不记录 UI、回放、联机或存档数据。
- **验收**：`Logs/TestResults-GamePlay-5.1-EditMode-Final.xml` 为 GamePlay EditMode `44/44`；`Logs/TestResults-GamePlay-5.1-AllEditMode-Final.xml` 共 `351` 条，其中 `349` 通过、`2` 条既有 UnitySkills Package Manager 条件跳过、`0` 失败；`Logs/TestResults-GamePlay-5.1-AllPlayMode-Final.xml` 为 `7/7`。全量运行启用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，未出现 `Leak Detected`、未释放 Native Collection 或编译错误。
- **下一步边界**：5.1 只建立“回合确认事实”。任何目标激活、遭遇筛选、日结阶段、天气、饥饿、危机、胜负条件或存档恢复，都必须先对照参考源码和当前正式职责重新拆分，不能把它们追加进 `ScenarioTurnSystem`。

#### 5.2 任务定义与生命周期（2026-08-08 订正中）

- **参考实现覆盖范围**：StackCraft `Quest` / `QuestInstance` / `QuestManager` 提供锁定、激活、进度、完成和后继解锁；FantasyWord 已有 `JournalSystem -> Quest -> QuestTask -> QuestTaskProgress`，证明玩家可见任务应是父级，子目标 / 条件只是任务内部结构。
- **原方案订正**：此前新增 `ObjectiveDefinition / ObjectiveSystem` 属于抽象错位，把 Quest 父级降成了目标集合。该路线已撤销，旧验证日志只保留为历史证据，不再证明当前设计有效。
- **吸收**：新增 `QuestDefinition`，复用唯一内容 ID、展示信息和 EX-GAS 标签，声明单向 `PrerequisiteQuestIds` 与内部 `QuestTaskDefinition[]`；新增 `QuestSystem`，持有当前单局任务集合的 `Locked / Active / Completed` 状态和任务子项运行进度。
- **删除 / 不保留**：不保留 `QuestType`、目标卡牌 / 配方固定字段、`QuestsToUnlock`、活动 / 完成双列表、跨 Manager 事件订阅、即时状态回写 SO、存档字段和任务 UI。后继关系只从前置任务 ID 派生，避免作者双重更新。
- **作者源校验**：统一内容校验器在索引建立前拒绝无效、未知、错类型、重复、自引用和循环前置任务；选入本局的任务集合若缺少某个前置任务，也会在提交任何运行状态前直接报错。
- **运行时边界**：剧本 / 关卡未来负责选择本局任务集合；任务系统只接收这组唯一内容 ID 和正式内容索引，不扫描所有内容自动开任务。`CompleteQuest()` 只接收后续任务子项或剧本流程已经确认的完成事实，不解释获得、制作、击败、时间、数值或胜负规则。
- **联机 / Mod 校准**：SO 只保存不可变作者定义，局内状态只在单局任务系统中存在；未来服务器或房主权威只需同步任务集合、任务状态和任务子项进度，不需要同步 Unity 对象引用。Mod 可以新增任务定义和任务图，但新任务子项类型仍要等正式任务 API，当前不冒充已经开放。
- **验收**：本轮 Quest 重构后的 Unity 回归待补；旧 Objective 相关 RED / GREEN / 全量日志不再作为当前有效验收证据。

#### 5.3 任务状态变化事实（2026-08-08 订正中）

- **参考实现覆盖范围**：StackCraft `QuestManager` 分别通过 `OnQuestActivated` 与 `OnQuestCompleted` 通知 `QuestsView` 和 `TradeManager`，但事件归 Manager 私有回调列表所有，消费者继续依赖模板单例和 `QuestInstance` 运行对象。
- **吸收**：新增 `QuestStatusChangedEvent`，统一表达任务唯一内容 ID、变化前状态和变化后状态；`QuestSystem` 完成状态提交后直接通过 YokiFrame `EventKit.Type` 发布，不新增事件总线、转发器或回调注册表。
- **事务边界**：开始任务集合时，全部任务和根任务状态先提交，再发布根任务激活事实；完成任务时，完成状态与满足前置的后继激活状态先全部提交，再按“完成原因在前、解锁结果在后”的因果顺序发布。同步订阅者查询任务系统时不会读到中间态。
- **删除 / 不保留**：不保留两套激活 / 完成 C# 事件、`QuestInstance` 对象载荷、Manager 单例订阅、任务进度事件、UI 更新、交易解锁、存档或网络消息。事件没有第二状态副本，也不允许订阅者回写任务状态。
- **任务条件延后理由**：模板的进度监听写死卡牌、制作、交易、时间与 `QuestType`；当前正式领域尚未提供这些完整事实，立即建立通用条件注册表只会猜测接口。5.3 因此只吸收已有稳定状态事实，不冒充所有任务条件或进度已经完成。

#### 5.4 剧本父级与任务组合生命周期（2026-08-08 订正中）

- **参考实现覆盖范围**：StackCraft 没有正式剧本聚合根。`GameDirector` 以场景名代表当前进度，`QuestManager` 和 `EncounterManager` 分别通过单例持有自己的运行状态，`DayCycleManager` 再直接串起遭遇阶段。这是小型单剧本模板的组织方式，不适合多世界、Mod、联机和关卡编辑器。
- **原方案订正**：此前直接从 `EncounterManager` 抽出 `EncounterDefinition` 与 `EncounterSystem`，等于在父级尚未成立时复制了来源工程的目录边界。该实现、测试和“5.4 遭遇候选已完成”的知识记录均已删除。
- **吸收**：新增最小 `ScenarioDefinition`，当前只组合已实现的任务 ID；新增 `ScenarioDirector`，只持有活动剧本身份，并统一开始 / 结束 `QuestSystem` 的任务集合。这个父级边界吸收的是模板 `GameDirector` 对单局流程的聚合作用，不复制固定场景名、存档槽、旅行卡牌或单例事件。
- **显式装配**：`ScenarioDirector` 通过序列化引用明确拥有 `QuestSystem`，并声明任务系统为启动依赖；不再使用隐藏全局查询。任务集合开始 / 结束入口收窄为程序集内部，外部只能通过活动剧本改变这组状态。
- **作者源校验**：剧本任务列表拒绝无效、重复、未知、错类型和缺失前置任务。当前不添加地图、事件池、天气、世界规则或初始内容空字段，等待对应职责从参考模块中逐步成立后再组合。
- **删除 / 不保留**：不保留 `EncounterType`、`EncounterDefinition`、`EncounterSystem`、固定日期筛选、优先级、概率、友好模式、卡牌数量限制、一次性记录、通知弹窗、随机坐标、生成卡牌、镜头和粒子执行。它们不是本步已经确认的稳定模块。

#### 5.5 剧本父级接管世界回合生命周期（2026-08-08 订正中）

- **参考实现覆盖范围**：StackCraft `GameDirector` 统一开始新局、读档、旅行和返回标题，`TimeManager` 则单独持有当前天数与时间并向外发布日结束。它证明单局时间事实必须受当前游戏流程约束，但两个单例之间没有正式父子生命周期，时间仍可脱离当前剧本独立存在。
- **现有实现问题**：5.1 的 `ScenarioTurnSystem.ConfirmTurn()` 原本是公开入口，5.4 的 `ScenarioDirector` 只拥有任务集合。即使没有活动剧本，外部也能直接确认世界回合；这不是正常业务失败，而是入口职责没有收口。
- **吸收 / 重构**：`ScenarioDirector` 显式引用并依赖 `QuestSystem` 与 `ScenarioTurnSystem`。开始剧本时统一重置回合编号并开始任务集合，玩家 / UI / 网络命令只能通过 `ScenarioDirector.ConfirmTurn()` 确认当前活动剧本的回合，结束剧本时统一清除任务集合和回合编号。
- **单一真相**：`ScenarioTurnSystem` 只保存 `ConfirmedTurnIndex`，确认和重置入口为程序集内部；它不保存第二份活动剧本布尔值。活动剧本是否存在只由 `ScenarioDirector.ActiveScenarioId` 判断，避免父子模块各维护一套状态。
- **统一测试场景**：新增 `test.foundation.quest` 与 `test.foundation.scenario` 两个 SO 作者资产。`FoundationTestSceneHarness` 从正式内容索引开始测试剧本，PlayMode 通过剧本父级确认回合并驱动既有普通行动；场景生成器同时校验任务和回合两条显式引用。
- **删除 / 不保留**：不添加日期、实时秒数、暂停锁、倍速、日开始 / 结束事件、固定日结阶段、喂食、卖卡、遭遇、通知弹窗或自动保存；不新增 `ScenarioRuntimeContext`、回合包装事件或第二套回合状态。

#### 5.6 行动完成任务子项与任务推进（2026-08-08 订正中）

- **参考实现覆盖范围**：StackCraft `QuestManager` 订阅 `CraftingManager.OnCraftingFinished`、`OnExplorationFinished` 等具体事件，按固定 `QuestType` 修改 `QuestInstance.CurrentAmount` 并在达标后完成任务。它证明“已成立的玩法事实驱动活动任务进度”值得吸收，但固定类型分支和跨 Manager 单例监听不适合本项目。
- **吸收**：新增 `TabletopCardActionCompletedEvent`，只在普通牌桌行动成功结算后直接通过 YokiFrame `EventKit.Type` 发布行动唯一 ID；新增多态任务子项作者入口，首个 `ActionCompletionQuestTaskDefinition` 声明具体行动 ID 与所需次数；`QuestSystem` 消费事实并持有唯一次数状态。
- **因果边界**：行动结果先全部提交，再发布完成事实；结算异常、取消和参与者失效都不发布。任务处理时先确定本次事实到达前已激活的匹配任务，再完成任务与解锁后继，因此同一次行动不会同时推进刚解锁的后继任务。
- **分类与标签**：没有新增 `QuestType` 或任务条件枚举。`ActionCompletionQuestTaskDefinition` 的运行时类型表示“如何解释事实”，行动 ID 表示精确引用；若未来真实需求是“任意探索类行动”，应新增使用 EX-GAS 层级查询的具体任务子项解释器，而不是把行动类别重新做成枚举或本地标签系统。
- **作者校验**：无效 / 未知 / 错类型行动引用和非正完成次数在内容索引建立前失败；未登记的任务子项子类也明确失败。当前不开放自定义任务子项注册表或 Mod 代码执行入口，内容型 Mod 只能配置已经内置的任务子项类型。
- **删除 / 不保留**：不保留模板 `QuestType`、目标卡牌 / 配方 / 时间字段、`QuestInstance` 通用整数、跨 Manager 订阅、进度 C# 事件、完成历史表、UI、存档或网络消息；不新增防重复事件 ID、已处理事件表或兜底夹取次数。
- **统一测试场景**：测试任务配置为“完成一次 `test.foundation.action`”。正式行动成功结算后任务变为完成；参与卡失效导致行动取消时任务保持活动，证明测试场景跑的是新框架同一条事实链，不是直接调用任务完成入口。
- **下一步边界**：其它任务事实、复合任务子项、进度读取 / 变化、剧本事件、一次性历史和世界规则阶段仍需逐项裁决；不能把 `QuestManager` 余下分支一次翻译成任务条件大全。

### 6. StackCraft 战斗 / Stats / 装备 / 职业变化（未来 GAS 边界）

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `CombatManager` 建立战斗矩形，把敌我卡牌从桌面移入冲突区；`CombatTask` 按时间推进命中、暴击、闪避和三系克制；`CombatRect` 处理队列布局和逃跑/合并表现；装备通过 `StatModifier` 和 `classChangeResult` 改变单位能力或身份。 |
| Gameplay 职责归属 | EX-GAS Ability / GameplayEffect / GameplayTag / TargetCatcher 负责技能、伤害、状态、目标捕获和表现 Cue；角色系统负责职业、经历、控制权、阵营和跨世界成长。 |
| 裁决 | **规则排除，表现参考。** 当前只审查 StackCraft 这部分架构和冲突边界，不把 Gameplay 职业技能树作为本阶段实现目标。 |
| 保留范围 | “冲突区/工作区”表现可以参考：把狩猎、谈判、机甲工位、战斗准备等正在结算的对象暂时组织到一个桌面区域。 |
| 排除范围 | 不吸收 RPS 战斗规则、`CombatType`、`CombatStats`、命中/暴击职责、投射物逻辑、装备直接变职业和独立战斗状态机；这些会和 GAS / 角色正式职责冲突。 |

### 7. 存档 / UI 框架 / 作者工具

| 对比项 | 结论 |
|--------|------|
| StackCraft 内核 | `GameData` / `SceneData` 保存卡堆位置、卡牌动态数值、装备、任务进度、战斗、商店、遭遇、时间、发现卡牌和发现配方；`InfoPanel`、`ProgressUI`、`QuestsView`、`RecipesView` 形成可玩反馈；`RecipeDefinitionEditor` 能检测同材料配方冲突。 |
| Gameplay 职责归属 | GameCore / YokiFrame SaveKit 候选 + Gameplay 正式 `RunSave`、`MetaSave`、`ScenarioState`、`ModDependencySnapshot`；正式 UI 框架 + 关卡编辑器 + 内容校验器。 |
| 裁决 | **存档只参考状态范围；UI 框架模式和编辑器校验体验可吸收；素材只作原型。** |
| UI 说明 | 旧文档里的“UI 投影”口径不准确。这里要审查的是 UI 框架本身：界面如何订阅运行时状态、如何分优先级显示信息、如何把任务进度贴到桌面对象、如何让目标/配方列表随事件更新。 |
| 保留范围 | 局内卡牌实例、任务进度、目标进度、遭遇一次性记录、发现内容、角色装备和场景/地点状态的保存范围；信息请求优先级、任务/配方列表刷新、配方冲突提示、同条件多结果概率展示、点击定位冲突资产、进度条贴牌桌。 |
| 排除范围 | 不吸收 `Application.persistentDataPath` 全目录扫档、不吸收文件名槽位逻辑、不把场景名作为存档主键、不吸收 StackCraft 数据编辑器为正式关卡编辑器，不让 UI 直接拥有规则真相。 |

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
| P1 | 日结阶段 pipeline | 进入世界规则阶段管线，饥饿只是第一条规则模块。 |
| P2 | 冲突区 / 工作区视觉组织 | 作为桌面表现参考，支持战斗、狩猎、机甲工位、多人协作行动等。 |
| P2 | 临时卡牌素材、Prefab、进度条和音效 | 原型阶段经适配层使用，不写入正式内容 ID。 |

## 明确不吸收清单

| 不吸收对象 | 原因 | 正式职责归属 |
|------------|------|------------|
| StackCraft 的 tag-like 枚举体系 | `CardCategory` / `QuestType` / `CombatType` 等不是可扩展标签系统，Mod 会被迫改代码。 | EX-GAS GameplayTag + Gameplay GAS 标签查询 / 内容标签。 |
| StackCraft 战斗规则 | RPS、命中、暴击、攻速和战斗状态会与 GAS 的 Ability / GameplayEffect / Tag 职责冲突。 | EX-GAS + Gameplay 冲突规则模块。 |
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
- 第三模块 3.1-3.6 已完成卡牌专用命名与职责收窄；现有测试只证明可堆叠卡牌状态、空间解算、视图投影、正式输入拖拽和真实 YooAsset 链路成立，不越权解释成全部牌桌形态模型。
- 第四模块 4.1-4.11 已完成当前吸收切片：已经形成单一行动作者源、参与条件、显式候选选择、唯一请求启动入口、回合消耗唯一进度真相、牌桌状态原子结果、权威随机、参与条件失效中断、发现状态过滤、行动作者源校验和活动作业只读快照；战斗实时链保持独立。库存、完整蓝图系统、地图、EX-GAS 结果、完整存档恢复、网络传输、玩家授权和 Mod API 仍需按真实 owner 逐项裁决，不能从现有卡牌切片越权推导。
- 第四模块统一测试场景已使用新框架和唯一请求入口复现最终选择吸收的功能，并通过定向与全量回归；明确排除的自动制作、旧候选直接执行、固定场景名、`CraftingManager`、`CraftingTask` 和 `isContinuous` 没有进入正式链路。第五模块当前完成 5.1 世界回合事实、5.2 目标生命周期、5.3 目标状态变化事实、5.4 剧本父级 / 目标组合生命周期、5.5 剧本父级接管世界回合生命周期与 5.6 指定行动完成次数要求；错误的独立遭遇系统已经删除，其它目标事实、剧本事件和日结仍未吸收。
- Gameplay 正式卡牌、行动和后续节点模块不得直接依赖 StackCraft 的 `CardManager`、旧单例链、固定场景名或 `Resources` 作为真相。
- 当前主线是 **StackCraft 架构搬迁 / 吸收审查**；Gameplay 的职业、技能树、叛徒和原创生存内容只作为边界约束，不作为本阶段实现目标。
- 需要临时原型素材时，只能通过有删除条件的适配层读取，不能把旧路径写成长期事实。
- 每个模块开工前，都要补一份“旧实现替换清单”：参考来源、重构范围、删除/隔离对象、临时适配删除条件、验收方式。
- 参考模板可以保留在 `Assets/StackCraft/` 用来对照手感，但 Gameplay 正式实现必须迁入自己的正式职责入口。
