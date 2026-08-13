# Gameplay 地基重新拆分：发现与裁决

## 2026-08-11：模块 3.4 牌桌表现裁决

1. `TabletopCardViewProjector` 名称把一个完整牌桌表现对象伪装成技术投影协作者；实际职责覆盖卡牌、战斗姿态、行动进度、拖拽表现和全部资源句柄。正确结构是一个 `TabletopView` 深模块，不是继续拆分，也不是保留误导名称。
2. `TabletopCardSettings` 同时包含牌桌级战斗排序和行动进度资源，因此不是单卡设置；现改为 `TabletopViewSettings`。权威空间尺寸不回到该设置。
3. `m_viewRoot` 在统一场景中始终手填为组件子节点，但组件自身已经是牌桌表现 owner。删除额外根引用后，Transform 层级只有一个正式入口。
4. `TabletopCardView` 原先复制卡牌 ID 和内容 ID；现保存对应卡牌对象并派生两项身份。姿态同步也直接读取 `TabletopCard.Stack`，不再横扫牌桌重建已存在的对象关系。
5. 设置 getter 原先会运行时创建空资源引用，拖拽锐度会自动夹取。两者都会掩盖作者错误；现统一在绑定或调用边界明确失败。
6. 资源句柄、卡面缓存和视图实例表都是同一牌桌表现生命周期的必要内部状态，不是第二玩法真相；它们继续由 `TabletopView` 随解绑统一释放。
7. 模块 3 最终全量验证为 EditMode `425/426`（`1` 条既有忽略）、PlayMode `30/30`。

## 2026-08-11：模块 3.3 拖拽输入裁决

1. 旧会话用牌桌世界距离判断点击/拖拽，相机缩放会改变同一鼠标位移的判定结果。正式实现改用唯一 `EventSystem` 的像素阈值，牌桌坐标只服务空间投影。
2. 旧输入把鼠标位置直接当牌堆锚点，从卡牌边缘按下会让卡牌跳到鼠标中心。现在保存“鼠标到牌堆锚点”的牌桌偏移，预览与释放请求都保持该偏移。
3. 释放位置原先同时表达鼠标落点和牌堆请求位置。当前意图区分 `ReleasePointerPosition` 与 `RequestedStackPosition`，避免边界解算使用错误坐标。
4. 输入组件原先要求手填命中层、最大射线距离、拖拽距离和牌桌平面。四者分别可由视图类型、主相机、正式 `EventSystem` 和组件自身 Transform 推导，现已全部删除。
5. 输入只读取当前牌桌并产生意图，不拆堆、不合堆、不移动权威状态，也不直接执行行动。非法装配为不同牌桌或缺失正式 `EventSystem` 时直接报错。
6. 新鲜验证为会话 `6/6`、Foundation 输入 `13/13`、全量 EditMode `425/426`（`1` 条既有忽略）、全量 PlayMode `30/30`。

## 2026-08-11：模块 3.2 放置规则裁决

1. 旧实现让每个 `TryPlaceStack` 调用方传入边界、卡牌尺寸、XY 步进和解算轮数；统一测试场景又从视图设置反向组装规则，因此同一牌桌可以同时存在多套空间事实。
2. StackCraft 的有效参考是 `Board` 拥有边界、`CardPhysicsSolver` 只负责算法。正式实现据此让 `ScenarioDefinition` 配置一次，`ScenarioRun` 创建牌桌时冻结一次，之后输入、视图和行动都读取同一个 `Tabletop.PlacementRules`。
3. 卡牌尺寸和 XY 步进决定权威占地，不能属于视图设置；Z 深度、排序和拖拽锐度只影响表现，继续属于当前 `TabletopViewSettings`。
4. `MoveStack` 和无规则创建会绕过牌桌不变量，现已删除公开移动旁路，并让正式创建走同一候选解算。快照恢复若需要自动挪动才能满足当前规则会直接拒绝，不静默修正。
5. 最大解算轮数是算法实现细节，不是剧本规则，已经从 SO 作者源和运行时规则中删除。作者只填写玩家可感知的空间事实。
6. 行动结果原本先删除参与卡再逐张创建产物，空间不足会留下部分提交。现在先预演完整结果，正常空间失败发生在任何写入之前。
7. 新鲜验证为牌桌定向 `11/11`、行动结算定向 `11/11`、全量 EditMode `423` 通过 / `1` 条既有忽略、全量 PlayMode `30/30`。

## 会话恢复校准（2026-08-11）

1. 当前主线没有变化：吸收 StackCraft 的可复用能力，建立 CardLoop 自己的 Gameplay 地基；不复制 StackCraft 的玩法或 UI，不提前实现荒岛、职业、战斗、联机或 Mod 业务。
2. 当前模块划分继续以生命周期和领域聚合为准：模块 0 技术宿主，随后依次是内容作者源、单局剧本、牌桌、牌桌内战斗、正式 UI、作者工具、快照存档、联机与 Mod 边界。不得按 StackCraft 的目录、Manager 或 System 名称重新拆成平铺模块。
3. 当前执行点是模块 0.4。审查对象是 `SceneKit`、`MapSystem`、`TransitionSystem` 与项目资源场景加载器的技术加载、卸载、资源句柄和事件时序；剧本选择或组合哪些场景仍属于后续 `ScenarioRun`，不在本切片实现。
4. 已有源码证据显示：`SceneKit` 的 Single 加载会调用 `ClearScenesForSingleMode`，而正常显式卸载会回收场景加载器。是否因此遗漏 YooAsset 场景句柄仍是待验证风险，必须用资源加载器源码和真实切换用例确认后才能定性或改动。

