---
name: gameplay-architecture
description: Gameplay 地基架构规范：用于吸收 StackCraft、2DRPGEngine、UE/Unity 等参考时，裁决对象模型、系统边界、目录组织、Mod 和联机扩展。
metadata:
  type: doc
  status: 已交付
---

# Gameplay 地基架构规范

## 适用范围

本规范用于 CardLoop Gameplay 地基、StackCraft 模板吸收、参考工程对照和后续模块回审。它解决的不是某个具体玩法数值，而是代码应该围绕哪些游戏对象组织、哪些职责应该合并或删除、哪些参考能证明什么。

当前阶段仍是打地基：用户补充的《卡牌生存：无限》设计只作为扩展性约束，不等于提前实现职业、剧本、Mod、联机或原创数值业务。

## 结论

Gameplay 地基必须优先以玩家可感知的游戏领域对象组织，而不是把一切拆成平铺的 State、Solver、Resolver、Projector、System 文件集合。

- 卡牌、牌堆、牌桌、剧本、角色、工位、行动实例、任务日志这类对象，必须有清晰对象边界、可读状态和唯一写入口。
- 规则解算、候选生成、投影刷新、校验器、索引器可以存在，但它们是领域对象的内部协作者，不是领域主结构。
- 不采用看起来更可扩展的 ECS 式贫血拆分，除非已经明确进入 DOTS/ECS 或有真实性能证据、数据布局需求和调试收益。
- 支持 Mod、关卡编辑器和联机，不是把对象拆没；正确做法是让对象边界、作者源、命令、快照、权限、确定性随机和版本约束清楚。

## 参考源裁决

### Unity 原生模型

Unity 的原生工作方式是 GameObject + Component：场景里的对象代表角色、道具、布景、相机等，组件提供功能；每个 GameObject 都有且只有一个 Transform 表达位置、旋转和缩放。对 CardLoop 的约束是：Unity 表现层可以是组件组合，但 Gameplay 领域层也应能对应到直观对象，不能只剩离散算法和状态表。

官方入口：

- https://docs.unity3d.com/Manual/GameObjects.html
- https://docs.unity3d.com/Manual/Components.html

### UE Gameplay Framework

UE 的 Gameplay Framework 以 Actor、Component、Pawn、Controller、GameMode、GameState、PlayerState 等粗粒度职责组织玩法。它证明成熟游戏框架会区分世界对象、控制意图、对局规则、同步状态和跨关卡持久对象，而不是把玩法拆成大量同级薄系统。

官方入口：

- https://dev.epicgames.com/documentation/en-us/unreal-engine/gameplay-framework-in-unreal-engine
- https://dev.epicgames.com/documentation/unreal-engine/gameplay-framework-quick-reference-in-unreal-engine

吸收边界：

- 吸收职责分层和生命周期原则。
- 不照搬 UE 类名、继承树、网络 API 或引擎私有结构。
- 不能用 UE 有 Framework 类作为新增空壳 Context、Owner、Manager、Service 的理由。

### StackCraft 模板

本地参考路径：Assets/StackCraft/Scripts。

当前已确认的有效参考：

- CardInstance 是卡牌实例，直接保存定义、尺寸、所属牌堆、生命、营养、战斗 / 装备组件和拖拽状态。
- CardStack 是牌堆对象，保存卡牌列表、目标位置、锁定状态和制作状态。
- CardController 处理卡牌拖拽输入。
- CardPhysicsSolver 是牌堆物理解算协作者。
- Board 表达桌面边界和禁放区域。
- GameDirector 表达游戏流程编排，但它的固定场景名、单例链和直接切场景方式不进入正式链路。

吸收边界：

- 吸收卡牌、牌堆、牌桌、拖拽、放置、解算的玩家可见功能和对象直观性。
- 不照搬 StackCraft 的全局单例、直接 Transform 改状态、固定场景名、旧输入系统和职责混合。
- 当前卡牌交互相关模块优先以 StackCraft 证明对象模型；2DRPGEngine 不能替代 StackCraft 证明牌桌对象设计。

### 2DRPGEngine / Mythril2D

本地参考路径：

- C:/Gamedev/Unity/Engine/2DRPGEngine/Assets/Mythril2D/Core
- C:/Gamedev/Unity/Engine/2DRPGEngine/Assets/JKFrame

