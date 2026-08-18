---
name: gameplay-foundation-reaudit
description: CardLoop Gameplay 地基模块 1-6 的 OOP、生命周期、作者源和参考等价重审记录。
metadata:
  type: feature
  status: 设计中
---

# Gameplay 地基全量重审

## 当前阅读口径

本文件保留模块 1-6 的历史回审过程，用来追溯“为什么要从平铺系统改回领域对象”。其中 2026-08-16 以前写下的“尚未实现 / 未吸收 / 缺口”只代表当时切片状态，不能自动当成当前事实。

当前 StackCraft 阶段 C 的最新玩家效果覆盖、排除项和待 Unity 补跑项，以 [`stackcraft-system-reference-matrix.md`](stackcraft-system-reference-matrix.md)、项目根目录 `task_plan.md` 和 `progress.md` 为准。已被后续实现覆盖的典型旧缺口包括：角色卡 ASC 长期快照、牌桌战斗权威随机、实时自动战斗、RPS 克制、投射物前摇、战斗音效、HitUI 式命中反馈、卡牌烟雾和非战斗通用反馈音效。

## 审查结论

2026-08-09 起，前面“模块已完成”“架构已收口”“最佳实践”的表述全部降级为“当前功能切片已验证”。它们只能证明部分玩家可见功能跑通，不能证明对象模型、生命周期 owner、Mod/联机扩展和作者体验已经是最佳设计。

本次重审发现的根本问题不是“小类太多”，而是部分父级对象没有真正拥有状态和行为：

- 运行时单局状态仍有进程级 GameManager / AGameSystem 假设。
- 行动定义、候选查询、作业推进和结果结算已经拆开，但没有形成足够深的行动聚合。
- 内容加载、引用编辑、剧本任务和战斗的权威随机仍有手工 ID、未收口入口或未来集成缺口。
- 测试装配器曾经进入正式 Runtime 和 Build Settings，说明测试边界没有成为架构约束。

## 二次校正：不按文件数量裁决

重审不能继续把“一个类内部做了多件实现细节”直接判成职责过多，也不能把“拆成多个类型”直接判成设计良好。正确标准是公开入口是否小、状态是否集中、删除后复杂度是否会重新散到调用方：

- `TabletopView` 的实例创建、资源句柄、卡面缓存和布局更新都服务同一个牌桌表现生命周期。它是有深度的表现对象，不继续拆分。
- `ActionDefinition` 把参与条件、回合消耗和结果声明组合成一份行动作者数据，本身可以是合理的作者聚合。候选、请求和运行实例分别属于 UI 预览、不可信命令和权威状态，不能仅因类型多就合并；真正的问题是旧运行实例只保存部分事实、完成时重新读取 SO，而且整个活动集合由进程级系统持有。
- `QuestSystem` 已经集中任务集合、前置解锁和子项进度，具备一定深度；问题是它作为进程级 `AGameSystem` 持有单局状态，并通过具体子项类型分支限制 Mod 扩展，不是“类太大”。
- `ScenarioTurnSystem` 只有回合计数、启停和一个事件，删除后复杂度只会回到剧本父级，不会分散到多个调用方。它是应并回单局剧本实例的浅模块。

## 参考对照口径

本轮使用三类本地事实源：

1. StackCraft：GameDirector、Board、CardInstance、CardStack、CraftingTask、QuestManager、EncounterManager。它证明了卡牌、堆栈、任务、制作和时间流程的玩家功能，但其单例 Manager、Resources.LoadAll、直接副作用和场景耦合不作为正式结构。
2. GameCore：GameManager、AGameSystem、系统依赖排序、ResourceSystem、SceneKit 和 YokiFrame EventKit。它是当前进程级服务和生命周期的候选 owner，但 GameManager 的宽职责和静态服务访问仍需按真实单局/联机流程继续审查。
3. EX-GAS：Ability、GameplayEffect、Attribute、GameplayCue、GameplayTag 和生成配置。职业、技能、状态、数值、效果和标签不能在 Gameplay 再造第二套。

比较标准不是“类名像不像”，而是逐条比较入口、权威状态、写入口、生命周期、失败语义、保存/同步点和玩家可见结果。

## 模块重审矩阵

| 模块 | 当前可保留 | 已确认的问题 | 重审裁决 |
|---|---|---|---|
| 1 内容定义 / 加载 / 校验 | `ContentAsset` 技术基类、`ContentId`、`ContentIndex`、SO 作者源、EX-GAS 标签引用、`ResourceSystem` 入口 | 2.1a 已把内容索引迁入单局，但当前只冻结“默认包 + 已启用 Mod 包”；剧本级包依赖、覆盖和游戏内 Mod 编辑器仍未实现 | 保留作者源与索引；后续由正式 Mod 职责组合剧本活动内容范围，让作者工具通过资产选择维护唯一 ID 引用；不能建立第二内容目录 |
| 2 启动 / 系统协作 | `GameManager` 进程级入口、`AGameSystem` 生命周期和依赖排序 | 剧本、任务、回合和行动的单局状态作为多个进程级系统并列存在，父子生命周期只靠互相查找和事件维持 | `GameManager` 继续负责进程基础设施；单局剧本实例集中拥有任务、回合、牌桌和行动状态。当前游戏只需一个活动单局，不为理论上的多房间预建并发容器 |
| 3 可堆叠卡牌 | `ScenarioRun.Tabletop` 统一拥有卡牌状态和牌桌行动；卡牌实例快照保留 ID、堆栈和下一分配号；`TabletopView` 按状态修订自动投影；输入预览不直接写状态 | 普通活动行动可在已恢复牌桌上复核并继续；完整 `ScenarioRun`、角色 GAS 状态和正式文件存档尚未接入，正式 UI 释放消费者尚未定型，因此当前只有测试场景装配器 | 保留当前深牌桌聚合、可序列化卡牌快照和完整牌桌视图；文件槽位与单局聚合接入留给正式存档模块。正式场景组合必须等真实 UI 消费者出现后再建立，不能为了清单创建转发壳；卡牌模型不扩展成地点 / 工位万能模型 |
| 4 行动 / 配方 / 牌桌进度 | `ActionDefinition` 作者聚合、显式候选、牌桌拥有的 `ActionPlan`、稳定请求、单局行动实例、回合进度真相、开始时冻结结果计划、完成后原子提交，以及运行中 / 暂停行动的可恢复快照 | 拖拽候选和 UIKit 填槽已经共同编辑同一计划；结果编译仍对两个现有牌桌意图做具体类型分支，Mod 新结果没有正式登记入口；完整单局文件存档、角色 GAS 快照和联机状态恢复尚未实现 | 保留一份 `ActionDefinition`，不建立第二套配方执行系统；候选是一次交互快照，计划是牌桌运行对象，完整后才生成请求；结果扩展等第二个真实状态 owner 出现后再以至少两个实现校准正式登记机制 |
| 5 剧本 / 回合 / 任务 | 单局剧本聚合、`QuestLog` 的任务集合与前置解锁、EventKit 已提交事实事件 | 当前正式事实只覆盖行动完成、日期到达与内容发现；尚未开放 Mod 包加载、任意事实投递、存档或联机复制 | 单局剧本实例直接拥有回合编号和任务日志；任务定义自行创建运行状态，任务日志只分发所属单局已提交的事实；新事实必须由真实 owner 接入，不新增 Encounter / Objective 顶级系统、任务注册表或第二事件总线 |
| 6 EX-GAS 属性 / 战斗集成 | EX-GAS 属性、GE、Tag、Cue；`CharacterCard` 继承 `TabletopCard` 并直接持有唯一 `AbilitySystemCell`；`Tabletop` 拥有活动 `Battle`、目标型 Ability 请求和独立权威随机流 | 自动实时调度、护盾、目标选择和正式数值规则尚未确定；战斗快照按产品决策明确排除；旧 `EAlignment` 仍有角色作者与旧目标判断消费者 | 角色卡不接入 `CharacterBase`；战斗只向 `AbilitySpec` 提交施法者、目标和权威随机种子；Foundation 关闭闪避 / 暴击，只验证固定基础伤害链；存档不保存活动战斗，联机会话策略后续独立裁决 |

## 同职责流程对照

| 模块 | StackCraft 流程 | 当前 Gameplay 流程 | 重构后的唯一职责流 |
|---|---|---|---|
| 内容 | `Resources.LoadAll` -> 各 Manager 私有列表 | `ScenarioDirector` -> `ResourceSystem` -> `ScenarioRun.ContentIndex` | 资源 / Mod 包依赖解析 -> 当前单局活动内容集合 -> `ContentIndex` 快照 |
| 启动 | `GameDirector` 与多个单例各自 `Awake` | `GameManager` 排序启动全部 `AGameSystem` | `GameManager` 启动进程服务 -> 创建 / 恢复一个单局剧本实例 -> 单局实例拥有玩法状态 |
| 卡牌 | `CardManager` / `CardStack` / `CardInstance` 同时改规则与表现，恢复时重新生成实例 | `ScenarioRun.Tabletop` 创建唯一卡牌状态并执行牌桌行动，快照保留局内实例 ID，投影器按状态修订自动读取 | 保持一份权威卡牌状态和一份可序列化快照；加载时原子重建同一批实例 ID，不建立第二卡牌目录或表现状态 |
| 行动 | 扫描配方 -> 随机选配方 -> `CraftingTask` -> `Recipe.Execute` 直接副作用 | 卡牌候选 -> 稳定请求复核 -> 单局行动实例 -> 冻结结果计划 -> 原子提交 | 不同交互入口生成候选 / 请求 -> 权威提交为行动实例 -> 统一推进 -> 各状态模块原子提交结果 |
| 剧本任务 | `GameDirector`、`TimeManager`、`QuestManager`、`DayCycleManager` 并列单例 | `ScenarioDirector`、`ScenarioTurnSystem`、`QuestSystem` 并列进程系统 | 单局剧本实例持有回合和任务日志，事实从聚合内提交，UI / 规则模块订阅已提交事实 |
| 战斗属性 | 模板自有 Stats / 战斗链 | EX-GAS 属性加程序集内部 GE 伤害执行；投射物只保存 GE ID；牌桌拥有活动战斗参与事实 | EX-GAS 是属性和效果唯一真相；牌桌只拥有参战/结束生命周期，不保留第二伤害配置源或卡牌状态 |