## 模块 0.4：场景加载后端初步裁决（2026-08-11）

1. 前一条“Single 切换可能遗漏句柄”的候选不成立：`ClearScenesForSingleMode` 调用场景句柄的回收，继而调用外层加载器的回收；当前 `ResKitSceneLoader.Recycle` 会继续调用下层加载器的 `UnloadAndRecycle`。
2. 真实风险在显式卸载：`SceneKit` 在卸载完成后只把 `ResKitSceneLoader` 放回其池中，而该池不会调用加载器的 `Recycle`。因此它持有的项目 `ResourceSystemSceneLoader` 不会释放其 YooAsset `SceneHandle`，也不会从“正在使用资源包”的集合移除。这个机制能解释未来 Mod 包在场景已卸载后仍无法卸载的现象。
3. `ResourceSystem` 当前只把项目加载器注册给 `ResKit`，形成 `SceneKit -> ResKitSceneLoader -> ResourceSystemSceneLoader -> YooAsset` 四层链。`SceneKit` 已公开 `SetLoaderPool(ISceneLoaderPool)` 扩展点；应把项目后端直接接入该正式入口，删去中间 `ResKitSceneLoader` 层，不修改第三方源码。
4. `MapSystem` 同时持有技术场景切换与旧 RPG 检查点、重生、角色传送、地图存档、导航等职责。CardLoop Foundation 当前只有它的技术场景切换能力需要吸收；模块 0.4c 必须将其与旧地图业务分离，不能继续用“地图”称呼全局技术场景生命周期。

## 模块 0.4 完成（2026-08-11）

1. 项目场景后端不再经过 `ResKitSceneLoader` 中转。`ResourceSystem` 直接通过 `SceneKit.SetLoaderPool` 接入项目 YooAsset 场景加载器；加载、显式卸载、Single 替换与句柄释放由一个加载器实例收口。
2. 场景加载器在显式卸载回调和异步加载返回无效场景时释放 YooAsset 句柄，并从资源包占用集合移除。真实 PlayMode 回归验证“附加场景卸载后，资源包不再占用”。
3. `SceneSystem` 接管技术场景切换，`TransitionSystem` 保留为淡入淡出播放对象。前者拥有整次切换的串行状态，后者拥有动画状态，两者职责不重叠。`SceneTransitionCompletedEvent` 只在成功时发送；`SceneTransitionEndedEvent` 统一表示流程结束，输入系统订阅后者恢复输入。
4. `MapSystem` 不再维护技术场景地址、加载、过场或场景事件；它保留检查点、重生、传送、地图配置、导航和旧地图存档。需要跨场景时调用 `SceneSystem`。
5. SceneKit 当前没有真正取消底层 YooAsset 场景加载的官方路径。项目侧不再把销毁令牌传给只会取消等待的包装 API，而是等待实际加载收口；强制取消场景加载不是本模块已实现能力。

## 本轮需求

- 主线仍是吸收 StackCraft 的框架能力，建立 CardLoop 自己的可扩展地基。
- 模板不是默认复制对象；先判定是否属于最佳实践，再选择整体保留、提取改造、自行实现或排除。
- 不提前实现荒岛、职业、经济、联机玩法或 Mod API；但每个设计必须避免把 Mod、关卡编辑器和联机写死。
- 每个大模块必须在统一测试场景证明已选择的玩家可见能力，再进入下一个模块。

## 证据来源

- `.spec/knowledge/features/project/stackcraft-system-reference-matrix.md`
- `.spec/knowledge/features/project/gameplay-foundation-proposal.md`
- `.spec/knowledge/features/project/gameplay-foundation-reaudit.md`
- `Assets/StackCraft/Scripts/`
- `Assets/Scripts/GameCore/Runtime/`、`Assets/Scripts/Gameplay/Runtime/`、`Assets/Plugins/GAS/`

## 当前已确认的结构

1. 进程级基础设施由 `GameManager` 和既有 GameCore 系统承担；Gameplay 不能另造启动、资源、输入或事件总线。
2. 单局对象模型是 `ScenarioDirector -> ScenarioRun -> QuestLog / Tabletop`。
3. `Tabletop` 是牌桌聚合，`CharacterCard : TabletopCard` 直接拥有唯一 EX-GAS `AbilitySystemCell`。
4. 普通行动由 `ActionDefinition -> ActionCandidate -> ActionRequest -> ActionInstance` 表达，运行实例归当前牌桌所有。
5. 战斗当前只完成参战关系与阵型投影；攻击、命中、实时调度和 AI 不能从模板 `CombatTask` 直接移植。
6. 当前 `FoundationTest` 已验证拖拽、选择行动、确认回合、产物和任务的最小链路；测试 UI 不是正式 UI。

## 新模块划分理由

原有模块顺序混合了“框架整合”“已完成切片”“未来业务”和“跨模块约束”。新计划按真正的生命周期依赖分层：先明确进程与内容会话，再建立单局和牌桌，再建立行动和剧本流程，随后才接入战斗、正式 UI、作者工具、存档与联机协议。Mod/联机不再被误解为最后才处理，而是每一步的边界检查。

## 当前下一步

模块 0 已完成当前吸收范围。下一步进入模块 1.1：审查内容作者源的最小共同契约与唯一身份；不进入内容集合、剧本、牌桌、职业或其它原创玩法业务。

## 内容集合归属订正（2026-08-11）

