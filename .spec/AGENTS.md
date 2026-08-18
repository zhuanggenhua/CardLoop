# CardLoop - 中心文档

CardLoop 是 Unity 卡牌生存项目。**主 Agent 调度,子 Agent 执行,Skill 是方法,.md 是规则。**
主 loop 理解目标、锁定前提、拆任务、调度、收口:清晰小改动直接编码;创造性工作走 `brainstorming` -> `writing-plans` -> `subagent-driven-development`;多卡并行用 `task-breakdown` 扇出通用 worker;职能子 Agent 只有 `reviewer`(写的人 != 审的人)。

> 知识导航(`knowledge/README.md`)与硬红线(`rules/system.md`)经 `CLAUDE.md` 的 `@import` 每次 init 强制载入,本文件不复述;沉淀 / 同步能力用 `spec-steward` 技能。

## 项目是什么

- Unity 工程根目录:`C:\Gamedev\Unity\Project\CardLoop`。
- 来源工程:`C:\Gamedev\Unity\Project\FantasyWord`。
- 当前迁移状态:已静态迁入 FantasyWord 的插件、本地 UPM 包、GameCore 运行时候选和可复用 AI workflow;迁入裁决见 [`../docs/FantasyWord-framework-migration.md`](../docs/FantasyWord-framework-migration.md)。
- 当前验证状态:2026-08-01 已用 CardLoop 的 Unity `6000.5.4f1` 跑过新鲜 batchmode 验证,Package Resolve 与脚本编译通过;场景运行和业务启用仍需单独验证。
- 当前边界:FantasyWord 的任务记录、业务知识库、截图证据、旧世界观和历史决策没有作为 CardLoop 正式事实接管。

## 调度核心

**子 Agent 名册**(便利镜像;权威是各 `.agent.md`):

| 名称 | 职责 | 何时调度 |
|------|------|----------|
| `reviewer` | 对照任务目标、项目红线和验证证据做隔离审查,产出放行 / 退回裁决 | 高风险代码、Unity 场景/资源、第三方插件、规则结算、保存/加载、发布相关改动;用户明确要求 review;整体收口需要写审分离时 |

> **agents/ 准入门槛:只收「隔离本身即是产出价值」的角色**(当前仅 `reviewer`)。编码 / 拆解不设角色,规程见「编码约定」与 `task-breakdown`。

- **调度取向:快 > 稳 > 好。** 默认并行:文件集互不重叠即并行扇出;串行留给有依赖或文件重叠的工作。
- **默认流程:** 创造性工作(新功能 / 建组件 / 改行为 / 规范结构) -> `brainstorming` 出设计共识 -> `writing-plans` 出实施计划 -> `subagent-driven-development` 逐任务执行;修 bug / 排障先 `systematic-debugging` 找根因再动手;多张独立卡并行扇出走 `task-breakdown`。交付前跑收口门槛 + `verification-before-completion`(证据先于声称);审查退回按 `receiving-code-review` 处理。
- **快速模式(收口白名单,默认优先尝试):** 纯文档 / 纯注释 / 机械索引 / 机械套用既有模式 / 生成物随源更新 / 有效 diff < 20 行(去空行注释) -> lint + 对应验证直接收口,交付附一行豁免声明。**红线面永不快速**:触碰 `rules/`、鉴权、安全面、可执行配置或 Unity 序列化资产的改动至少快审。
- **收口门槛:** `.spec` 结构改动默认跑 `node .spec/tools/spec-lint.mjs` + `node --test .spec/tools/spec-lint.test.mjs`;Unity 插件或框架迁移还要做静态文件数对账,进入 Unity 后再做 Package Resolve、脚本编译和 Console 错误检查。
- **并行边界与合入:** 任务文件集互不重叠才可并行,重叠必串行;多宿主并存时共享任务真值是 `.spec/tasks/`,宿主内置任务工具只作个人草稿。
- **派活模板:** worker 派遣与 reviewer 触发的 prompt 骨架见 [`knowledge/standards/workflow.md`](knowledge/standards/workflow.md) 和 [`agents/reviewer.agent.md`](agents/reviewer.agent.md)。
- **交回物格式(全仓单一权威):** ① 改动清单;② 验证证据(命令与关键输出,不得只声称已通过);③ known gaps;④ 知识沉淀落点(或声明无需沉淀)。
- **谁来调度:** 只有主 loop 派活;子 Agent 只执行,各自上下文只拿任务卡 + 相关文件。
- **失败处理:** P0 / P1 -> 附审查报告退回重做;同一问题三次不过 -> 质疑方案,拆解问题重修卡,方向问题升级用户。

