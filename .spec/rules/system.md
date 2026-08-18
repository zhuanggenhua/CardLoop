# System Rules（系统硬红线）

本文件只写强制红线：必须、只能、不得。具体做法放在 `.spec/knowledge/standards/` 或对应 skill。

## 沟通与汇报

- 必须始终使用中文沟通、写文档和写总结。
- 对用户汇报排查结果时，必须使用听得懂的中文；确实需要提内部字段、日志标签、数据库列名或代码符号时，先说明现实含义，再给原始符号作证据。
- 迁移、复制、接入和启用必须区分：静态复制完成不等于 Unity 已编译，不等于框架已启用。

## 前提锁定

- 修改代码、配置、数据、资源、场景、状态或执行会改变外部结果的动作前，必须锁定四项前提：问题对象、真相来源、目标入口/环境、验收口径。
- 四项任一缺失时，只能继续定位、补证据或问最小问题；不得先实施。
- 用户给出本地路径、外部参考工程、网页、仓库、文档或资源目录作为参考来源时，必须先读取或验证该精确来源。
- 不得把 FantasyWord 的项目事实、任务记录、旧世界观、截图证据或历史决策自动当成 CardLoop 事实。

## 修复与验收

- 止血、降噪、限流、跳过、吞异常、兜底返回、避免崩溃、暂时绕开依赖，只能称为止血、缓解或临时保护；不得称为根因已解决。
- 完成、修好、通过等声称前，必须有新鲜验证证据。
- Bug 修复必须回到原始现实结果验收，不得用日志安静、页面不报错或局部现象消失替代。

## Git 与工作区

- 不使用 `git reset`、`git revert`、强制 `git checkout` 到旧提交等回滚/撤销历史操作。
- 未经用户当轮明确许可，不创建、切换、重建或删除分支、tag、worktree。
- 未经用户当轮明确许可，不提交、不推送、不发布。
- 不得回滚或覆盖用户已有改动；发现与当前任务冲突时先停下说明。

## Unity 与插件边界

- 当前仓库 / Unity 工程名是 CardLoop；正式玩法层代码、程序集、命名空间和作者菜单默认使用 `Gameplay` 作为自有玩法职责归属。除项目事实、仓库路径、迁移记录和外部展示名外，不得再用 `CardLoop` 命名正式玩法模块；`GamePlay` 只是历史拼写，不得新增。
- 当前阶段默认只打 Gameplay 地基和 StackCraft 吸收；《卡牌生存：无限》只作为知识记录和架构约束，除非用户当轮明确切换，不得提前实现职业、剧本、关卡、联机、Mod 业务或原创数值内容。
- StackCraft 吸收必须按 [`knowledge/standards/workflow.md`](../knowledge/standards/workflow.md) 和 [`knowledge/standards/gameplay-architecture.md`](../knowledge/standards/gameplay-architecture.md) 执行；StackCraft 原脚本/原场景能运行、文档裁决完成、内部方法或测试入口存在，都不得声称新框架已吸收完成。
- 吸收 StackCraft 玩法模块前，必须执行系统 skill `D:\codex-home\skills\absorb-reference\SKILL.md` 的父级职责检查；StackCraft 的目录、`Manager`、`System` 和单例边界不得直接变成 Gameplay 模块。
- 同一玩法职责只能有一个正式 owner。行动、配方、制作、剧本目标、战斗、存档、资源加载、事件和 UI 绑定等职责，不得同时保留 Gameplay / GameCore 旧实现、StackCraft 参考实现和新增实现三套并行链路。
- 不得用桥接层、中转层、包装层、兼容壳、浅模块、空壳系统或第二套状态掩盖职责没有收口；设计模式、反模式和防护性架构裁决见 [`knowledge/standards/code-design.md`](../knowledge/standards/code-design.md)。
- Gameplay 地基必须领域对象优先；对象模型、继承/组合、阶段门禁、Mod 和联机扩展按 [`knowledge/standards/gameplay-architecture.md`](../knowledge/standards/gameplay-architecture.md) 执行，不得把主结构拆成平铺的 State / Solver / Resolver / Projector / System 集合。
- Gameplay 内容身份、内部 key、`Gameplay` / `GamePlay` / `CardLoop` 命名、GameCore 通用框架命名和生成物规则按 [`knowledge/standards/code-style.md`](../knowledge/standards/code-style.md) 执行；不得让作者手动维护第二套身份、第二套局部 key 或第二份生成真相。
- 配置真相、唯一依赖入口、资源加载、事件入口、UI/表现和性能边界按 [`knowledge/standards/runtime-implementation-boundaries.md`](../knowledge/standards/runtime-implementation-boundaries.md) 执行；不得新增第二套 YooAsset 加载封装、资源地址真相、事件总线或唯一场景对象引用。
- EX-GAS / GAS 的能力、标签、效果、Cue、Timeline、TargetCatcher 和 GameCore 集成边界按 [`knowledge/features/gamecore-gas.md`](../knowledge/features/gamecore-gas.md) 执行；未登记项目侧集成点不得成为正式能力。
- 接入第三方插件、外部框架或本地 UPM 包前，必须先读插件自带 README / Wiki / SKILL / package.json / 官方仓库文档；插件入口、`Assets/Plugins` / `Assets/ProjectPlugins` 边界和偏离登记按 [`knowledge/features/plugin-docs.md`](../knowledge/features/plugin-docs.md) 执行。
- 第三方插件源码、插件编辑器界面、插件内置样式、插件示例文档或插件生成器本体，未经用户当轮明确许可不得直接修改。
- 迁入的 GameCore、GAS 生成配置、YooAsset 配置和输入配置在 CardLoop 中都必须经过 Unity 编译和运行验证后，才可称为正式启用；修改 Unity 资源时必须保留 `.meta` 文件，避免 GUID 丢失。

## 规范治理

- 新增规则、更新规范、新建或修改 skill 前，必须先判断落点：系统 AGENTS、项目 AGENTS、系统 skill、项目 skill、任务/专项文档。
- 不得把本该进 skill 的具体 SOP、参数、命令顺序、验收清单直接塞进根 AGENTS。
- `.spec` 结构、知识索引、skill 路由有改动时，必须同步相关索引和说明。