1. `ContentAsset`、唯一内容 ID、EX-GAS 静态标签、内容引用和单资产作者校验是静态作者源；它们归模块 1。
2. 已解析的 `ContentIndex`、跨资产引用校验、YooAsset 句柄和“当前能查询哪些内容”必须跟随具体单局的开始和结束，因此归模块 2 的 `ScenarioRun`，不是进程级 `GameManager` 的全局玩法状态。
3. 当前 `ContentRegistrySystem` 用固定 `gameplay-content` 标签加载全部内容，无法表达一个剧本实际选择的基础内容和 Mod 组合。它不再被视为可保留的正式 owner；模块 2.1 应迁移并删除该全局入口，而不是给它增加包装层。
4. 这次只是重新划分职责与执行顺序，尚未修改内容运行时代码。模块 1.1 先判断 `ContentAsset` 是否仍是足够狭窄的静态技术基类，再决定保留、收窄或删除其字段。

## 模块分层订正（2026-08-11）

1. 原计划的高层对象顺序仍成立：内容作者源 -> 单局剧本 -> 牌桌 -> 行动 -> 剧本规则 -> 战斗 -> UI / 工具 / 存档 / 联机。
2. `GameManager`、资源、Mod 运行时、EX-GAS、输入、事件和 Unity 场景加载后端不是第一个玩法模块，而是让第一个玩法模块能安全进入运行态的技术宿主前置条件，统一保留为模块 0。
3. 原模块 0 的“场景与单局边界”拆法错误：场景加载后端可在模块 0 审查，但一次剧本要组合、切换和释放哪些场景，属于 `ScenarioRun` 的生命周期，移入模块 2。这样不会出现 `MapSystem` 和剧本运行实例各自维护一份单局状态。
4. 当前继续点不变：先以现有 `ResourceSystem` 生命周期测试完成 0.2a.1；没有因此提前开始内容、剧本或原创玩法实现。

## 模块 0.2a.1：资源系统原子生命周期完成（2026-08-11）

1. **原始失败现象**：外部入口直接完成 YokiFrame / YooAsset 初始化后，调用 `ResourceSystem.Shutdown()` 没有拒绝，反而销毁了外部资源运行时。定向 PlayMode 测试 `Shutdown_WhenYokiFrameOwnsResources_ThrowsWithoutReleasingExternalState` 首次结果为 `5/6`，该项预期异常却得到空值。
2. **根本机制**：`ResourceSystem.Initialized` 原先只根据“本类保留的默认资源包非空 + YooAsset 已初始化”判断。YooAsset / YokiFrame 的公开状态只描述全局资源框架是否运行，不描述启动者，因此这两个事实不能推出本类拥有关闭权。
3. **正式修正**：`ResourceSystem` 只在自己开始调用 `YooInit.InitAsync` 前记录私有的启动责任；成功就绪状态同时要求该责任、默认包、YokiFrame 和 YooAsset 都成立。公共关闭入口只有在本类持有责任时才销毁资源；外部或半初始化状态直接抛出。该责任不是第二套资源数据，也不暴露给 Gameplay，它只补足第三方公开 API 缺失的所有权事实。
4. **验证**：修正后同一资源生命周期测试为 `6/6`；`FoundationTestScenePlayModeTests` 为 `11/11`，`ContentRegistryPlayModeTests` 为 `3/3`，`GameManagerAndGameStateLifecycleEditModeTests` 为 `9/9`。这些结果覆盖资源启动/关闭、既有牌桌场景、内容索引和进程级启动回归，不证明 Mod 内容包、GAS 生命周期或正式存档已经完成。

## 模块 0.2a.2：ModAPI 原子生命周期完成（2026-08-11）

1. **原始失败现象**：`IModLoader` 明确返回失败时，旧 `ModAPI.Initialize` 直接正常返回；第一次加载仍在进行时，第二次调用也能进入，关闭后迟到的完成结果还能继续访问已被清空的静态配置。
2. **根本机制**：旧实现把静态 `ModInfos` 直接作为加载中的临时容器，只有最终成功分支才写 `Initialized`，却没有“初始化进行中”和“关闭已取消本轮提交”的生命周期事实。因此失败、并发和关闭之间没有唯一的提交边界。
3. **正式修正**：Mod 扫描先写入局部暂存列表；`ModAPI` 只在扫描、状态清理、配置保存都成功且未被关闭后，一次性提交清单、配置和初始化状态。初始化期间使用仅由本模块持有的取消源拒绝并发进入；`Shutdown` 取消未完成初始化，失败或取消会清理 ModAPI 自己的清单与配置。资源包不由 ModAPI 回收，仍由 `ResourceSystem` 的既有资源所有权统一释放。
4. **验证**：新增的生命周期定向测试先以 `1/3` 复现两个缺陷，修正后为 `3/3`；Mod 路径安全测试为 `2/2`，`GameManagerAndGameStateLifecycleEditModeTests` 为 `9/9`，`FoundationTestScenePlayModeTests` 为 `11/11`。这些证据不代表 Mod 依赖解析、内容合并、脚本插件、联机 Mod 协议或创意工坊已经实现。

## 模块 0.2a.3：EX-GAS 项目组合入口完成（2026-08-11）