当前已确认存在的职责形态：

- 运行时目录按 Game、Systems、Database、Maps、Save、Quest、Dialogue、Inventory、Combat、Abilities、Entities、UI 等 RPG 闭包组织。
- GameManager + AGameSystem 是进程级系统生命周期参考。
- DatabaseRegistry、DatabaseEntry、Sheet、Reference 是 RPG 长期数据和稳定引用参考。
- MapSystem、SaveSystem、PersistenceSystem、GameFlagSystem、Quest、Dialogue、Inventory、ICommand 是 RPG 世界规则、地图、存档、任务、对话、背包和命令语义参考。
- AbilitySheet + ActiveAbility / PassiveAbility 是轻量技能资产与运行时技能族参考。

吸收边界：

- 2DRPGEngine 对 CardLoop 的价值主要在 GameCore/RPG 数据、地图、存档、任务、对话、命令和技能族闭包。
- 它不是当前卡牌牌桌、堆叠交互、桌面空间和 StackCraft 模块吸收的首要结构参考。
- 不能把 FantasyWord 旧文档里 2DRPGEngine 是 RPG 地基误读为 CardLoop 全部 Gameplay 都要按 2DRPGEngine 组织。

## 正式对象层级

后续 Gameplay 目录和代码应尽量表达这棵对象关系，而不是按技术阶段平铺：

    Gameplay
      Content/        内容作者源、唯一 ID、索引和校验
      Scenario/       剧本定义、单局剧本实例、内容发现、回合/即时节奏、任务日志
      Tabletop/       牌桌聚合、卡牌、牌堆、桌面区域、放置规则
        Cards/        卡牌实例、角色卡派生类型、卡牌状态、牌堆对象、快照
        Placement/    放置规则和内部解算协作者
        Actions/      绑定牌桌卡牌的候选、请求、运行实例、时间换算、快照和结算
        Input/        拖拽意图、释放目标，不拥有写权限
        View/         卡牌视图和投影，不保存第二玩法状态
      Actions/        可复用的行动作者定义、参与条件和结果意图声明
      Quests/         任务定义和任务日志；归属单局剧本，不做进程级任务系统
      Combat/         即时战斗和 EX-GAS 集成；属性/效果真相归 GAS

这只是职责组织示意，不是要求一次性建空目录。只有真实代码或文档需要承载对应职责时才创建文件夹。

## 设计门禁

### 领域对象优先

遇到新名词时，先判断它是不是玩家可感知对象：

- 是：优先建立对象或聚合，让对象持有自己的核心状态和行为入口。
- 不是：再判断它是值对象、作者定义、内部协作者、UI 投影、编辑器工具还是测试夹具。
- 不能因为出现一个名词就创建 XSystem、XContext、XRegistry、XManager 或 XService。

### 继承与组合裁决

先判断领域关系，再决定 C# 结构；不能因为“以后也许扩展”或“想避免空字段”直接选择组合、接口或继承。

| 事实关系 | 正式结构 | 约束 |
|---|---|---|
| 子对象在整个运行生命周期内都是父对象的一种，并且必须参与父对象的核心契约、牌堆、放置、拖拽、快照或查询 | **继承** | 派生类直接继承父类，父级集合保存基类引用；不要把这个稳定子类型伪装成父类上的可选组件。 |
| 行为、效果或表现可以独立添加/移除、可同时出现多个、或有独立生命周期但不改变对象身份 | **组合** | 被组合对象必须有自己的真实状态、唯一 owner 和可解释生命周期；不能只是为避免类型判断而包一层字段。 |
| 只是一次规则计算、查询、投影、校验或短暂输入姿态 | **不建立领域组件** | 作为父级对象的方法、值对象或内部协作者处理；不要为了名词再建一个可选运行时对象。 |

裁决步骤：

1. 用自然语言验证“X 是不是永远是一种 Y”。成立时优先继承；例如角色卡永远是一张牌桌卡，因此是 `CharacterCard : TabletopCard`。
2. 确认该关系是否会在正常玩法中动态添加、移除或叠加。会变化的能力、状态、装备效果、临时地点权限和表现才优先组合或由 GAS 处理；它们不能反过来改变卡牌的 C# 身份。
3. 确认候选组合对象是否拥有独立作者源、运行时状态、存档/联机边界或生命周期。没有其中任何真实职责时，删除该对象，不得把稳定子类型拆成“基类 + 可空组成部分”。
4. 若证据仍不足，不新增混合模型。先在模块替换清单记录待裁决关系，等作者源和生命周期明确后再落代码。