## 编码约定

**约束一切写代码的上下文--主 loop 直编或通用 worker,一视同仁。**

- **领任务先标记:** 动手前标为进行中;多宿主任务更新 `.spec/tasks/<slug>.md` 的 `status`;不自标 completed(归「审查闭环」)。
- **先加载再动手:** 用 `before-you-code` 锁定问题对象、真相来源、目标入口/环境和验收口径,再按 `knowledge/README.md` 读取命中的知识正文。
- **测试先行:** 用 `test-driven-development` 为核心逻辑、新玩法切片、已复现 bug 和高风险公开契约选择 TDD、回归测试或 Unity 验证方式;纯文档、规范、索引、注释和机械命名迁移不机械进入 TDD。
- **排障先找根因:** 遇到 bug / 测试失败 / 异常行为,先走 `systematic-debugging`;未完成根因调查不得动手修。
- **设计先查规范:** 新增业务功能、拆职责、选设计模式、审查 SOLID / 反模式 / 防护性架构时,先读 [`knowledge/standards/code-design.md`](knowledge/standards/code-design.md)。
- **不夹带(全仓单一权威):** 只做当前目标要求的改动,不顺手重构、不加未要求的功能、不引入任务外新依赖。
- **收工即验证:** 交付前必过「收口门槛」;任何「完成 / 修好 / 通过」的声称前先过 `verification-before-completion`。
- **交付带证据:** 按「交回物格式」交付;主 loop 直编则据此向用户交代。
- **改完沉淀:** 新模式 / 新规范用 `spec-steward` 落 `knowledge/`,决策记 `decisions/`;纯修复 / 微调可豁免,豁免须在交回物声明。

## 宿主差异

| 能力 | Claude Code | Codex |
|------|-------------|-------|
| 任务持久化 | `TaskCreate` / `TaskUpdate` / `TaskList` | `.spec/tasks/<slug>.md`(frontmatter `status`) |
| 子 Agent 发现 | `.claude/agents/` 自动发现 | 主 loop 手动读 `.spec/agents/` |
| 技能加载 | `.claude/skills/` 自动发现 | `.agents/skills` 索引,手动调用 |

项目 skill 和 agent 的唯一权威源在 `.spec/skills` 与 `.spec/agents`。`.agents/skills`、`.claude/skills`、`.claude/agents` 是宿主自动发现适配入口,Git symlink 到 `.spec/`;`.codex/skills` 不再作为项目 skill 来源存在。

Codex 主 loop 本地执行:设计与计划用 `brainstorming` / `writing-plans`,执行按 `subagent-driven-development` 的 Inline Fallback,拆卡扇出用 `task-breakdown`,实现按「编码约定」,实质改动交付后读 `reviewer.agent.md` 本地对抗审查;同上下文自审丧失「写 != 审」独立性,属已知降级。宿主能力演进快,以官方文档为准,偏差时更新本表。

## 框架自身的决策与校验

- 决策一律记 [`decisions/`](decisions/README.md)(ADR,不改写、只新增取代)--功能内与框架级共用,唯一落点;feature 文档只描述设计现状,不留决策记录。
- 结构一致性由 `node .spec/tools/spec-lint.mjs` 校验,改完 `.spec/` 必跑;校验项清单以脚本头部注释为单一权威。

> 硬性禁令在 [`rules/system.md`](rules/system.md)。