1. **原始失败现象**：EX-GAS 已由其它入口直接创建 World 与标签图时，项目组合入口仍继续调用插件初始化。插件只写重复初始化警告，随后项目侧尝试重复建立标签图；所用反射辅助只写错误日志并返回空值，调用链没有把失败交给 `GameManager`。
2. **根本机制**：`FormalAbilityRuntimeBootstrap` 原来只用本地“已初始化”标记判断重入，没有先辨别已有 GAS World 是否由项目启动；其生成缓存调用通过会吞异常的 `ReflectionHelper.InvokeStaticMethod`，所以标签图或生成缓存失败会被误当作普通返回。
3. **正式修正**：组合入口在写入项目状态前检查 EX-GAS World、运行状态和全局计时器；已有状态一律视为外部入口并直接拒绝，不再接管或关闭。项目自身启动失败时只回滚它本轮已进入的 GAS 生命周期。跨程序集生成入口仍通过反射调用，但现在解析类型/方法并传播目标异常，不能只写日志后继续。`GasEditModeTestHelper` 与牌桌战斗测试均改为调用正式组合入口，删除了测试对项目私有状态的反射改写和直接 `GASManager` 启停。
4. **验证**：新增外部 GAS 生命周期用例先以 `1/2` 复现问题，收敛后为 `2/2`；伤害扩展为 `7/7`，牌桌战斗为 `5/5`，战斗阵型为 `3/3`，`GameManagerAndGameStateLifecycleEditModeTests` 为 `9/9`，`FoundationTestScenePlayModeTests` 为 `11/11`。这些结果仅覆盖 EX-GAS 进程启动/关闭、生成缓存、现有伤害扩展和战斗测试入口；不代表新技能、职业、战斗实时调度、动态 Mod 标签或网络同步已实现。

## 模块 0.2a.4：三项基础设施组合审查完成（2026-08-11）

1. **原始失败现象**：资源初始化尚未完成时调用 `ResourceSystem.Shutdown()`，旧实现把它当作完整初始化状态并进入释放分支，直接抛出“YokiFrame 或 YooAsset 状态不完整”。
2. **触发机制**：项目入口已经取得本轮资源启动责任，但 YokiFrame 的全局初始化还没有完成；第三方取消令牌只能中断等待，不能清除已登记的资源包字典，因此不能由同步关闭入口假设第三方已处于可销毁状态。
3. **正式修正**：资源入口保存本轮取消源。关闭发生在初始化期间时，只取消本轮结果提交；YokiFrame 初始化继续收敛，之后初始化入口统一执行回滚并传播取消异常。回滚完成前 `s_ownsResourceRuntime` 仍阻止新的初始化进入，避免两次初始化竞争同一进程状态。
4. **验证**：资源生命周期测试从 `6/7` 收敛到 `8/8`，新增覆盖关闭中取消和取消回滚期间重入拒绝；`GameManagerAndGameStateLifecycleEditModeTests` 为 `9/9`；`FoundationTestScenePlayModeTests` 为 `11/11`。
5. **边界**：没有修改 YokiFrame / YooAsset / Mod / EX-GAS 第三方源码；没有把取消控制扩展成第二套资源状态，也没有进入 Mod 内容依赖、联机或玩法实现。

## 模块 0.2b：GameManager 组合生命周期完成（2026-08-11）

1. **原始失败现象**：YokiFrame 已由外部入口完成资源启动时，`GameManager.Start` 调用资源入口会正确失败，但旧代码已提前记录“已进入资源初始化”，失败清理又调用 `ResourceSystem.Shutdown()`，生成一条与原始冲突无关的“资源系统关闭失败”异常。
2. **根本机制**：三个布尔标记表达的是“尝试过初始化”，却被关闭链当成“已成功取得关闭责任”。两种事实不同，导致外部状态或子模块自回滚失败路径被进程入口误关闭。
3. **正式修正**：标记改为“本管理器已成功启动该运行时”，仅在成功返回后写入；未完成资源与 Mod 初始化接收 Unity 销毁取消令牌并在各自边界自行回滚/拒绝迟到提交。成功关闭顺序是系统、GAS、Mod、资源。
4. **验证**：外部资源冲突 PlayMode 用例从 `0/1` 到 `1/1`；Mod 取消契约 `4/4`；资源 `8/8`、进程系统 `9/9`、GAS `2/2`、统一地基场景 `11/11`。这些证据验证进程级组合边界，不代表 Mod 扫描可以被即时中断或 Mod 内容协议已完成。

## 模块 0.2c：真实 FoundationTest 组合回归完成（2026-08-11）

1. **验收目标**：不能只证明各基础设施各自能启动或关闭，必须在真实 FoundationTest 的 GameManager、资源、Mod、GAS、输入和牌桌组合完成启动后，验证销毁唯一进程入口不会留下运行时。
2. **正式验收**：新用例在场景进入 Ready 后确认 `ResourceSystem`、`ModAPI`、`GASManager`、`YooInit`、`YooAssets` 都已运行；销毁 GameManager 后确认它们全部为未运行状态，且 GAS World 已释放。
3. **验证**：定向真实关闭用例 `1/1`；完整 `FoundationTestScenePlayModeTests` `12/12`。测试期间没有未预期控制台错误。

## 模块 0.1 发现

1. `GameManager` 是唯一进程入口：按“资源 -> Mod -> GAS -> 已装配 `AGameSystem`”启动，子系统只从自身层级收集并按声明依赖排序。`FoundationTest` 真实装配 `ContentRegistrySystem`、`ScenarioDirector`、`MapSystem`、`TransitionSystem`、`GameStateSystem` 和输入系统，单局不属于进程级系统。
2. `ModAPI.Initialize` 已经由 `GameManager` 唯一调用，但遇到第二次调用只记录日志并返回；这会让错误调用看似成功，违背唯一启动入口的失败语义。已新增一条只使用空 Mod 加载器的 EditMode 回归测试，待 RED 后修复。
3. 旧 PuerTS MCP 测试端点配置为 `127.0.0.1:18990`，当前无响应；实际 UnitySkills 服务在 `127.0.0.1:8090` 正常运行。验证将改走 UnitySkills 正式 REST 入口，不把工具端口故障当作代码测试结果。

## 模块 0.1a 验证结果