本项目的具体约束：

- `CharacterCard` 是 `TabletopCard` 的派生类型，并直接拥有唯一 EX-GAS `AbilitySystemCell`。
- `CharacterBase` 是 GameCore 的 2D 场景角色，不是 `CharacterCard` 的父类或组成部分。同一逻辑角色不得同时由两者拥有 ASC；要接入场景表现时，必须先重构唯一 ASC 归属，不能加桥接或同步副本。
- 普通物品、地点和事件卡保留为 `TabletopCard`。卡牌在规则下临时提供地点交互，不会因此改变为另一个卡牌派生类型。

### 唯一写入口

状态必须由最接近业务不变量的对象修改：

- 卡牌位置、牌堆成员、拆堆、合堆和放置由牌桌 / 牌堆聚合提交。
- 角色卡的能力、属性、动态标签和 GameplayEffect 由 `CharacterCard.AbilitySystem` 提交；`CharacterCard` 继承 `TabletopCard`，普通卡不创建角色状态。
- 行动进度、完成、取消和结算由当前单局内的行动实例提交。
- 当前单局发现了哪些内容由 `ScenarioRun` 持有和修改，牌桌候选查询不能绕过这项剧本事实。
- 任务进度由单局剧本实例拥有的任务日志提交。
- `ScenarioRun` 必须持有创建时已冻结的本次内容查询集合；`ScenarioDirector` 只负责按正式内容解析链创建单局，不能从 `GameManager` 读取一个进程级全局内容索引。`ContentIndex` 是这份查询集合的内部协作者，不是新的运行时总管。
- 资源加载、事件派发、标签、属性和效果优先回到既有正式职责归属，不新增第二套入口。

如果非法状态只能由绕过正式入口形成，应收窄入口或直接抛错，不新增防护表、兜底状态或静默修复。

单局结束状态只有一份，记录在其唯一牌桌上。`ScenarioRun` 读取该状态约束回合、实时推进和发现写入，不再维护第二个结束标记；调用方可以保留终局只读状态或快照，但旧牌桌引用不能继续创建、移动卡牌或执行行动。

### 协作者降级

Solver、Resolver、Projector、Validator、Index 的存在条件：

- 它背后有足够复杂的算法或转换，删除后复杂度会分散到多个调用方。
- 它只服务一个父级对象时，应放在父级对象目录下，并尽量设为 internal。
- 它不能保存第二份玩法真相，不能要求作者手填内部 key。
- 它的公开 API 必须比实现更小；否则就是浅模块，应并回父级或删除。

### 目录表达对象

目录应优先表达游戏对象和生命周期，而不是表达实现阶段：

- 推荐：Tabletop/Cards、Tabletop/Placement、Tabletop/Actions、Scenario、Actions。
- 谨慎：把所有 State、Snapshot、Resolver、Projector、System 同级堆在一个大目录。
- 禁止：为了以后可能有提前建空目录、空接口、空 Context 或无真实职责的 Manager。

### ECS / 数据导向例外

只有满足以下条件之一，才允许用 ECS / 数据导向式拆分作为主结构：

- 明确使用 Unity DOTS / Entities，并有真实性能目标。
- 对象数量、查询频率或热路径已经被 profiling 证明需要数据布局优化。
- 联机同步或回放需要独立数据流，但仍要保留玩家可感知对象的聚合入口和调试视图。

否则默认保持 OOP / 聚合对象模型，内部算法可局部数据化。

### Mod 与联机

Mod 和联机扩展通过以下方式保证，不通过贫血拆分保证：