## 横向问题

### 测试边界污染正式架构

2026-08-09 已把 `FoundationTestSceneHarness`、测试场景生成器和测试场景过滤规则迁入 `Assets/Tests/Support`：

- `FoundationTestSceneHarness` 现在属于 `Gameplay.Foundation.TestSupport`，只在 `UNITY_INCLUDE_TESTS` 下编译，不进入正式 Player 构建。
- 测试场景生成器和场景过滤规则属于 `Gameplay.Foundation.TestSupport.Editor`，正式 `Gameplay.Editor` 不再依赖测试类型。
- 脚本 `.meta` 随文件移动，统一场景经正式生成器重建后引用 `Gameplay.Foundation.TestSupport::Gameplay.Tests.Support.FoundationTestSceneHarness`。
- `ProjectSettings/EditorBuildSettings.asset` 仍为零构建场景；测试使用 `EditorSceneManager.LoadSceneAsyncInPlayMode` 按测试路径加载。
- 新鲜验证：删除任务集合重复布尔状态后，`Logs/TestResults-Gameplay-Reaudit-EditMode-R5.xml` 为 `52/52`，`Logs/TestResults-Gameplay-Reaudit-PlayMode-R2.xml` 为 `6/6`。

### 作者工具反向依赖私有字段

测试生成器和多个 EditMode 测试仍通过 `SerializedObject.FindProperty` / `JsonUtility` 构造测试作者资产。这些入口已经隔离在测试程序集，不再是正式作者 API；但它们仍会在字段改名后集中失败。后续应给作者聚合提供少量明确的 Editor 创建 / 修改入口，让测试复用真实作者行为，而不是逐字段复制 Inspector 实现。

### 领域对象与系统的判定规则

只有具备独立生命周期、独立运行状态、独立存档/联机同步边界、独立作者源集合或独立玩家可见规则的对象，才建立独立 System。否则优先使用聚合根、值对象、作者定义或投影适配器。

不能因为有一个名词就创建 XSystem、XContext、XRegistry、XManager 或 XService。

### 战斗参与边界

2026-08-10 已完成第六模块的第一个真实战斗对象切片：

- `Battle` 是牌桌内的领域对象，直接拥有多个 `BattleSide`；每个战斗方只拥有本场参战卡牌 ID。角色所属阵营、敌我关系和临时阵营变化继续只存在于角色 GAS 状态与后续剧本关系规则中，不复制进战斗参与对象；`Tabletop` 是唯一能开始、离开和结束战斗的聚合根。
- 参战卡始终仍属于同一张牌桌，活动期间禁止直接移除；因此不会出现“牌桌一份卡牌、战斗再复制一份卡牌”的双状态。战斗结束后，卡牌正常回到牌桌的唯一移除入口。
- 旧活动战斗快照已删除，并按产品决策不再重建。战斗不进入存档；模块 8 只处理非战斗单局和局外长期状态。不得用 StackCraft 只保存双方卡牌、读档后重开战斗的方式冒充恢复。
- 未吸收 StackCraft 的全局 `CombatManager`、`CombatTask`、战斗单例、RPS 规则、攻击计时、战斗 AI 或同步协议。冲突区的“参战卡进入阵列”效果已由剧本阵型规则和牌桌唯一投影器复现；其矩形碰撞、堆栈推动、直接 Transform 修改和清理副作用没有进入正式链路。
- 旧 `EAlignment` 不能现在替换成单个整数标签字段：角色 ASC 基础标签目前为空，现有 GAS 标签表没有旧善恶中立的等价项，且敌我关系本来属于剧本规则而不是角色固有枚举。后续迁移的正确前提是剧本阵营关系作者源、角色 ASC 标签来源与临时阵营变更的 GE 生命周期同时成立。
- 阵型表现的边界是：`ScenarioDefinition` 内嵌按战斗方顺序配置的两方或多方队列规则，`Tabletop` 根据活动 `Battle` 和现有牌堆位置派生姿态，`TabletopView` 绑定单一 `Tabletop` 并观察卡牌 / 战斗修订；`TabletopViewSettings` 只提供一次战斗基础排序配置。Mod 新增 GAS 阵营不需要修改阵型，阵型也不拥有卡牌位置、战斗 ID 或 Unity 表现对象。
- 2026-08-11 重审后，角色卡 / 战斗方 / 阵型定向 `7/7`、统一 Foundation `13/13`、ScenarioContent `7/7`、全量 EditMode `432/433`（1 条既有忽略）、全量 PlayMode `30/30`，均零失败。覆盖角色卡 ASC 作者来源、参战资格、战斗方、阵型投影、结束和牌桌移除边界；不证明 GAS 阵营关系解析、实时调度、普攻、完整命中链、逃跑、战斗快照、完整存档、联机或回放已经完成。
- `CharacterCard` 继承 `TabletopCard`，并直接持有一个 EX-GAS `AbilitySystemCell`；`Tabletop` 创建、移除和结束单局时负责该状态的完整生命周期，行动候选和运行中复核按卡牌实际类型直接读取，不再保留外部 resolver、测试字典或桥接层。普通 `TabletopCard` 不创建 ASC，因此物品、地点和事件不会被强迫创建角色状态。
- `CharacterBase` 是 GameCore 的 2D 场景角色，继承 `Movable` 并依赖 Transform、Rigidbody2D、控制器、动画、交互和旧角色数据；它可以作为另一种场景角色存在，但不能与同一张角色卡同时代表同一逻辑角色，否则会创建两份 ASC。当前 Gameplay 牌桌不使用它，也不为它建立适配层。
- `CharacterCardDefinition : CardDefinition` 是牌桌角色的正式作者类型，只增加一个 EX-GAS ASC 预设引用。`Tabletop.CreateCard` 根据内容定义自动创建普通卡或 `CharacterCard`；公开的手工 `CreateCharacterCard(..., AbilitySystemCellConfig)` 已删除，调用方不能再拼第二份初始标签、属性集和技能配置。
- 当前角色 GAS 状态还没有正式快照模型。为避免表面成功、实际丢失属性/效果/能力，包含角色卡的牌桌快照会立即报错；角色存档、联机复制与战斗攻击链仍待后续真实模块裁决。生成的 `ABILITY_Attack` 仍只是 EX-GAS 生成身份，不是可直接执行的作者配置。

## 设计正确性的证明口径

后续每个模块必须提供以下证据，缺一不能称为最佳实践：

1. 职责流对照：参考实现、现有 GameCore 和新代码的入口、状态 owner、写入口、生命周期和失败语义逐项对齐。
2. 聚合不变量：明确哪个对象拥有状态；非法状态是否无法通过公开入口形成；内部契约破坏是否立即抛错。
3. 作者成本对照：作者只填写业务必要字段；内部 ID、派生索引、加载地址和局部 key 不要求重复维护。
4. 功能等价验收：新结构能够复现被吸收的玩家可见效果；测试场景不能只证明新框架内部自洽。
5. 变化压力验收：用多剧本、Mod 叠加、网络请求重放/过期、存档恢复或规则替换中的适用项验证没有单局/单机写死。
6. 删除测试：删除候选模块后，如果复杂度不会重新分散到多个调用方，该模块就是浅包装，应删除或并回父级。

## 本轮直接结论

- 前面模块的功能切片测试和架构最佳声明分离；测试通过记录保留，但不再作为最佳实践证据。
- 模块 3 不再拆分 `TabletopView`；它的实例、句柄、卡面缓存和布局更新共同构成一个牌桌表现生命周期，拆分只会把复杂度重新分散到场景、输入和资源调用方。
- 模块 4 已确认候选、请求和运行实例是三种真实阶段，不能误删；`ActionDefinition` 保留为作者聚合，运行实例和活动集合必须属于当前单局。
- 模块 2、5 的进程级系统与单局状态混合是跨模块问题，需要以真实单局会话模型统一重构。
- 模块 5 的游戏日不建立第二份可写状态：`ScenarioRun` 只保存总确认回合，当前日期和当日已确认回合由剧本的每日回合配置推导。按天数与内容发现任务直接由所属单局写入任务日志；发现集合仍只有剧本单局持有，任务解锁后由单局回放已有发现事实。当前跨日消费者只有任务日志与 HUD，已有回合事实已经携带提交后的日期；日结规则、遭遇、输入锁和自动存档仍没有消费者或作者源，因此不新增日开始 / 日结束事件、阶段枚举、规则注册表或空日程管线。
- 模块 5 的任务子项不再由任务日志中心工厂按类型创建。每个任务定义自己创建运行状态并解释所属单局已提交的事实；`QuestProgress` 作为单个任务的运行对象拥有状态和子项进度，`QuestLog` 只拥有集合生命周期与事实分发。当前日期和内容发现由 `ScenarioRun` 作为已有状态统一刷新，行动完成仍只消费一次；这允许未来派生任务消费既有事实而不修改核心任务日志，但不把尚未存在的 Mod 包加载或任意事实投递伪装成已开放 API。
- 模块 6 的自定义 GE 伤害执行不是默认删除对象；旧属性投影、GE 表之外的第二伤害作者源、旧效果系统和直接伤害命令已经删除。当前剩余边界是权威随机 owner 与固定伤害语义，不把它们伪装成已完成的联机设计。
- 模块 3 的卡牌 / 堆栈对象、深牌桌聚合、实例恢复与修订驱动投影可以保留；正式文件存档接入等待存档模块统一裁决，场景组合等待真实 UI 释放消费者，不提前创建空壳。
- 当前文档中“模块已完成”“最佳实践”“架构已收口”等表述，后续统一改成“当前切片已验证 / 尚未完成全量架构审查”。