1. `ModAPI.Initialize` 的第二次调用原先只输出错误日志后返回，调用方会误以为重复启动成功。该行为已经用 `ModApiLifecycleEditModeTests` 复现：第一次运行 `0/1` 通过，失败信息明确指出预期异常为空，控制台仍只有旧日志。
2. 正式修正只发生在 `Assets/Scripts/GameCore/Runtime/Mods/ModAPI.cs`：重复初始化现在直接抛出 `InvalidOperationException`，不增加包装层、状态表或第二个启动入口。
3. 外部文件编辑后，Unity 没有立即重新导入脚本；首次绿测仍命中旧日志。经 Unity `AssetDatabase.Refresh` 后，编译状态为空闲，目标测试 `1/1` 通过；这证明问题是编辑器未刷新，不是存在第二份 `ModAPI`。
4. 回归 `GameManagerAndGameStateLifecycleEditModeTests` 结果为 `8/8` 通过，覆盖了受管理子系统的发现范围、依赖排序和逆序关闭。它是 0.1b 的已有行为证据，不等于 0.1b 的源码职责回审已经结束。

## 模块 0.1b 裁决

1. 原始 2DRPGEngine 的 `GameManager` 会用全场景扫描收集全部 `AGameSystem`，而 CardLoop 当前实现只收集 `GameManager` 自身层级中的系统，并按声明依赖排序。这避免地图、牌桌或临时场景对象被误登记为进程级系统，应保留这个改造方向。
2. `FoundationTest` 的实际装配也符合该边界：`GameManager` 对象本身承载全局状态、场景过渡、地图、内容索引和剧本导演；新输入系统作为唯一子节点。它不是把每个 Gameplay 对象变成全局系统。
3. 发现一处与唯一启动语义冲突的残留：`GameManager`、`GameStateSystem` 先前对重复初始化/启动静默返回。现已改为直接抛出；“停止后再次启动”仍是合法 Unity 生命周期。
4. 2026-08-10 回归 `GameCore.Tests.GameManagerAndGameStateLifecycleEditModeTests` 为 `9/9` 通过，新增覆盖重复初始化、重复启动和停止后的合法重启；`ModApiLifecycleEditModeTests` 同时 `1/1` 通过。
5. 下一步只进入 0.1c：审查 `GameManager.Start` 的异步失败、取消、禁用和销毁链路是否会留下错误启动状态。资源、GAS、输入、场景和 Gameplay 单局职责仍分别留在后续子模块。

## 模块顺序订正：原 0.1c 并入 0.2

`GameManager.Start` 在调用资源、Mod 和 GAS 前就记录“已经进入初始化”，而三个基础设施当前对重复调用、半初始化和清理的语义并不一致。因此“失败和关闭”不能作为 `GameManager` 的孤立子模块：它必须先审查每个基础设施能否保证原子初始化，再决定进程组合层何时取得关闭权。

后续顺序改为：

1. 0.2a：资源、Mod、GAS 各自的重复调用、部分失败和清理责任。
2. 0.2b：`GameManager` 只在对应基础设施成功后取得关闭权，并审查取消/失败/销毁顺序。
3. 0.2c：在真实 `FoundationTest` 场景回归组合启动与关闭。

这是职责重新归位，不是增加一个新框架模块。

## 模块 0.2a.1：资源系统初步证据

1. `ResourceSystem.InitializeAsync` 当前已完成初始化时直接返回；这会让第二个调用者被误认为取得了资源系统的启动权。
2. YokiFrame 的 `YooInit.InitAsync` 会先创建 YooAsset 全局状态和资源包，再在最后设置自身完成标记；包初始化抛异常时，插件本身不会自动调用 `Dispose` 或 `YooAssets.Destroy`。第三方源码未经授权不改，项目侧 `ResourceSystem` 必须在自己发起初始化后保证失败回滚。
3. 当前 `ResourceSystem` 已经能识别“YooAsset 被其它入口初始化、但 YokiFrame 未完成”的明显冲突；还缺“YokiFrame 已完成但不是 ResourceSystem 发起”的明确拒绝，以及重复调用和项目侧失败回滚的契约。
4. 这个切片只处理资源生命周期，不改变 YooAsset 地址、内容 ID、Mod 包格式或 Gameplay 资源引用入口。

## 模块计划再对齐（2026-08-11）

1. 不重建一级模块表。模块 0 到 9 已按进程宿主、静态作者源、单局剧本、牌桌、行动、战斗、UI、工具、存档、联机与 Mod 的真实生命周期划分；按 StackCraft 的 `Card`、`Crafting`、`Quest` 等目录重新拆分会重新引入模板式平铺系统。
2. 原来的 1.2 同时包含两份不同的作者事实：内容的 EX-GAS 语义标签，以及内容对其它内容的唯一 ID 引用。两者只共享基础设施，不共享状态或写入口，已拆为 1.2a 和 1.2b。
3. 模块 2 的 2.1 也拆为内容集合选择/解析、冻结查询集合、资源句柄归属三步。这样不会把 `ContentRegistrySystem` 的全局加载逻辑换个名字继续保留。
4. 当前源码已显示 1.1 的实现边界：`ContentAsset` 只保留唯一内容 ID、静态 EX-GAS 标签和校验入口；`DisplayableContentAsset` 承担显示字段；纯时间规则不再继承内容资产。但现有工作区改动尚未在本次会话做新鲜回验，因此计划状态必须保持“待回验”，不能提前进入 1.2a。
5. EX-GAS 插件文档确认：静态内容标签和角色动态标签都保存整数标签码，但层级比较必须使用 `TagHelper.HasTag` 或 `AbilitySystemCell` 的正式查询；不能把整数相等或本地标签表当作替代。

