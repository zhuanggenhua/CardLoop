# Gameplay 地基工作记录

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
- 已由新框架复现：日程推进、任务完成、同一角色跨地区旅行、角色 GAS 状态连续、牌桌战斗创建、正式 Ability / Timeline / GameplayEffect 固定伤害和战斗结束后的单局状态连续性。明确排除：战斗存档、StackCraft RPS、正式命中 / 闪避 / 暴击公式。尚未完成但不阻塞阶段 B：正式 UI、非战斗单局存档、作者工具、自动战斗调度、联机和 Mod 协议。
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
- 明确排除：StackCraft `CardInstance.CurrentHealth`、`CombatStats`、卡牌自行扣血和 `EquipmentPanel` 不进入正式链路；完整职业、装备、技能、经历侧栏等待对应领域对象和作者源成立后再进入正式 UI。模块 7.3 当前范围完成，下一步进入 7.4 HUD 与交互反馈。

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