### 模块 6 当前实施结果（2026-08-10）

- `CharacterSheet`、角色 ASC、属性快照和 UI 查询全部以 EX-GAS 属性码与正式属性集为准；旧 `Stats`、`EStat`、`FormalAttributeCatalog` 和只为它们服务的投影已删除。
- 投射物只保存 `impactGameplayEffectId`。命中时读取正式 GameplayEffect 配置；命中方向是该效果实例的动态输入，不是另一份伤害配置。
- 旧 `GameplayEffectDamageApplier`、`AEffect` / `IEffect` 和直接伤害命令已删除。伤害执行数据不对作者或外部模块公开，避免重新出现绕过 GameplayEffect 的写入口。
- 可配置的旧法力增减命令也已删除；技能 Cost 与恢复效果只走 EX-GAS GameplayEffect。角色内部仍可为复活和升级调整资源，但这不是可供 Gameplay 或内容作者调用的第二条效果执行链。
- 当前验证只证明属性与基础伤害集成切片成立：Unity Test Runner 的定向伤害测试 `7/7`、GameCore EditMode `89/89`、Gameplay EditMode `65/65`、Gameplay PlayMode `9/9` 均零失败；它不证明完整战斗、联机同步、回放或 Mod 动态伤害语义已完成。
- 牌桌战斗的权威随机不再派生于 ECS Entity 索引：牌桌唯一随机流派生每场战斗随机流，战斗再为每次 Ability 激活提供种子，EX-GAS Timeline 为每个 GE 派生独立种子。尚未迁入牌桌聚合的旧 2D 场景能力仍保留实体索引兼容路径；后续统一战斗接入时必须删除该兼容分支。战斗不进入存档，因此该随机流只服务活动战斗与未来会话权威同步。

## 下一步实施裁决

下一步不直接重写行动代码，先订正它依赖的单局生命周期：

1. 保留 `GameManager` 作为进程基础设施入口，不建立第二启动器。
2. 保留 `ScenarioDirector` 作为新局剧本开始、恢复和结束的编排入口；它只允许持有一个活动的单局剧本实例，符合当前客户端 / 主机一次只运行一局的真实需求。
3. 新的单局剧本实例是普通 C# 聚合，不是 `AGameSystem`、MonoBehaviour、静态单例或泛化 `RuntimeContext`。它接管当前已经存在的剧本 ID、回合编号和任务日志，并创建唯一 `Tabletop`；牌桌内部拥有卡牌状态和行动实例。
4. 删除浅的 `ScenarioTurnSystem`；确认回合成为单局剧本实例的行为，提交状态后仍直接通过 EventKit 发布事实。
5. 把 `QuestSystem` 的运行时逻辑保留为单局任务日志对象，由单局剧本实例创建和销毁；不再作为进程级系统。任务定义、前置解锁和当前测试行为保持不变。
6. 父级生命周期验收通过后，再重构模块 4：`ActionDefinition` 保留；卡牌拖拽和填槽弹窗共同编辑行动计划；只有完整合法计划才能创建行动实例；行动实例统一拥有参与者、权威随机结果、进度和状态迁移。
7. “配方”当前只是行动作者数据的一种语义组合，不建立第二 SO、第二 ID、第二作业或第二执行系统。只有真实出现跨行动复用的一组配方作者数据时，才讨论可复用子资产。
8. 结果扩展接口不在只有牌桌移除 / 创建两种结果时提前定型。等库存或 EX-GAS 效果成为第二个真实状态提交方后，再用至少两个实现校验扩展接口；当前具体类型分支登记为必须替换的临时限制，不继续增加新分支。

这一顺序只迁移已经存在的状态和行为，不新增原创玩法，也不为未来可能性创建空容器。

## 单局剧本聚合实施结果

2026-08-09 已完成上述第 1-5 项现有行为迁移：

- `GameManager` 仍是进程级系统装配入口，没有新增第二启动器。
- `ScenarioDirector` 仍是唯一的单局开始、结束和回合确认入口；它只持有当前活动的 `ScenarioRun`。行动完成事实由牌桌直接回到所属单局，先提交任务状态，再通过 YokiFrame EventKit 对外发布。
- `ScenarioRun` 是普通 C# 聚合，当前拥有已经存在的剧本 ID、已确认回合编号、`QuestLog` 和唯一 `Tabletop`。它不是 MonoBehaviour、`AGameSystem`、静态单例或泛化 Context。
- `QuestLog` 保留了原任务集合的前置解锁、状态变化、行动完成计数和任务子项进度；剧本允许零任务，不再用额外布尔值重复表达任务集合状态。
- `ScenarioTurnSystem` 与进程级 `QuestSystem` 已删除，不保留兼容组件、转发包装或第二查询入口。
- 旧进程级牌桌行动系统已经并入 `ScenarioRun.Tabletop`；回合与 Unity 游戏时间都由当前单局父级直接推进，不再依赖进程级事件订阅维持父子关系。
- 统一测试场景只装配 `ScenarioDirector`，任务和回合状态都从 `ScenarioDirector.ActiveRun` 查询；Build Settings 继续保持空共享场景列表。

新鲜验证证据：

- EditMode：`Logs/TestResults-Gameplay-ScenarioRun-EditMode-R1.xml`，52 项全部通过。
- PlayMode：`Logs/TestResults-Gameplay-ScenarioRun-PlayMode-R1.xml`，6 项全部通过。
- 场景重建：`Logs/Gameplay-ScenarioRun-RebuildScene-R1.log`，生成入口退出码为 0，场景中没有旧任务 / 回合组件残留。

本次没有实现存档恢复、联机复制、Mod 包加载、任意事实投递或完整任务 API。`QuestLog` 的具体任务子项中央工厂已经删除：每个任务定义自行创建运行状态，派生任务可以消费已有正式事实而无需修改核心任务日志。新事实来源、Mod 权限、联机权威和存档格式仍必须等真实 owner 与需求确定后再接入，不能以空接口提前定型。

## 行动运行聚合实施结果

2026-08-09 已完成模块 4 当前真实行为的运行时重构：

- StackCraft 的 `CraftingTask` 证明“开始后存在一个拥有目标、进度和终态的运行对象”需要吸收；其 `CraftingManager` 进程单例、`Recipe.Execute` 直接副作用、随机自动选配方和 UI 混合职责没有照搬。
- 保留 `ActionCandidate`、`ActionRequest`、`ActionInstance` 三阶段。它们位于 `Gameplay.Tabletop.Actions`：候选给本地 UI 展示；请求是已确认计划的短暂提交命令，只携带稳定内容 ID、槽位 key 和局内卡牌 ID，不冒充存档或网络 DTO；实例只由当前单局和牌桌复核创建。三者处于不同生命周期，不是重复真相。
- 删除只有一次线性查找的 `TabletopCardActionCandidateSelector`。具体 UI 必须从自己持有的候选集合完成选择，不能把一个循环提升成正式模块。
- 旧 `Job` 命名及相关状态、快照已统一改成 `ActionInstance`；不保留旧类型、别名或兼容包装。
- `Tabletop` 不继承 `AGameSystem`，也不是 GameManager 组件。它由当前 `ScenarioRun` 创建和结束，直接拥有唯一 `TabletopCards`、活动实例、推进模式和地区独立权威随机流；随机根种子由 `ScenarioDirector` 在单局开始时提供并由 `ScenarioRun` 派生，场景组件不能再事后初始化牌桌随机状态。
- 候选查询也从同一行动系统进入，UI 不再重复传入牌桌状态、内容索引和 GAS Cell 解析器；复杂槽位分配算法保留为内部实现，不形成第二公开入口。
- 行动开始时把基础结果与已选随机分支编译成不可变牌桌结果计划，记录需要移除的卡牌 ID 与生成物事实。完成时只提交这份计划，不再重新读取可能已经被 Mod / 编辑器改变的行动 SO。
- 牌桌结果仍在一个深模块内先完成全部存在性、位置和容量检查，再统一移除与生成；失败不会留下部分提交。
- `ScenarioDirector` 只使用 Unity `Time.deltaTime` 推进当前活动 `ScenarioRun`；结束剧本会把所有活动行动明确取消为 `ScenarioEnded`。
- 统一测试场景没有进程级牌桌行动组件，只保留 `ScenarioDirector`；测试装配从 `ScenarioDirector.ActiveRun.Tabletop` 进入正式链路。

新鲜验证证据：

- RED：`Logs/TestResults-Gameplay-ActionPlan-Red-R1.xml`，证明旧实现会在行动完成时读取修改后的 SO，目标测试为 0 通过、1 失败。
- GREEN：`Logs/TestResults-Gameplay-ActionPlan-Green-R1.xml`，同一目标测试 1 项通过。
- EditMode：`Logs/TestResults-Gameplay-ActionAggregate-EditMode-R3.xml`，53 项全部通过，Unity 进程正常退出。
- PlayMode：`Logs/TestResults-Gameplay-ActionAggregate-PlayMode-R3.xml`，6 项全部通过，Unity 进程正常退出。
- 场景重建：`Logs/Gameplay-ActionAggregate-RebuildScene-R1.log`，退出码为 0；场景中没有旧行动系统组件，Build Settings 仍为空。