- 内容作者源使用唯一内容 ID、GAS 标签、资源系统和内容包依赖，不允许第二套身份。
- `ContentIndex` 建立后必须同时冻结按 ID 查询表和公开内容集合；不能让调用方通过强转公开列表改变内容枚举结果而不改变 ID 查询结果。
- 运行时改动通过命令或请求进入聚合根，聚合根复核权限、版本、内容存在性和随机源。
- 可保存 / 可同步状态通过快照表达；快照保存事实，不保存 Unity 表现对象或资源句柄。
- 客户端 UI 可以生成候选和意图，但不能绕过单局 / 牌桌 / 行动实例直接写权威状态。
- 过期联机指令、缺失 Mod 内容、版本不匹配属于真实外部失败，可以显式拒绝；内部不变量破坏必须直接报错。

### 内容校验扩展

- 内容校验入口只负责所有内容共有的身份、标签和校验调度，不得直接识别全部行动、任务、剧本或牌桌具体类型。
- `ContentAsset` 派生定义负责自己的作者数据和跨内容引用校验；`SerializeReference` 多态子项也必须拥有对应的受保护校验入口。
- Mod 派生内容通过同一对象校验钩子扩展，不建立第二验证器注册表、第二内容索引或按类型名分发的中央 switch。
- 内容校验上下文只是在一次索引建立期间提供只读内容查询和问题报告，不保存运行时玩法状态。

## 回审口径

### 阶段集成门禁

领域模块只证明职责边界正确，不能单独证明参考模板已经被新框架吸收。Gameplay 地基必须同时维护跨模块纵向验收：

1. 技术宿主和内容作者源完成后，验证正式进程入口可以加载内容并正常关闭。
2. `ScenarioRun`、`Tabletop` 和牌桌行动的当前正式职责完成后，立即在统一 `FoundationTest` 场景复现 StackCraft 核心卡牌行动闭环，不等待 UI、存档或联机全部完成。
3. 剧本日程、任务、旅行和战斗完成后，在同一闭环上追加运行玩法验收，不能另建测试专用玩法链。
4. 正式 UI、存档和作者工具完成后，对照功能矩阵验收所有选择吸收的模板功能；只有这一步通过，才能声称模板功能吸收完成。

每个阶段必须记录：已复现功能、明确排除及产品理由、尚未完成的阻塞项。测试只能证明它直接覆盖的行为，旧模板场景可运行不能证明新框架等价，模块分别通过也不能替代阶段闭环。

阶段验收不是最小实现策略。模块仍须按正常生产框架处理当前已知对象关系、生命周期、失败语义和扩展边界；不得为了提前出画面保留临时接口、测试专用 owner、残缺状态模型或计划在后续推倒的实现。

每个已经完成或准备继续的模块，都必须用以下问题回审：

1. 玩家可感知对象是什么？它现在有没有直观对象承载？
2. 谁拥有核心状态？是否存在第二份状态、第二套 ID、第二套资源、第二套事件或第二写入口？
3. 参考源里同职责怎么组织？StackCraft、2DRPGEngine、UE/Unity 分别能证明什么，不能证明什么？
4. 当前结构更像 OOP 聚合，还是 ECS 式平铺？如果是后者，是否真的有 DOTS/性能/同步证据？
5. 删除某个 System、Context、Solver、Projector 后，复杂度会回到多个调用方，还是会直接消失？
6. 新框架是否能复现 StackCraft 被选择吸收的玩家可见功能？
7. Mod、关卡编辑器、联机和存档压力下，是否需要新增第二真相？如果需要，说明设计还没收口。

不能只用测试通过证明架构正确。测试只能证明覆盖到的行为；架构正确还必须有对象边界、职责归属、参考对照和扩展压力证据。

## 当前订正方向

- 模块 1（内容定义）继续保留唯一内容 ID、SO 作者源、GAS 标签和资源系统归属，但要避免把所有内容做成万能父类或强迫非卡牌内容拥有卡面。
- 模块 2（剧本 / 单局生命周期）继续保留 ScenarioDirector -> ScenarioRun 的单局聚合方向，但要防止重新引入进程级浅系统。
- 模块 3（牌桌）应继续向牌桌拥有卡牌和牌堆对象靠拢，目录和类型命名要体现卡牌对象、牌堆对象、桌面区域和内部放置协作者。
- 模块 4（行动）保留候选、请求、行动实例三阶段，但运行对象必须属于当前单局 / 牌桌聚合，结果结算不能直接散落到外部系统。
- 后续战斗、任务、职业和 UI 不能提前按空系统拆分；必须先有玩家可见对象、作者源和生命周期。