## 模块 1.1：最小共同契约与唯一内容身份完成（2026-08-11）

1. `ContentAsset` 的正确公共事实是唯一内容 ID、EX-GAS 静态标签与派生校验入口；名称、描述和图标只由需要展示的 `DisplayableContentAsset` 承担。`TurnTimingDefinition` 作为纯规则参数不继承内容资产。
2. `ContentIdRules` 的默认生成保持“可读文件名片段 + Unity GUID 短 hash”；GUID 只在第一次为空时作为生成种子，已有合法 ID 再执行生成入口不会改写，因此后续改名或移动不会让身份自动漂移。
3. 新鲜 EditMode 验收：`Gameplay.Tests.ContentValidationEditModeTests` 为 `3/3` 通过；新增的 `Gameplay.Tests.ContentIdentityEditModeTests` 为 `2/2` 通过。后者是稳定地基公开契约测试，不是未定玩法的 TDD。
4. 项目知识文档中把 `ContentAsset` 写成直接拥有“最小展示信息”的旧表述已改为 `ContentAsset` / `DisplayableContentAsset` 的真实分层；后续 1.2a 才审查标签作者选择与查询，不提前改内容引用或单局集合。

## 模块 1.2a：EX-GAS 静态标签作者语义完成（2026-08-11）

1. 内容静态标签继续只保存 EX-GAS 的整数标签码。作者字段和现有行动标签字段均直接使用 EX-GAS `GeneralGasChoiceHelper.Tags()` 选择器；没有新增 Gameplay 标签表、枚举、字符串符号表或标签查询包装。
2. 编辑器内容校验从同一官方作者来源读取有效标签码。未知标签明确报错 `CONTENT_TAG_UNKNOWN`；若 GAS 作者数据为空，明确报错 `CONTENT_TAG_AUTHORING_SOURCE_EMPTY`。本次校验的临时集合不保存，也不构成运行时标签索引。
3. 标签层级比较仍由 EX-GAS `TagHelper.HasTag(实际标签码, 查询标签码)` 负责；角色当前状态仍由 `AbilitySystemCell` 查询。整数相等没有被当作父子标签查询。
4. 新鲜验证：`Gameplay.Tests.GameplayTagCodeAuthoringEditModeTests` 为 `2/2` 通过；`GameCore.Tests.FormalAbilityRuntimeLifecycleEditModeTests` 为 `2/2` 通过。前者验证官方下拉与未知静态码拦截，后者验证 EX-GAS 标签图初始化后父子标签查询可用。
5. `Gameplay.Editor` 已声明对 EX-GAS 通用作者程序集的正式依赖。没有修改 EX-GAS 源码、生成表或运行时 GAS 生命周期。下一步只审查现有 `ContentIdReference` 候选是否真的只序列化唯一内容 ID，不能因已存在代码就默认完成。

## 模块 1.2b：单 ID 内容引用作者入口完成（2026-08-11）

1. `ContentIdReferenceAttribute` 只是作者入口标记，真正序列化的字段仍是 `ContentId.m_value` 这一份字符串。`ContentIdReferenceDrawer` 选择 `ContentAsset` 后只写入所选资产的唯一内容 ID，不并列保存 Unity 对象引用、YooAsset 地址、路径、包身份或第二个引用 ID。
2. 选择器的类型限制是编辑器作者约束；行动、任务、剧本和结果意图的具体定义对象分别在自己的 `ValidateContent` / 子项校验入口中检查无效 ID、未知 ID 和错误内容类型。这样引用校验归引用所属领域，不把所有引用反射成一个万能中央系统。
3. 选择器为 Inspector 显示建立的 `AssetDatabase` 映射只是不可序列化的编辑器解析缓存，项目变更时失效；它不进入运行时，不负责资源加载，也不替代模块 2 的活动内容集合。当前场景仍挂有进程级 `ContentRegistrySystem`，该旧内容集合入口继续登记为模块 2.1 的迁移删除候选。
4. 没有代码需要改动：现有实现符合唯一 ID、禁止双重更新和领域对象自行校验的边界。新鲜 EditMode 验证为：`ContentReferenceAuthoringEditModeTests` `3/3`、`ActionDiscoveryAndValidationEditModeTests` `4/4`、`ScenarioDirectorEditModeTests` `1/1`。证据分别为 `Logs/TestResults-Gameplay-ContentReferenceAuthoring-Batch-R3.xml`、`Logs/TestResults-Gameplay-ContentReferenceValidation-Batch-R1.xml`、`Logs/TestResults-Gameplay-ContentReferenceScenario-Batch-R1.xml`。
5. 下一步进入 `1.3`，只审查 `ContentAsset`、`DisplayableContentAsset`、卡牌、行动、剧本、任务和纯规则定义的继承边界；不因为引用测试通过就提前进入活动内容包或剧本业务。

## 模块 1.3：领域定义继承边界完成（2026-08-11）