该段是 2026-08-09 的历史结论。2026-08-12 模块 7.2 已出现真实填槽消费者，并新增由 `Tabletop` 拥有的可编辑 `ActionPlan`；行动请求仍只承接完整计划。结果计划仍只编译现有移除 / 生成两类牌桌意图，计划存档、内容包版本锁定和联机命令尚未实现。

## 牌桌聚合与自动投影实施结果

### 2026-08-11 模块 3.1 对象关系订正

- `TabletopCard` 不再只是 ID 记录。它直接持有所属 `TabletopCardStack`，并通过牌堆公开自己的逻辑位置与放置锁定状态；移出牌桌后归属清空。
- `TabletopCardStack` 是成员关系唯一写入口。构造、合堆、拆堆和移除负责更新卡牌归属；`TabletopCards.m_stackByCardId` 派生关系表已删除，避免牌堆列表与字典双重更新。
- 原 `TabletopCardState` 重命名为 `TabletopCards`，表达它只是 `Tabletop` 直接拥有的卡牌 / 牌堆集合和局内索引，不是与牌桌并列的状态聚合。序列化 `TabletopCardStateSnapshot` 仍保留准确的快照语义。
- StackCraft 的卡牌 / 牌堆对象直观性被吸收；`CardInstance` 混合 Transform、碰撞、Tween、UI、战斗、装备和制作的 MonoBehaviour 上帝对象没有进入正式链路。
- 新鲜验证：对象归属 RED 因缺少 `Stack` / `Position` 编译失败；GREEN 定向 `10/10`，全量 EditMode `421/422`（一条既有忽略），全量 PlayMode `30/30`。

### 2026-08-11 模块 3.2 放置规则 owner 订正

- `ScenarioDefinition` 内嵌牌桌放置作者定义，只声明边界、禁放区、卡牌规则尺寸和 XY 堆叠步进；它不是独立内容，不新增 ID。`ScenarioRun` 创建 `Tabletop` 时生成并冻结唯一运行时规则。
- `Tabletop.TryPlaceStack` 不再接受调用方传入规则，公开 `MoveStack` 已删除。正式卡牌创建、拆堆放置、快照恢复校验和行动产物预演都使用同一个 `Tabletop.PlacementRules`。
- `TabletopViewSettings` 只保留视图资源、Z 深度、排序和拖拽手感。卡牌尺寸与 XY 步进从表现配置移除，`TabletopView` 直接读取所绑定牌桌的规则几何。
- 最大解算轮数属于内部算法预算，已从剧本作者源和运行时规则删除，由内部 `TabletopCardStackPlacementSolver` 维护；策划和 Mod 作者不填写技术参数。
- 行动结果在删除任何参与卡前，先预演移除后的牌堆和全部产物。空间不足直接拒绝且修订号不变；成功时独立牌堆产物按规则解开重叠。
- 新鲜证据：牌桌定向 `11/11`、行动结算定向 `11/11`、全量 EditMode `423/424`（一条既有忽略）、全量 PlayMode `30/30`。

### 2026-08-11 模块 3.3 拖拽意图与输入边界订正

- `TabletopCardDragInput` 是新输入系统到牌桌意图的薄适配组件，不是输入 owner，也没有牌桌写权限。它只订阅 `GameCore.InputSystem` 的正式 Click 动作。
- `TabletopCardDragSession` 以屏幕像素位移判断点击或拖拽，以牌桌坐标保持鼠标按下点到牌堆锚点的偏移；相机缩放不再改变拖拽阈值，从卡牌边缘按下也不会跳牌。
- `TabletopCardPointerReleaseIntent` 明确区分指针按下/释放牌桌位置与请求牌堆锚点。目标卡牌只是候选事实；空白放置仍由唯一 `Tabletop.TryPlaceStack` 复核并原子提交。
- 删除输入组件上的命中层、最大射线距离、拖拽距离和牌桌平面手填字段。正式主相机与 `EventSystem` 只从 `GameManager` 读取，射线距离由相机远裁面推导，组件自身 Transform 定义牌桌平面。
- StackCraft 的按下偏移、尾段拖动、点击/拖拽区分和目标高亮被吸收；输入回调内的拆堆、交易、装备、战斗、制作和直接 Transform 写状态被排除。
- 新鲜证据：拖拽会话 `6/6`、Foundation 真实输入 `13/13`、全量 EditMode `425/426`（一条既有忽略）、全量 PlayMode `30/30`。

### 2026-08-11 模块 3.4 牌桌视图对象订正

- 原 `TabletopCardViewProjector` 不是狭窄投影算法，而是当前牌桌的完整 Unity 表现对象；它已重命名为 `TabletopView`，继续集中管理卡牌、行动进度、战斗姿态、拖拽表现和资源句柄，不拆成平铺表现系统。
- 原 `TabletopCardSettings` 已重命名为 `TabletopViewSettings`。它只保存牌桌级视图资源和表现参数，不拥有权威卡牌尺寸或 XY 堆叠步进。
- 删除可由表现对象自身推导的 `m_viewRoot`。单卡视图保存对应 `TabletopCard` 对象引用，身份从领域对象派生；姿态同步直接读取卡牌所属牌堆。
- 设置为空或非法时在绑定阶段明确失败，不运行时创建引用对象或静默夹取。资源实例和句柄仍只通过 `ResourceSystem` 创建与释放。
- 新鲜证据：定向 EditMode `2/2`、Foundation 真实 YooAsset 创建与解绑释放 `13/13`；模块 3 最终全量 EditMode `425/426`（一条既有忽略）、PlayMode `30/30`。旧类型名和旧视图根正式入口扫描为空。

2026-08-09 已完成模块 3 当前父级归属和表现同步切片：

- 没有新增只有 `Cards / Actions` 两个属性的转发壳。原本已经具备请求复核、行动实例、权威随机、进度和结果提交深度的行动模块，直接提升为当前单局的 `Tabletop` 聚合，并让它直接拥有唯一 `TabletopCards` 集合。
- `ScenarioRun` 是唯一 `Tabletop` 创建者。`TabletopCards` 构造函数改为程序集内部入口；正式运行时代码不能再创建一份卡牌状态后通过 `BindTabletopActionState` 二次绑定。
- 删除旧 `TabletopCardActionSystem` 类型、`TabletopActions` 属性、两种牌桌状态绑定方法以及外部 `AbilitySystemCell` resolver，不保留别名、兼容类型或转发入口。需要角色动态 GAS 标签时，行动直接读取角色卡自身的唯一 `AbilitySystemCell`，不重复传入牌桌状态。
- `TabletopCards.Revision` 只记录成功发生的真实状态变化。同堆合并、从堆底拆出原堆和移动到原位置不会制造假变化。
- `TabletopView` 在 `LateUpdate` 比较修订号，一帧内无论结果结算修改多少张卡牌都只读取一次最终权威状态；没有新增局部事件总线、刷新队列、影子卡牌表或第二表现状态。
- 统一测试装配器删除行动完成后的手动 `Refresh` 协程。PlayMode 验收会逐张核对现有视图对应的局内卡牌，并核对随机产物内容，避免只比较总数造成假通过。
- 正式 UI 的释放消费者仍未确定，因此本轮没有新增 `TabletopController`、`Context`、`Service` 或其它场景组合空壳；当前 `FoundationTestSceneHarness` 继续只属于测试程序集。

新鲜验证证据：

- RED：`Logs/TestResults-Gameplay-TabletopAutoProjection-Red-R3.xml`，1 项失败；行动结果提交后仍保留已移除卡牌的旧视图。
- GREEN：`Logs/TestResults-Gameplay-TabletopAutoProjection-Green-R1.xml`，同一行为 1 项通过，测试装配器没有手动刷新。
- 编译：`Logs/Gameplay-TabletopAggregate-Compile-R1.log`，Unity 退出码为 0，脚本编译错误为空。
- 场景重建：`Logs/Gameplay-TabletopAggregate-RebuildScene-R1.log`，Unity 退出码为 0。
- EditMode：`Logs/TestResults-Gameplay-TabletopAggregate-EditMode-R1.xml`，53 项全部通过。
- PlayMode：`Logs/TestResults-Gameplay-TabletopAggregate-PlayMode-R1.xml`，6 项全部通过。

当前未完成：完整 `ScenarioRun`、任务和活动行动还没有接入文件存档；正式 UI 消费者、关卡编辑器和 Mod API 尚未出现，本轮没有提前设计它们的组合接口。Build Settings 继续保持空共享场景列表。

## 卡牌实例恢复实施结果

2026-08-09 已完成模块 3 的卡牌状态快照与权威重建切片：

- StackCraft 的 `StackData / CardData` 只保存内容和堆栈，加载时重新生成实例；当前 Gameplay 活动行动会保存参与卡牌 ID，因此不能复制这种恢复方式。
- 新增 `TabletopCardStateSnapshot`、堆栈快照和卡牌快照。它们只保存下一局内卡牌 ID、堆栈登记顺序、位置、锁定状态、卡牌顺序、局内卡牌 ID 和唯一内容 ID，不保存表现对象、资源句柄或作者资产引用。
- `TabletopCards.Restore` 先完整验证所有堆栈、坐标、实例 ID、内容 ID、重复 ID 和下一分配号，再构造一份新状态。坏快照不会修改正在运行的牌桌，也不会留下半恢复状态。
- 快照经过当前 SaveKit 默认序列化器同源的 Unity `JsonUtility` 往返后再恢复，64 位实例 ID、堆内顺序和下一分配号保持不变；已经分配后又删除的最高 ID 不会被重新使用。
- `Tabletop` 的恢复构造入口使用当前 `ContentIndex` 复核每张卡牌内容。缺失 Mod 内容或引用非 `CardDefinition` 的快照会在牌桌发布前直接拒绝。
- 卡牌创建、删除、合堆、拆堆和移动的公开写入口全部收回 `Tabletop`。`Tabletop.Cards` 对外只提供查询、修订号和快照；行动结果提交也走同一牌桌聚合，不能绕过内容类型检查。
- GameCore `SaveSystem` 当前仍把地图、玩家、标记和持久化对象固定聚合为一个 `SaveDataBlock`。本轮没有为了牌桌恢复临时新增第二文件存档或修改其职责；完整单局存档扩展留给后续存档一级模块裁决。

