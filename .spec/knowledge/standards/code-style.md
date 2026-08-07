---
name: code-style
description: 代码与文档风格：说明中文优先、命名、注释、生成物和项目 skill/frontmatter 约定。
metadata:
  type: doc
  status: 已交付
---

# 代码与文档风格

## 语言

- 项目规范、文档、总结默认使用中文。
- Git 提交信息默认使用中文；Conventional Commits 的 type/scope 可保留英文，冒号后的摘要和正文用中文。
- 内部字段、日志标签、代码符号可以保留原名，但给用户解释前必须先说明现实含义。

## 集合查询 / ZLinq

- 项目侧运行时代码新增集合查询必须使用 ZLinq；写查询链前先 `using ZLinq;`，并从集合、数组、Span、Unity 对象遍历入口显式转成 ZLinq 查询入口，例如 `.AsValueEnumerable()`。
- 卡牌堆叠、回合结算、AI 评分、路径/目标筛选、资源扫描缓存等热路径必须使用 ZLinq 或手写无分配循环；不得在这些位置新增 `System.Linq` 查询链。
- 普通非热路径如果需要集合查询，也必须优先使用 ZLinq；只有 ZLinq 无法表达、会明显降低可读性，或第三方 / Unity API 明确要求 `System.Linq` 结果类型时，才允许局部例外，并在代码旁说明原因。
- Editor 工具、一次性导入脚本和测试代码可以按可读性使用 `System.Linq`，但不能把这类写法复制进运行时热路径。
- 不启用 ZLinq DropInGenerator 作为默认策略；避免全项目隐式改写 LINQ 行为。确需启用时必须先单独评估编译影响、第三方兼容和回退方案。

## 注释

- 注释只写代码表达不了的约束、原因、边界和外部依赖。
- 不写“改了什么”的流水账注释；改动说明放在交付汇报或提交信息。
- 项目侧新增或改写注释必须使用中文；第三方源码、生成代码、外部协议/API 名称、唯一 ID 和引用原文可保留英文，但项目侧语义说明不能只写英文。
- 项目侧 C# 的公开/受保护/内部类型、ScriptableObject 配置、编辑器工具、验证入口、生命周期/协程/事件/物理/存档等非显然逻辑，必须补中文注释说明职责、契约和边界。
- Unity Inspector 暴露配置必须补中文 `InspectorName` / `Tooltip` / `Header`，说明这个值影响什么、由谁配置、错误配置会怎样；不要依赖未登记的 Inspector 辅助插件。
- 这里的“Inspector 暴露配置”包括 `[SerializeField]` 字段、会显示在 Inspector 的 `public` 字段、ScriptableObject 配置字段、编辑器窗口参数、验证工具参数和其它给内容作者直接调整的字段。
- 新增或改写暴露字段时，字段符号本身继续使用英文代码命名；至少用中文 `InspectorName` 表达字段现实含义。存在配置风险、单位、取值范围、引用职责归属或旧数据兼容影响时，必须同步写中文 `Tooltip`。有分组时使用中文 `Header`。
- 若后续正式接入 NaughtyAttributes、Odin 或同类 Inspector 辅助插件，必须先登记插件落点，再使用其中文标签能力；未登记前只使用 Unity 内置中文特性。
- 简单赋值、自说明字段和一眼能懂的私有方法不强行补注释，避免把代码翻译成中文。
- 需要新增或审查注释时，使用全局 `D:\codex-home\skills\code-comments\SKILL.md`；本项目当前没有 `.agents/skills/code-comments/SKILL.md`。

## 命名

- `.spec` 目录和 skill 目录使用 kebab-case。
- GameCore 作为通用框架时，运行时默认值、编辑器入口、作者菜单、存档目录、输入绑定 key、配置文件名和生成物默认名使用 `GameCore` 或职责名；不得使用当前 Unity 工程名、未来游戏名或来源工程名。当前工程名只允许出现在仓库路径、项目事实、迁移记录、验证记录等“说明当前工作区”的文档位置。
- 项目侧正式玩法资产、素材文件、Prefab、ScriptableObject、Sprite Library、场景实例和正式测试场景入口优先中文命名；尤其是给策划、关卡、技能或表现作者直接选择的 SO 资产，文件名和 Inspector 显示名都应使用中文表达现实含义。
- `CreateAssetMenu` 的 `menuName`、`fileName` 等作者入口优先使用中文；只有唯一 ID、外部协议键、跨工具 ASCII 键或第三方来源名需要保留英文。
- C# 类名、结构体名、接口名、枚举名、方法名、字段符号、属性符号、事件符号和命名空间必须按项目现有英文符号风格保持稳定；中文通过 Inspector 特性、菜单名、资产名、注释和文档承载，不把运行时代码符号强行改成中文。
- GamePlay 正式玩法层命名优先使用游戏、桌游和 Unity 语义。启动/单局编排只有真正承担新局、读档、场景组合、保存恢复或生命周期调度时才用 `Director`；单局状态边界只有在成熟框架校准和项目职责归属审查后确认需要独立状态容器时才用 `RuntimeContext`；牌桌用 `Tabletop` / `Board` / `CardView`，运行时协作者用 `System` / `RuntimeSystem`。
- `Bootstrap`、`Provider`、`Store`、`Router`、`Service`、`Controller` 等词只有在对接第三方 / Unity 既有概念，或确实承担对应职责时才允许使用，并必须能说明职责归属、生命周期和验收点。不得为了显得架构完整使用网页端、后端或通用框架空壳名，也不得用命名包装掩盖旧职责没有重构。
- 第三方原始目录、代码符号和兼容外部 ID 保留原名，不为美观强行改。

## 生成物

- 生成物不得手改；必须改生成源并重新生成。
- `.meta`、GUID、Unity 资源引用必须作为闭包处理，不得只移动或改主文件。

## Skill frontmatter

- 项目 `.spec/skills/<name>/SKILL.md` 只要求 `name` 和 `description`。
- description 必须写清触发场景，不把完整 SOP 堆在描述里。
- 详细做法放正文，相关细节放 references 或项目 docs。