1. `ContentAsset` 继续是只含身份、静态 EX-GAS 标签和校验钩子的抽象技术基类；`DisplayableContentAsset` 只给真正需要名称、描述和图标的内容定义增加展示字段。没有恢复万能内容父类、地点/物品/角色空壳层级或通用卡面字段。
2. `CardDefinition`、`ActionDefinition`、`ScenarioDefinition`、`QuestDefinition` 都是有独立内容身份和作者生命周期的顶层定义，因此保留可继承的代码 Mod 作者入口。原先只有 `QuestDefinition` 被错误声明为 `sealed`，与其受保护校验钩子和项目 Mod 扩展约束冲突，现已移除该限制。
3. `TurnTimingDefinition` 仍是纯规则 ScriptableObject，不继承内容资产；`QuestTaskDefinition` 与 `ActionResultIntent` 是嵌入所属定义的多态子项；`TabletopCard` / `CharacterCard` 是单局运行时实例。这些对象不因为未来扩展被误塞进内容作者继承树。
4. RED：`QuestDefinition` 可扩展性合同在旧 `sealed` 状态下 `0/1` 失败，证据为 `Logs/TestResults-Gameplay-ContentInheritance-RED-R1.xml`。GREEN：完整内容作者边界测试 `4/4` 通过，证据为 `Logs/TestResults-Gameplay-ContentInheritance-GREEN-R2.xml`；对应 Unity 日志没有编译或 InitializeOnLoad 错误。
5. 下一步进入 `1.4a`，只审查现有内容校验菜单、派生校验钩子和引用选择器是否各自只承担作者体验与错误定位，不能把它们升格为运行时内容目录或资源加载入口。

## 模块 1.4a：作者校验与槽位引用入口完成（2026-08-11）

1. 现有 `ContentValidationMenu` 只在编辑器扫描作者资产并报告问题，运行时 `ContentValidator` 只消费调用方传入的内容集合；两者没有加载 YooAsset、创建全局集合或替代 `ResourceSystem`，因此本轮保留其职责边界。
2. 行动结果的 `RemoveCardsResultIntent.m_slotKey` 与 `CreateCardsResultIntent.m_anchorSlotKey` 是隐藏的内部稳定键。单槽位运行时可以自动推导，多槽位运行时已经要求明确键，但旧 Inspector 没有选择入口，作者只能手改 YAML。
3. 新增选择器只读取所属 `ActionDefinition.ParticipationSlots`，显示槽位的 `DisplayName`，并把选择写回同一个字符串键字段。多槽位不能留空或命中未知/重复键；单槽位空值仍保留原有自动推导语义。
4. `ActionInstanceSnapshot` 的结果分支键和槽位键属于运行时快照，不是作者源字段；不能因为名称相似就把存档事实改成编辑器引用。横向搜索没有发现其它作者源存在同类手填内部键入口。
5. 该切片没有恢复 `RecipeDefinition`、内容万能父类、运行时内容目录或第二套标签/资源入口。下一步只审查 YooAsset 构建期过滤规则与唯一作者源的关系。

## 模块 1.4b：YooAsset 收集规则完成（2026-08-11）

1. 本项目实际 YooAsset 版本是 `3.0.5`，不能直接套用针对 2.x 的 skill 细节。当前源码证明 `FindAssetType` 接收类型名，YooAsset 内部统一拼接 `t:`；现有 `nameof(ContentAsset)` 正确。
2. `ContentAssetFilterRule` 只通过 `AssetDatabase.LoadAssetAtPath<ContentAsset>` 判断资产类型。Collector 负责扫描路径、打包方式和 `gameplay-content` 构建标签；`AddressDisable` 让内容定义不产生并列 YooAsset 地址，内容身份仍只有 `ContentId`。
3. 当前收集规则不会加载资源、创建 `ContentIndex`、选择剧本内容或持有句柄。进程级 `ContentRegistrySystem` 读取 `gameplay-content` 全量建立索引是另一项运行时职责错误，必须在 2.1a 迁移删除，不能归咎于构建过滤规则。
4. 真实 Collector API 验证与运行时加载回归均通过，因此 1.4b 不需要重写配置。模块 1 至此只完成静态作者层；模块 2 才负责把基础内容与 Mod 内容解析为某次 `ScenarioRun` 的冻结集合。
## 2026-08-11：单局内容 owner 订正

1. “当前能查询哪些 Gameplay 内容”属于一次具体剧本运行，不属于整个游戏进程。进程级 `ContentRegistrySystem` 已删除，不能改名或包装后恢复。
2. 当前合法内容来源只有开局时已初始化的默认包与已启用 Mod 包。`ScenarioDirector` 负责在开局边界调用既有 `ResourceSystem`，`ScenarioRun` 只读持有构建完成的 `ContentIndex`。
3. 内容加载、索引构建和初始任务激活任一步失败，都不能发布半成品单局；临时句柄必须由开局流程立即释放。正常结束、系统停止和关闭则释放活动单局句柄。
4. 当前实现没有声明剧本级 Mod 依赖、覆盖或热切换能力。这些需要未来正式 Mod 模块提供协议，不能在内容索引旁边提前增加第二目录、覆盖表或防冲突状态。

## 2026-08-11：ContentIndex 查询边界订正

1. `ContentValidationReport.Issues` 原先只是把内部 `List` 作为 `IReadOnlyList` 暴露，调用方仍可强转并清空错误；这会让“校验失败”失去真实性。现改为真实只读视图。
2. 派生内容校验使用的 `ContentValidationContext.Assets` 原先同样暴露可修改列表；现改为只读视图，保留 Mod 派生内容通过正式校验钩子读取其它作者资产的能力。
3. 同一资产重复传入时，原实现只发警告，之后 `ContentIndex` 才在字典写入处抛出底层重复键异常。现由校验层直接报告明确错误，不自动去重、不覆盖、不继续构建索引。
4. 2.1b 未改变 `ContentIndex` 的查询 API、唯一 ID 规则或资源加载职责；句柄 owner 留给 2.1c。

## 2026-08-11：单局内容句柄 owner 裁决