新鲜验证证据：

- RED：`Logs/Gameplay-TabletopRestore-Red-R1.log`，缺少卡牌状态快照、导出和恢复入口。
- GREEN：`Logs/TestResults-Gameplay-TabletopRestore-Green-R1.xml`，实例 ID、堆栈顺序和下一分配号恢复行为 1 项通过。
- RED：`Logs/Gameplay-TabletopRestoreContent-Red-R2.log`，`Tabletop` 缺少通过当前内容索引恢复牌桌的入口。
- GREEN：`Logs/TestResults-Gameplay-TabletopRestoreContent-Green-R1.xml`，非卡牌内容拒绝行为 1 项通过。
- EditMode：`Logs/TestResults-Gameplay-TabletopRestore-EditMode-R1.xml`，55 项全部通过。
- PlayMode：`Logs/TestResults-Gameplay-TabletopRestore-PlayMode-R1.xml`，6 项全部通过。

当前边界：这里只完成牌桌卡牌状态的可序列化事实和原子恢复；活动行动恢复随后由模块 4 在此基础上接入，但仍不代表存档槽位、完整 `ScenarioRun`、任务、角色 GAS 状态、随机流或内容包版本已经完成恢复。

工具链残留：本轮 EditMode / PlayMode 日志仍出现 Unity `CoreBusinessMetrics` 缓存的 SQLite `disk I/O error`，但两次测试结果完整写出、全部通过且 Unity 退出码均为 0。该缓存故障没有在本轮修复，也不能和 Gameplay 恢复能力混称为同一问题。

## 牌桌拆堆、边界与重叠实施结果

2026-08-09 已完成模块 3 空白桌面放置链的回审与重构：

- 回审确认旧 `TabletopCardOverlapSolver` 只被 EditMode 测试调用，而且以单张卡牌 ID 表达空间占地；直接接入会让同一堆成员互相参与重叠解算。该实现及单卡合同已经删除，没有保留兼容类型。
- 新的内部 `TabletopCardStackPlacementSolver` 只按整堆解算。一次解算使用当前底牌的局内卡牌 ID 定位堆栈，不新增 Stack ID，也不让作者维护第二套身份；业务和 Mod 公开面只保留几何、放置规则和 `Tabletop.TryPlaceStack`。
- 当前 `TabletopViewSettings` 只配置视图预制体和表现参数；卡牌尺寸与 XY 堆叠步进已迁移到剧本的唯一牌桌放置规则。视图保持单位预制尺寸并读取 `Tabletop.PlacementRules`，不再让表现资产决定权威占地。
- `Tabletop.TryPlaceStack` 是空白桌面落位的唯一公开命令。它先在独立候选布局上完成拆堆后的整堆占地、边界、禁放区和堆间重叠解算；空间不足属于正常业务拒绝，返回 `false`，卡牌成员、位置和修订号都不改变。
- 解算成功后，拆堆、卡牌所属牌堆更新和所有受影响堆的位置只提交一次修订。`TabletopView` 继续按修订号自动投影，没有增加移动事件、刷新包装或视图反写状态。
- 统一测试场景按释放目标分流：命中其它卡牌时只查询行动候选；拖到空白桌面时提交整堆放置；点击不被误解为放置。输入层仍只产生释放意图，不拥有牌桌写权限。
- StackCraft `CardPhysicsSolver` 的“堆与堆分离、锁定堆不移动、完整占地受边界限制”玩家效果被吸收；其 `CardManager` 全局扫描、Transform 直接改状态和 CombatRect 耦合没有进入正式链。

新鲜验证证据：

- RED：`Logs/Gameplay-TabletopStackPlacement-Red-R1.log`，缺少整堆几何、完整放置规则和原子放置入口。
- GREEN：`Logs/TestResults-Gameplay-TabletopStackPlacement-Green-R1.xml`，整堆解算与原子状态行为 `15/15` 通过。
- 空白释放 RED：`Logs/TestResults-Gameplay-TabletopBlankPlacement-Red-R1.xml`，真实拖拽仍错误执行一次半成品行动查询。
- 空白释放 GREEN：`Logs/TestResults-Gameplay-TabletopBlankPlacement-Green-R1.xml`，真实输入到原子拆堆落位 `1/1` 通过。
- 边界与重叠 PlayMode：`Logs/TestResults-Gameplay-TabletopBoundaryOverlap-PlayMode-R1.xml`，越界夹取和空白落点堆间分离 `2/2` 通过。
- 场景重建：`Logs/Gameplay-FoundationScene-Rebuild-CardSettings-R1.log`，唯一卡牌设置资产和单位尺寸预制体生成成功，Unity 退出码为 0。
- 全量 EditMode：`Logs/TestResults-Gameplay-TabletopPlacement-AllEditMode-R2.xml`，`373` 通过、`1` 条既有条件忽略、`0` 失败，Unity 退出码为 0。
- 全量 PlayMode：`Logs/TestResults-Gameplay-TabletopPlacement-AllPlayMode-R2.xml`，`9/9` 通过，Unity 退出码为 0；日志可见重叠请求 `(0.90, 0.35)` 解算到 `(0.68, 0.35)`，越界请求 `(0.00, -4.00)` 解算到 `(0.00, -2.00)`。
- 公开面收紧后定向 EditMode：`Logs/TestResults-Gameplay-TabletopPlacement-PublicSurface-EditMode-R2.xml`，整堆解算与原子状态行为 `15/15` 通过。
- 公开面收紧后 PlayMode：`Logs/TestResults-Gameplay-TabletopPlacement-PublicSurface-PlayMode-R1.xml`，真实边界与重叠拖拽 `2/2` 通过。

当前边界：一张牌桌当前使用一份统一卡牌几何，符合可读卡面和稳定交互目标；本轮没有引入每内容不同尺寸。正式 UI 释放消费者、关卡编辑器中的牌桌区域作者源、禁放区域编辑、联机放置命令与 Mod API 仍未实现。`FoundationTestSceneHarness` 只是测试场景消费者，不能作为正式 UI 架构入口。

工具链残留：全量日志仍出现 Unity `CoreBusinessMetrics` SQLite `disk I/O error`，但本轮最终 EditMode / PlayMode 均写出完整结果并以代码 0 正常退出。该工具缓存问题仍未修复。

## 牌桌行动职责归属订正

2026-08-09 已完成顶层行动作者定义与牌桌行动运行对象的边界订正：

- `Gameplay.Actions` 只保留 `ActionDefinition`、参与槽位和结果意图基类，不再承载任何局内牌桌卡牌 ID、发现状态、回合时间换算或牌桌运行状态。
- 候选、请求、运行实例、快照、完成事实和牌桌结果结算全部进入 `Gameplay.Tabletop.Actions`，并由 `Tabletop` 创建、推进和提交；没有新增第二个行动 owner。
- 牌桌专属的移除卡牌、生成卡牌结果意图同步进入 `Gameplay.Tabletop.Actions`。顶层结果基类不再反向包含具体牌桌结果类型。
- 类型名删除重复的 `TabletopCard` 前缀，改为 `ActionCandidate`、`ActionRequest`、`ActionInstance` 等；旧类型、别名和兼容包装均未保留。
- 测试行动资产通过正式测试场景生成器重建，序列化类型已迁移到新命名空间；没有手改 Unity YAML。

新鲜验证证据：

- 编译：`Logs/Gameplay-TabletopActions-R1-Compile.log`，脚本编译错误为空，Unity 正常退出。
- 场景与测试资产重建：`Logs/Gameplay-TabletopActions-R1-RebuildScene.log`，Unity 正常退出；六个关键场景脚本 GUID 保持，Missing Script 扫描为空。
- EditMode：`Logs/TestResults-Gameplay-TabletopActions-EditMode-R1.xml`，`58/58` 通过。
- PlayMode：`Logs/TestResults-Gameplay-TabletopActions-PlayMode-R1.xml`，`8/8` 通过。

## 内容校验职责归属订正

2026-08-09 已把领域校验从内容底层总开关迁回定义对象：

- `ContentValidator` 只校验所有内容共有的唯一 ID 和 EX-GAS 标签，再调用 `ContentAsset` 的受保护校验钩子；它不再引用行动、任务、剧本或牌桌具体类型。
- `ActionDefinition`、`QuestDefinition`、`ScenarioDefinition` 各自校验自己的作者数据和跨内容引用。行动结果意图与任务子项通过自身多态钩子校验，不再要求中央校验器增加类型分支。
- `ContentValidationContext` 只提供本次活动内容集合的只读查询和问题报告；它不是第二内容索引，也不进入单局运行状态。
- Mod 派生内容、行动结果意图和任务子项可覆盖受保护校验入口；没有新增验证器注册表、静态类型表或兼容包装。
- 原有问题码和阻止索引建立的行为保持，任务前置循环仍只报告一次。

新鲜验证证据：

- 编译：`Logs/Gameplay-ContentValidationOwnership-R1-Compile.log`，脚本编译错误为空，Unity 正常退出。
- EditMode：`Logs/TestResults-Gameplay-ContentValidationOwnership-EditMode-R1.xml`，`59/59` 通过；其中新增一条派生内容校验契约测试。
- PlayMode：`Logs/TestResults-Gameplay-ContentValidationOwnership-PlayMode-R1.xml`，`8/8` 通过。

