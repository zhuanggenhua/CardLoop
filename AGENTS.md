# CardLoop AI 规范入口

> 本文件只负责把 AI 引导到 `.spec` 规范中心；不要把长期规则、详细 SOP 或项目知识直接追加到这里。

## 必读顺序

1. `.spec/AGENTS.md`：项目 AI 规范中心。
2. `.spec/rules/system.md`：项目硬红线。
3. `.spec/knowledge/README.md`：知识导航。
4. 按任务类型继续读取 `.spec/skills/`、`.agents/skills/`、`.codex/skills/` 或 `docs/FantasyWord-framework-migration.md`。

## 当前项目事实

- Unity 工程根目录：`C:\Gamedev\Unity\Project\CardLoop`。
- 来源工程：`C:\Gamedev\Unity\Project\FantasyWord`。
- 本轮已从 FantasyWord 静态迁入插件、本地 UPM 包、GameCore 框架候选和可复用 AI 工作流。
- 2026-08-01 已用 CardLoop 的 Unity `6000.5.4f1` 跑过新鲜 batchmode 验证，Package Resolve 与脚本编译通过；证据见 `docs/FantasyWord-framework-migration.md`。

## 正式入口

- 项目规范入口是 `.spec/AGENTS.md` 与 `.spec/knowledge/README.md`。
- FantasyWord 的任务记录、业务知识库、截图证据和历史决策没有作为 CardLoop 正式事实接管。
- 迁入裁决和排除项见 `docs/FantasyWord-framework-migration.md`。