1. 内容资源句柄是导演创建单局时取得的外部资源租约，正确 owner 是 `ScenarioDirector`；把它移入普通 C# 的 `ScenarioRun` 会让领域对象直接依赖资源基础设施，并不会增加生命周期正确性。
2. `ScenarioRun` 只读持有冻结的 `ContentIndex`。导演在单局发布前完成内容加载、索引构建和初始任务激活，失败时释放局部句柄；结束、停止和关闭通过同一释放入口收口。
3. 新鲜 PlayMode 证明主动结束单局后，原内容句柄失效且活动单局清空；全量 PlayMode `28/28` 通过。
4. `ResourceSystem.UnloadModPackageAsync` 当前会释放使用目标包的活动资源操作，而内容索引可能仍属于正在运行的剧本。由于当前没有正式 Mod 卸载调用入口，这不是 2.1c 要猜测实现的业务；但 9.1 必须在开放运行时 Mod 切换前裁决“先结束受影响单局”或“拒绝卸载”的明确协议，不能让资源层暗中使活动单局失效。

## 2026-08-11：单局创建与结束裁决

1. 当前导演只有“无活动单局”和“存在活动单局”两种真实状态；内容加载通过同步边界完成，没有第二个开局并发入口，因此新增 Starting / Ending 状态机会制造尚无职责的影子状态。
2. 活动单局存在时重复开局在资源加载前直接失败；结束入口清空导演引用、结束牌桌并释放内容句柄。结束后的旧 `ScenarioRun` 仍可读终局事实，但不能继续确认回合或修改牌桌。
3. 重新开始同一剧本会重新解析当前内容集合并创建新的 `ScenarioRun`，回合、任务、发现和牌桌状态不从旧局泄漏。
4. 2.3 的真实场景组合已经证明开局和结束必须可等待，因此正式入口已收口为异步方法；没有保留同步别名或第二套启动 API。
## 2026-08-11：模块与阶段必须分层

1. 领域模块只能回答“职责和状态由谁拥有”，不能证明多个模块组合后已经得到 StackCraft 的玩家体验。
2. 原计划把完整 `ScenarioRun` 内部能力全部排在牌桌与行动前，会延迟第一次纵向闭环，增加长期在局部结构上跑偏的风险。
3. 正确顺序是先按生产标准完成剧本运行时、牌桌和行动的当前正式职责，再立即验证核心卡牌行动闭环；日程、任务、旅行和战斗随后叠加到同一入口，不能另建测试专用链。
4. 阶段门禁不是 MVP 策略。它只缩短组合验证间隔，不能降低模块完整性，也不能允许临时 API、残缺 owner 或后续推倒重写。
4. “所有模块分别通过”不等于“模板功能吸收完成”。完整等价必须在 UI、存档和作者工具接入后，对照功能矩阵逐项裁决，并保留缺失项作为阶段阻塞。

## 2026-08-11：模块 2.3 剧本场景组合裁决

1. StackCraft `GameDirector` 的有效职责证据是“流程入口决定进入场景并在切换完成后继续单局”；固定 `Main` 字符串、直接 `SceneManager.LoadSceneAsync`、单例和跨场景业务搬运不进入正式链路。
2. 当前技术场景唯一入口已经是 `GameCore.SceneSystem -> YokiFrame.SceneKit -> ResourceSystemSceneLoaderPool -> YooAsset`。因此 Gameplay 不新增场景管理器；`ScenarioDirector` 只决定剧本场景和单局发布时机。
3. `ScenarioDefinition.InitialSceneAddress` 是资源地址，不是内容身份。作者选择场景资产后只保存这一份地址；场景型测试资产也从真实 `SceneAsset.name` 推导，不维护手填副本。
4. 活动单局必须在目标场景完成切换并完成初始组合后一次发布。结束顺序相反：先让旧局失效并释放本局内容句柄，再返回来源场景，避免返回过场期间旧局继续接收操作。
5. 运行测试确认 `GameManager` 作为 `DontDestroyOnLoad` 进程宿主不能随普通返回场景再次实例化。重复宿主必须继续报错，正确修正是让剧本 / 返回场景成为纯场景组合，而不是把重复入口降级为静默销毁。
6. 全量新鲜证据为 EditMode `420` 通过、`0` 失败、`1` 条既有忽略，PlayMode `30/30`；这证明当前场景组合、返回和释放合同，不证明模块 5 的剧本内旅行、正式主菜单、存档恢复或联机换场已经完成。

## 2026-08-11：模块 3.1 卡牌与牌堆对象裁决

1. StackCraft 正确表达了“卡牌属于牌堆，牌堆拥有位置和成员顺序”，但 `CardInstance` 同时承担 MonoBehaviour 表现、碰撞、Tween、悬浮信息、战斗、装备、制作和资源数值，不能整体复制。
2. 当前候选的 `TabletopCard` 只有 ID；所属牌堆被单独保存在 `TabletopCardState.m_stackByCardId`。牌堆成员列表和派生字典必须在合堆、拆堆、删除时双重更新，正是对象贫血和第二关系真相。
3. 重构后卡牌直接持有 `Stack`，位置由所属牌堆提供；牌堆内部方法是关系唯一写入口。`TabletopCards` 只保留卡牌 ID 索引和牌堆集合，查询所属牌堆直接读取卡牌对象，不再维护关系字典。
4. `Tabletop` 仍是牌桌聚合根。`TabletopCards` 的写方法保持程序集内部，输入、视图、行动和测试不能取得第二套正式写入口；角色卡继承关系和唯一 ASC 未改变。
5. 新鲜验证为定向 `10/10`、全量 EditMode `421` 通过 / `1` 条既有忽略、全量 PlayMode `30/30`。这些证据覆盖实例、成员关系、快照、拖拽、行动、战斗和投影回归，不替代 3.2 对桌面区域与放置算法的独立审查。