## 活动内容索引不可变性订正

2026-08-09 已收紧模块 1 的运行时内容公开面与生命周期失败语义：

- `ContentIndex.AllAssets` 原先声明为 `IReadOnlyList`，实际对象仍是可修改 `List`。调用方可以强转后增删内容，使公开枚举和按 ID 字典表达两套不同事实。
- 内容索引现在发布同一构建列表的只读包装；没有复制第二个内容集合，外部强转到 `IList` 后修改会明确抛错，按 ID 查询与内容枚举不能再分裂。
- 历史切片曾让 `ContentRegistrySystem.OnSystemInit` 对重复初始化立即抛出；该进程级登记器已在 2.1a 删除，因此这条只保留为旧生命周期回审证据，不再描述当前架构。
- YooAsset 内容加载统一使用既有 `YooAssetContentTag` 常量，没有增加第二资源标签或加载入口。

新鲜验证证据：

- RED：`Logs/TestResults-Gameplay-ContentIndexImmutability-EditMode-RED-R2.xml`，公开内容集合仍可修改，目标测试 `0/1` 通过。
- GREEN：`Logs/TestResults-Gameplay-ContentIndexImmutability-EditMode-GREEN.xml`，同一不可变性行为 `1/1` 通过。
- EditMode：`Logs/TestResults-Gameplay-ContentIndexImmutability-EditMode-R1.xml`，`62/62` 通过。
- PlayMode：`Logs/TestResults-Gameplay-ContentIndexImmutability-PlayMode-R1.xml`，`9/9` 通过。

## 牌桌视图解绑语义订正

2026-08-09 已删除当前 `TabletopView` 前身的 `Clear()` 半绑定状态：

- 旧 `Clear()` 释放全部视图和资源句柄后仍保留牌桌状态与内容索引，`IsBound` 继续为真；修订号未变化时也不会自动重建视图，公开调用会留下永久空白投影。
- 正式入口改为 `Unbind()`，一次性释放视图、卡面资源和拖拽预览，同时清除权威状态引用、内容索引和已投影修订号。
- `Bind()` 重绑、组件销毁和测试场景消费者停止都走同一个 `Unbind()`；未保留 `Clear` 别名或兼容包装。

新鲜验证证据：

- 编译：`Logs/Gameplay-ViewProjectorUnbind-R1-Compile.log`，脚本编译错误为空，Unity 正常退出。
- EditMode：`Logs/TestResults-Gameplay-ViewProjectorUnbind-EditMode-R1.xml`，`59/59` 通过。
- PlayMode：`Logs/TestResults-Gameplay-ViewProjectorUnbind-PlayMode-R1.xml`，`9/9` 通过；禁用场景消费者后投影器变为未绑定，卡牌视图归零。

## 单局内部行动完成事实订正

2026-08-09 已删除牌桌到所属单局任务日志之间的全局 EventKit 绕路：

- 旧链路是 `Tabletop` 发布全局 `ActionCompletedEvent`，`ScenarioDirector` 再把事件写回当前 `ScenarioRun.QuestLog`。事件没有剧本身份，无法证明事实来自当前单局。
- `ScenarioRun` 创建唯一牌桌时注入必需的完成回调；牌桌结果提交后直接回到创建它的单局，先更新该单局任务日志，再对外发布 `ActionCompletedEvent`。
- `ScenarioDirector` 不再注册或注销行动完成事件，只负责活动单局的开始、推进和结束。牌桌构造入口要求明确完成接收者，不允许无 owner 的运行牌桌静默存在。
- EventKit 继续承担跨模块外部事实派发，但不再替代同一聚合内部的父子对象协作。

新鲜验证证据：

- 编译：`Logs/Gameplay-ScenarioActionFact-R1-Compile.log`，脚本编译错误为空，Unity 正常退出。
- EditMode：`Logs/TestResults-Gameplay-ScenarioActionFact-EditMode-R1.xml`，`60/60` 通过；新增测试证明不启动 `ScenarioDirector` 时，行动仍只更新所属 `ScenarioRun.QuestLog`，且外部事实晚于任务状态提交。
- PlayMode：`Logs/TestResults-Gameplay-ScenarioActionFact-PlayMode-R1.xml`，`9/9` 通过。

## 普通行动时间换算职责订正

2026-08-11 回审确认时间换算没有独立作者生命周期：

- 删除独立 `TurnTimingDefinition` SO、测试资产、场景引用和 YooAsset 收集项；它不再作为可被场景任意替换的第二规则源。
- `ScenarioDefinition.SecondsPerTurn` 与每日回合数共同构成剧本时间规则，`ScenarioRun` 开局时冻结该值并控制回合 / 即时模式切换。
- `Tabletop` 只内部消费单局给出的秒数，把即时增量换算到既有 `ActionInstance.ProgressedTurns`；具体行动仍只有 `TurnCost`，战斗仍使用自己的实时链。

新鲜验证证据：

- 编译：`Logs/Gameplay-TurnTimingOwnership-R2-Compile.log`，脚本编译错误为空，Unity 正常退出。
- EditMode：`Logs/TestResults-Gameplay-TurnTimingOwnership-EditMode-R1.xml`，迁移后当时的 `60/60` 通过。
- PlayMode：`Logs/TestResults-Gameplay-TurnTimingOwnership-PlayMode-R1.xml`，迁移后当时的 `9/9` 通过。
- 当前回审证据：`Logs/TestResults-Gameplay-Module44-ScenarioTiming-R1.xml` 为 `8/8`，`Logs/TestResults-Gameplay-Module44-Foundation-PlayMode-R1.xml` 为 `13/13`，全量 EditMode / PlayMode 分别为 `430/431` 与 `30/30`。

## 单局终止与发现状态归属订正

2026-08-09 已继续收紧 `ScenarioRun`、`Tabletop` 和行动候选的生命周期边界：

- 旧实现结束剧本后只从 `ScenarioDirector.ActiveRun` 移除引用；调用方若提前保存 `Tabletop`，仍能创建或移动卡牌、切换推进模式和开始行动。
- `Tabletop` 现在拥有唯一终止状态。结束时取消活动行动并进入只读终态；此前拿到的牌桌引用仍可读取最终卡牌与快照，但全部写入口、候选生成和行动推进都会立即抛错。
- `ScenarioRun` 不重复保存第二个结束字段，只读取所属牌桌的终止状态，在增加回合编号或修改发现事实前先拒绝结束后的调用。
- 原 `ContentDiscoveryState` 没有被任何单局拥有，`FoundationTestSceneHarness` 可以自行创建和清空；浅的 `ActionDiscoveryFilter` 也允许调用方绕过发现规则直接查询牌桌候选。两者均已删除，不保留别名或兼容包装。
- 当前单局已发现内容直接成为 `ScenarioRun` 的状态。发现未知内容会拒绝写入；行动候选由 `ScenarioRun.FindActionCandidates` 统一应用发现事实后再交给牌桌解析，`Tabletop.FindCandidates` 收为程序集内部入口。
- `ScenarioDirector` 的公开开局方法只接收剧本 ID。2.1a 进一步删除了进程级 `ContentRegistrySystem`：导演在开局时通过 `ResourceSystem` 解析当前默认包与已启用 Mod 包，构建 `ContentIndex` 并原子发布 `ScenarioRun`；加载或初始激活失败时释放临时句柄，不发布半成品单局。

- 2.1b 已收口 `ContentIndex` 的查询边界：内容校验报告和跨资产校验上下文都只暴露真实只读视图；重复传入同一资产在校验阶段直接报告错误，不再让底层字典异常替代内容错误。当前仍未裁决内容资源句柄的最终单局 owner，下一切片为 2.1c。

- 2.1c 已确认句柄 owner：`ScenarioDirector` 唯一持有自己创建的内容资源句柄，并在开局失败、结束、停止和关闭时释放；`ScenarioRun` 只持有冻结的 `ContentIndex`。运行中卸载 Mod 包的协商协议仍归后续 Mod 模块，当前不能声称支持热切换。

- 2.2 已回验单局创建与结束：活动时重复开局直接失败；结束后旧单局不能再推进；重新开始同一剧本会得到独立的新 `ScenarioRun` 和归零状态。2.3 因真实场景等待将正式入口收口为异步方法，但仍没有第二套同步入口或影子开局状态机。
- 2.3 已完成场景组合：`ScenarioDefinition` 通过正式场景资产选择器声明初始 YooAsset 场景地址，空值表示当前场景运行；`ScenarioDirector` 只在 `SceneSystem` 完成切换和单局初始组合后发布 `ScenarioRun`，结束时先关闭旧局与内容句柄，再返回来源场景。`GameManager` 仍是唯一跨场景进程宿主，普通剧本 / 返回场景不得再配置第二个宿主。场景组合定向 PlayMode `1/1`、剧本内容夹具 `7/7`、全量 EditMode `420/421`（一条既有忽略）、全量 PlayMode `30/30`。
- 统一测试场景不再查询内容系统后把同一索引回传给导演。未来 Mod 链若改变活动内容集合，必须在内容登记职责中合并和校验，再由所有单局共同消费，不能让不同开局调用方私传第二套索引。
- 这次只迁移 StackCraft 已选择吸收的“发现后才可展示 / 选择行动”能力，没有实现研究随机、蓝图 UI、局外图鉴、存档格式或 Mod API。

新鲜验证证据：

- RED：`Logs/TestResults-Gameplay-EndedRun-EditMode-RED.xml`，结束剧本后旧牌桌引用创建卡牌没有抛错，目标测试 `0/1` 通过。
- GREEN：`Logs/TestResults-Gameplay-EndedRun-EditMode-GREEN.xml`，同一生命周期行为 `1/1` 通过。
- 编译：`Logs/Gameplay-ScenarioDiscoveryOwnership-R1-Compile.log`，发现状态迁移后脚本编译错误为空。
- EditMode：`Logs/TestResults-Gameplay-ScenarioOwnership-EditMode-R1.xml`，`61/61` 通过。
- PlayMode：`Logs/TestResults-Gameplay-ScenarioOwnership-PlayMode-R1.xml`，`9/9` 通过；统一测试场景的拖拽行动候选经过所属 `ScenarioRun` 的发现状态。
- 最终编译：`Logs/Gameplay-ScenarioContentOwnership-R1-Compile.log`，导演内容依赖收口后脚本编译错误为空。
- 最终 EditMode：`Logs/TestResults-Gameplay-ScenarioContentOwnership-EditMode-R1.xml`，`61/61` 通过。
- 最终 PlayMode：`Logs/TestResults-Gameplay-ScenarioContentOwnership-PlayMode-R1.xml`，`9/9` 通过。

## 活动行动快照恢复实施结果

2026-08-10 已吸收 StackCraft `StackData.ActiveCraft` 与 `CraftingManager.RestoreCraftingTask` 所表达的“进行中的桌面工作可保存进度并继续”的玩家可见能力，但没有迁入其全局管理器或文件存档边界：

- `ActionInstanceSnapshot` 现在用 Unity 可序列化字段保存行动内容 ID、回合消耗、已推进进度、运行 / 暂停状态、参与卡牌绑定、已选随机分支和开始时冻结的牌桌结果计划。活动集合不会包含完成或取消行动，因此 2026-08-11 删除了快照中永远只能为 `None` 的取消原因字段。
- `ActionRequest.FromSnapshot` 只把快照中的行动与槽位事实重新提交给同一 `Tabletop` 复核；`Tabletop` 在卡牌状态恢复后逐项重建候选、校验参与对象、恢复行动，并在所有行动均合法后一次性发布活动集合。
- 恢复只接受尚未完成的运行中或暂停行动。损坏快照、缺失参与卡牌、行动作者回合消耗变化、缺失结果产物或锚点都会直接拒绝，不能留下半恢复行动。
- 行动完成时继续使用快照中冻结的结果计划，不重新读取已经被编辑器或 Mod 修改的行动结果资产；这保留了“已经开始的工作按开始时规则完成”的事实。
- 本轮没有新增第二套存档、资源、标签、事件或行动 owner。正式文件槽位、完整 `ScenarioRun` / 任务 / 随机流恢复、内容包版本锁定和联机会话恢复仍属于后续真实存档 / 联机职责。
- 当前卡牌快照只重建普通 `TabletopCard`。因此，依赖 `CharacterCard` 的 EX-GAS 状态或角色标签的行动，当前会在恢复复核时明确拒绝；必须等角色与 GAS 拥有正式快照来源后再扩展，不能伪造默认角色状态。

本轮验证：

- 运行时程序集静态编译通过。
- 编辑器测试程序集静态编译通过；仅存在已有 Odin API 过时警告。
- 新增行为测试覆盖 JSON 往返、运行行动继续、暂停保持、冻结结果计划、损坏快照、参与卡牌缺失和作者回合消耗漂移。
- 2026-08-11 已补齐新鲜运行验证：`Logs/TestResults-Gameplay-Module45-ActionSnapshot-R1.xml` 为 `16/16`，全量 EditMode `430/431`、`0` 失败、`1` 条既有忽略，全量 PlayMode `30/30`。

## 阶段 A 核心闭环验收（2026-08-11）

- **已复现**：统一 `FoundationTest` 真实加载后，经现有资源系统 / YooAsset 建立单局内容索引和卡牌视图；真实新输入系统覆盖空白拖拽放置与目标卡牌候选；UIKit 显式选择后由 `ScenarioRun.StartAction` 创建行动；HUD 推进两回合后完成权威分支结算、产物视图和任务反馈。
- **明确排除**：StackCraft 合堆自动制作、全局配方扫描、随机替玩家选择、固定 `Main` 场景、`Resources.LoadAll`、`CraftingManager` / `CraftingTask`、SO 直接执行副作用和自动连续制作均未进入正式链路。
- **本阶段阻塞缺口**：无。地点 / 工位行动提供者、待填充候选 UI、正式 UI、完整存档、联机和 Mod 协议属于后续模块，未被本阶段测试 harness 冒充完成。
- **证据**：`Logs/TestResults-Gameplay-Module45-PlayMode-R1.xml` 中 Foundation 全组 `13/13`；内容视图、边界拖拽、重叠放置和 HUD 完整行动四项门禁均通过。同轮全量 EditMode `430/431`、全量 PlayMode `30/30`。

## 牌桌行动进度表现验证

2026-08-10 已用当前牌桌框架复现 StackCraft `CraftingTask` 与 `ProgressUI` 的玩家可见效果：行动启动后显示进度，暂停时保留进度并变更状态提示，恢复后继续推进，完成后进度表现消失。这里吸收的是功能，不迁入 StackCraft 的全局制作管理器或其旧 UI 状态。

- `ActionInstance` 仍是唯一的行动进度和运行 / 暂停状态真相，`Tabletop.ActiveActions` 是当前单局可投影行动的唯一来源。进度视图只缓存本帧用于绘制的数值，不能修改行动、卡牌、工位或回合状态。
- 单个 `TabletopView` 已经拥有同一牌桌下的进度视图实例及其资源句柄：它从活动行动找到首个参与卡牌作为锚点，创建、更新和释放对应表现。多个行动落在同一卡牌时只在画面上错开，不增加或改变工位容量、参与规则或行动成功率。
- `TabletopViewSettings` 是行动进度预制体的唯一作者配置入口；实例化和释放继续使用既有 `GameCore.ResourceSystem` / `SoftAssetReference`，没有新增 YooAsset 地址字段、加载包装层或第二套资源 owner。
- 当前蓝色卡面和进度条是统一测试场景的原型素材，不是正式 UI 框架或正式界面皮肤。正式 UI 出现前，牌桌投影器继续只承担“把单局状态显示出来”的职责。

本轮真实链路和验证：

- 在 `FoundationTest` 场景通过“释放事实 -> 候选解析 -> `StartSelectedAction` -> `ScenarioDirector.ConfirmTurn`”启动行动，半个回合后进度为 `0.5`，并锚定到首个参与卡牌。
- 暂停后进度数值保持不变，进度条由运行中的青色变为暂停中的橙色；恢复并确认下一回合后行动完成，`ActiveActions` 与场景中的活动进度视图都归零。
- `FoundationTestScenePlayModeTests` 覆盖创建、半回合进度、暂停、恢复、完成和视图释放；2026-08-10 在当前已打开的 Unity 编辑器中真实运行，结果为 `8/8` 通过、`0` 失败。运行中、暂停、完成后释放的截图分别保存在 `.puerts-unity-mcp/editor-window-screenshots/action-progress/`。

## 牌桌拖拽后的行动选择验证

2026-08-10 已让统一测试场景从真实拖拽路径复现“候选行动由玩家明确选择”的交互，而不是把 StackCraft 的自动制作链带入 Gameplay。

- `FoundationTestSceneHarness` 仍是测试夹具，不进入正式 Player 构建；它只把命中目标卡牌后的释放意图交给所属 `ScenarioRun` 查询候选，并使用既有 UIKit 展示本次结果。
- `TabletopActionChoicePanel` 只是短暂的 UI 投影，只回传玩家选择，不构造请求或执行行动。`TabletopInteraction` 对完整候选调用所属 `ScenarioRun.StartAction`；未完整候选经单局发现权限复核后创建牌桌拥有的 `ActionPlan` 并打开填槽面板。UI 不保存第二份槽位绑定。
- UI 输入没有新增第二套作者资产。面板进入既有 `GameStateSystem` 的 UI 层时，`GameCore.InputSystem` 复用同一份 `PlayerInput.actions` 驱动 UIKit 的 `InputSystemUIInputModule`；关闭面板后恢复 Gameplay 输入。Gameplay 状态下的常驻 HUD 复用同一个 UI 模块，但关闭导航事件，避免把键盘导航误送入 HUD。
- 新鲜验证：`FoundationTestScenePlayModeTests` 为 `11/11` 通过，`ContentRegistryPlayModeTests` 为 `3/3` 通过，Unity 控制台错误为 `0`。`Library/GameplayVisualEvidence/foundation-action-choice.png` 显示面板位于目标牌侧方、目标仍可见，图面判断为通过。
- 2026-08-12 模块 7.2 已用两个真实候选覆盖多选和填槽链路，并补充中文测试字体。仍不宣称正式 UI 皮肤、网络授权、计划存档或 Mod 行动 API 已完成。

## 玩家确认回合 HUD 验证

2026-08-10 已让统一测试场景复现 StackCraft `DayTimeUI` 的最小玩家效果：玩家可以从底部常驻控件确认当前剧本回合，而不是让测试代码直接调用回合入口。

- `ScenarioTurnPanel` 位于 UIKit 的 `UILevel.Hud`，只持有当前 `ScenarioDirector` 的引用并显示其活动 `ScenarioRun` 已有的日期和总回合。面板没有日程副本、第二个回合编号、第二个事件或新的输入资产；按钮只调用 `ScenarioDirector.ConfirmTurn()`，显示通过既有 `ScenarioTurnConfirmedEvent` 刷新。
- `FoundationTestSceneHarness` 仍是测试场景装配器：它在测试剧本启动后打开 HUD，并在自身禁用时关闭 HUD，避免跨场景保留已销毁剧本导演。预制体、YooAsset 收集项和场景引用继续全部由既有“重建测试场景”作者入口生成。
- `GameCore.InputSystem` 继续是唯一输入 owner。它把 UIKit 创建的唯一 `InputSystemUIInputModule` 绑定回同一份 `PlayerInput.actions`；玩法状态保留 UI 的指针动作以支持 HUD，但不发送导航事件。牌桌拖拽在输入回调中用当前指针坐标直接做 UI 射线检测，不读取上一帧 `IsPointerOverGameObject()` 状态，因此一次 HUD 点击不会同时成为牌桌拖拽。
- 测试 HUD 暂时使用当前工程已有字体的 ASCII 文案 `Day / Turn / Advance Turn`，避免把缺少中文字形的临时字体伪装成本地化完成；这不是正式文本或字体管线决策。
- 新鲜验证：`FoundationTabletop_PlayerAdvancesTurnWithHudButton` 覆盖真实鼠标点击后总回合和 HUD 显示同步；`FoundationTabletop_PlayerCompletesTurnBasedActionThroughHud` 覆盖“拖拽 -> 选择行动 -> 两次 HUD 点击 -> 行动完成 -> 产物 / 任务完成”，不直接调用回合确认。当前 Unity PlayMode 回归 `FoundationTestScenePlayModeTests` 为 `11/11`，内容索引回归 `ContentRegistryPlayModeTests` 为 `3/3`，控制台错误为 `0`。当前 Game View 图面核验通过：HUD 位于底部中央，不遮挡测试牌桌卡牌，字体无缺字。
- 这仍是地基测试场景的最小 HUD，不宣称已经完成正式日月美术、角色详情、回合计划界面、本地化、联机命令授权或完整 UI 组合。

## 吸收方式校正（2026-08-10）

用户指出“用另一套代码复现模板，和直接复制有什么区别”后，对当前最小闭环做了源码级复核。结论是：当时 Gameplay 正式运行时代码没有调用 `CryingSnow.StackCraft`；当时唯一直接复用的是 `FoundationTest` 生成器读取 `Assets/StackCraft/Sprites/Square.png` 作为临时卡面。当前卡面已经迁入 `Assets/Art/Sprites/卡牌占位图.png`，该历史段落只作为 2026-08-10 吸收方式校正记录。因此，当前链路应准确称为“综合当前框架后实现的最小等效能力”，不能称为“直接迁入模板代码”。这不是否定自行实现，也不是要求以后优先复制模板；每个模板切片仍须先判断模板是否已是适合 CardLoop 的最佳实践。

本次裁决如下：

| StackCraft 切片 | 当前处理 | 为什么不能直接作为正式运行代码 | 后续口径 |
|---|---|---|---|
| `GameDirector` | 只保留单局开始、推进、结束的职责证据 | 它同时拥有静态单例、固定场景名、文件槽位、跨场景临时搬运卡牌和直接 `SceneManager` 调用；直接迁入会与现有 `GameManager`、资源和存档职责并存。 | `ScenarioDirector -> ScenarioRun` 只作为当前单局最小入口，不继续复制标题、读档、旅行等模板流程。 |
| `CardStack` / `CardPhysicsSolver` | 改造为纯牌桌状态和放置解算 | 模板对象直接持有 Unity 卡牌、Transform、Tween、`CardManager`、`Board` 和战斗矩形；当前解算还必须支持稳定排序、禁放区和纯状态快照。 | 保留“牌堆 + 边界 + 重叠分离”的行为与算法思路；不为了表面复用把旧组件链搬回。 |
| `CraftingTask` / `CraftingManager` | 只吸收进行中行动和进度表现 | 模板从全局管理器扫描配方、自己创建 UI，并由 `Recipe.Execute` 直接修改世界；它的秒数进度也与当前统一回合进度冲突。 | 当前 `ActionInstance` 已足以证明行动开始、暂停、推进、完成。没有新的明确玩家能力前，不继续向它补模板制作功能。 |
| `DayTimeUI` / `ProgressUI` | 只作为测试 UI 的交互参考 | 模板 UI 直接读取 `TimeManager`、`AudioManager`、`WorldCanvas` 等旧单例。 | 现有 HUD 和进度视图仅验证当前 owner 的输入和状态投影，不作为正式 UI 设计或继续克隆模板界面。 |
| 卡面素材 | 已迁入项目资源 | 素材没有运行时状态或旧职责冲突；当前为避免模板路径残留，已迁入 `Assets/Art/Sprites/卡牌占位图.png`。 | 原型期使用项目自有中文素材路径；参考模板只保留为对照来源。 |

因此，`FoundationTest` 的“拖拽 -> 选择行动 -> 确认回合 -> 产物 / 任务完成”是当前基础验收链。后续可以在需要验证新模块时继续扩展它，但扩展目标是证明 CardLoop 自己的对象、交互和职责边界能覆盖选择吸收的模板能力，而不是复制 StackCraft 的 UI 或玩法。每个 StackCraft 模块开工前，先写清该模块是整体保留 / 迁入、提取改造、自行实现还是排除，并给出模板是否值得保留的源码依据。

## 模块 0.3：输入、UIKit 与事件入口订正

2026-08-11 完成模块 0.3 的技术宿主审查。这里不实现正式 UI，只确认当前统一测试场景的输入、UI 根和跨模块事件不会形成第二套真相。

- `GameCore.InputSystem` 保留为唯一正式输入 owner。它持有 `PlayerInput` 动作资产、动作图切换、重绑定、UI 输入模块绑定和外部动作订阅；牌桌 `TabletopCardDragInput` 只订阅其 Click 动作，不读取 `UnityEngine.Input` 或 StackCraft 的静态输入帮助器。
- 当前 UIKit 根预制体已持有唯一 `EventSystem` 和 `InputSystemUIInputModule`，项目定义启用新输入系统，UIKit 会选择新输入模块。UIKit 根与 Foundation 场景唯一 `PlayerInput` 均使用 `InputSystem_Actions`；Unity UI 模块切换资产时按同名 UI 动作重定向引用。删除测试夹具内第二次 `SetActionMap` 后，真实 HUD 点击仍成立，因此不新增强制创建 UI 根、第二个输入资产或项目侧 UIKit 包装。
- 删除 `InputSystem` 在启动时直接向旧 2D 角色系统发送移动、交互、技能和菜单命令的代码。输入层不再拥有角色或技能业务，未来玩法对象必须通过动作订阅进入自己的聚合根。
- Gameplay 没有 `GameEventBus`、`EventCenter` 或 `GameRuntimeEvents` 等第二套通用事件入口；`ScenarioRun`、任务日志和视图直接使用 `EventKit.Type`。搜索到的 `GASEventCenter` 是 EX-GAS 属性变更 API，不作为 Gameplay 事件总线。
- `UISystem/UIManager` 没有加入 Foundation 场景，当前仅是 GameCore 的旧通用菜单候选，不是 CardLoop 的正式 UI 框架。它在模块 5 有真实菜单消费者前不再扩展；唯一当轮订正是把遗留的 `fw_menu` 默认栈名改为通用 `game-menu`。
- 删除 `FormalSceneSingletonConflictDiagnostics`。该类仅对白名单场景写日志，既不拥有 UI 根，也不让非法配置立即停止，属于无职责诊断层。Foundation 新增“一个 `PlayerInput`、一个 UIKit `EventSystem`、零旧输入模块”的场景架构守卫。

新鲜验证：输入唯一性守卫 `1/1`，完整 `FoundationTestScenePlayModeTests` `13/13`，`GameManagerAndGameStateLifecycleEditModeTests` `9/9`。这只能证明当前单玩家 Foundation 场景的入口一致，不证明正式菜单、多人本地输入、联机授权、Mod 输入扩展或正式 UI 已完成。

## 模块 0.4：场景加载、过场与地图业务订正

2026-08-11 完成技术场景后端审查。这里吸收的是 StackCraft 场景进入 / 离开的玩家可见能力和稳定加载顺序，不复制其固定场景名、`GameDirector` 单例、直接 `SceneManager` 调用或跨场景业务搬运。

- `ResourceSystem` 现在直接将项目 `ResourceSystemSceneLoaderPool` 注册到 `SceneKit.SetLoaderPool`。加载器本身选择默认包或 Mod 包，并拥有对应 YooAsset 场景句柄；不再经过 `ResKitSceneLoader` 再转一次项目加载器。
- 场景显式卸载完成和异步加载返回无效场景时，加载器立即释放句柄并清除资源包占用。这样 Mod 包卸载只依据真实场景占用，而不是旧加载器缓存。
- `SceneSystem` 是唯一技术场景切换 owner，串行编排淡出、Single 加载、淡入和场景事件；它不拥有剧本、地图、检查点、角色位置或存档。`TransitionSystem` 只负责动画播放，且是 `SceneSystem` 的显式系统依赖。
- 原 `MapSystem` 保留为旧 2D 地图业务对象：检查点、重生、传送、地图配置、导航和地图存档。它跨场景时调用 `SceneSystem`，不再同时保存技术场景状态。
- 生命周期事件名称从 `Map*` 收敛到 `Scene*`。`SceneTransitionCompletedEvent` 只代表成功，`SceneTransitionEndedEvent` 才代表成功、失败或取消后的清理完成；输入系统订阅结束事件恢复输入。
- `SceneKit` 的取消参数不会取消底层 YooAsset 场景加载，只会取消等待。项目侧选择等待真实加载完成，避免资源句柄失去 owner；当前不宣称“可强制取消场景加载”。

新鲜验证：`GameManagerAndGameStateLifecycleEditModeTests` `9/9`、`PersistenceSystemRegistrationEditModeTests` `4/4`、`ResourceSystemLifecyclePlayModeTests` `8/8`、`ContentRegistryPlayModeTests` `4/4`、`FoundationTestScenePlayModeTests` `13/13`。`FoundationTest` 由正式生成入口重建，场景只配置一个 `SceneSystem`。
