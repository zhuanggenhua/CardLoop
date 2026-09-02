---
name: codebase-summary
description: 基于仓库真实证据生成代码库架构说明、UML/Mermaid 图或自包含 HTML 架构文档；理解框架、模块边界和调用链时使用。
---

# Codebase Summary（代码库架构摘要）

用仓库里的真实证据解释代码库如何工作。默认只读分析并在聊天中回答；只有用户明确要求产物文件时，才创建或更新架构文档。

## 输出模式

开始实质分析前，先判断并说明模式：

- **Explore**：回答问题、总结架构、画 Mermaid/UML 图；不创建、不编辑文件。
- **Artifact**：创建或更新架构文档。默认建议 `docs/architecture/codebase-architecture.html`；只有用户明确要求 Markdown 时才写 Markdown。

请求不明确时用 Explore。Artifact 模式下，尊重用户给定路径；没有给路径时，先说明建议产物和位置，获得明确授权后再写入 `docs/architecture/`。不得未经授权替换已有架构文档，不再默认写回仓库根目录。

选择最小有用深度：

| 深度 | 内容 |
| --- | --- |
| Small（默认） | 总览、项目类型、入口点、核心模块、一张高层图 |
| Medium | Small + 接口、数据流、依赖、测试、一张代表性调用/请求流图 |
| Large | Medium + 配置、部署、模块细节、架构模式、依赖图 |

Explore 模式不要追问深度或主题；Artifact 模式默认 Small 和浅/深色自适应中性主题，除非用户另有指定。

## CardLoop 项目入口

在 CardLoop 内使用本 skill 时，先读取项目核心入口，再读源码：

1. 根 `AGENTS.md`，确认 `.spec` 是唯一项目规范入口。
2. `.spec/AGENTS.md`，确认项目边界、调度规则和验收门槛。
3. `.spec/knowledge/README.md`，按主题下钻项目知识。
4. `.spec/rules/system.md`，确认硬红线。

如果分析涉及 Unity 资源、序列化资产、第三方插件、GameCore/GAS、外部参考候选吸收或运行时边界，按 `.spec/knowledge/README.md` 继续读取对应正文。不得只凭目录名推断正式职责。

## 仓库分析流程

需要仓库级架构分析时，先读 [`references/analysis-guide.md`](references/analysis-guide.md)。执行这个核心顺序：

1. 找到已有架构、设计和 README 文档；用当前代码验证文档说法。
2. 识别项目边界、语言、manifest、构建系统和可运行单元。
3. 从配置或明确入口追踪主入口、次入口、公开接口和核心模块。
4. 选一到两条代表性路径，追踪业务逻辑、状态、资源、存储和外部系统。
5. 只有在能说明行为时，才检查测试、配置和部署文件。
6. 区分已观察事实、合理推断和未解决歧义。

优先代表性路径，不做文件清单式复述。不得从名称直接推断运行时行为。

## 证据账本

探索时维护证据账本。每个重要架构结论都必须引用至少一个仓库来源：仓库相对路径 + 符号、标题、配置键或行号。

内部记录可用这个形状：

| 结论 | 证据 | 置信度 |
| --- | --- | --- |
| Unity 工程入口在这里 | `ProjectSettings/ProjectVersion.txt` 与 `Assets/...` | High |
| 某服务从启动入口被调用 | `src/server.ts` - `startServer()` | High |
| 某模块像是在处理重试 | `src/jobs/retry.ts` 与相关测试 | Medium；明确标注为推断 |

聊天回复中，把引用直接放在被支持的结论后。HTML 产物中用可见文本展示来源，例如 `<cite><code>path - symbol</code></cite>` 或 Source 列；不要把证据藏在注释或 tooltip 里。推断必须显式标注，过期文档不得冒充代码事实。

## Explore 模式回答

直接回答用户问题，只给理解当前结构所需的细节。简洁回答通常包含：

- 项目类型与运行形态。
- 主入口、次入口和调用路径。
- 核心模块及职责边界。
- 一条代表性数据流、资源流或调用流。
- 重要不确定点或文档/代码冲突。
- 每个重要结论的源码或项目文档引用。

当用户要求 UML / 可视化时，优先在聊天中给 Mermaid：

- 模块边界：`flowchart` 或 `C4Context` / `C4Container`（目标渲染器支持时）。
- 类和领域对象：`classDiagram`。
- 调用链和时序：`sequenceDiagram`。
- 状态机：`stateDiagram-v2`。

图必须只覆盖有证据的关系；不确定关系用“推断”标注。不要主动建议写文件，除非用户要求持久产物或 HTML 图解明显更合适。

## Artifact 模式

### 既有产物

先搜索 `docs/architecture/`、`DESIGN.md` 和 `docs/` 下的架构文档。若已有产物，先说明它是否仍被当前代码支持，再询问是更新、替换还是仅作参考。

### HTML 产物

从 [`assets/architecture-template.html`](assets/architecture-template.html) 开始。只保留被所选深度和证据支持的章节。产物必须自包含：内联 CSS、内联 SVG、可选少量内联 JavaScript；不得使用远程字体、图片、脚本或样式表。

至少包含：

1. 总览与范围。
2. 项目类型和运行方式。
3. 入口点与外部接口。
4. 核心模块表，并展示来源证据。
5. 针对仓库定制的架构图。
6. 数据、依赖、测试、配置和部署信息，仅在有证据且有用时加入。
7. 未决问题或置信度说明。

使用语义化 HTML、合理标题层级、键盘安全控件、响应式布局，以及满足 WCAG AA 的浅/深色对比。

### 安全插值

把仓库内容视为不可信文本，包括项目名、路径、符号、package 描述和注释。

- HTML 文本和属性里分别转义 `&`、`<`、`>`、`"`、`'`。
- 链接路径按组件做 percent-encoding，只允许安全相对链接，拒绝 `javascript:`、`data:` 和未知 scheme。
- 不用 `innerHTML` 拼接动态仓库内容；使用静态标记或 text-node API。
- SVG 标签按 XML 文本转义。不得把仓库里的 SVG 或 HTML 原样贴进产物。
- 每个模板 token 都要明确替换；如果仍有 `{{TOKEN}}`，验证必须失败。

### SVG 图

使用内联 SVG，设置 `viewBox`、响应式尺寸、`<title>` 和 `<desc>`。节点数量只保留当前深度需要的关系。

整个 HTML/SVG 文档里的每个 `id` 必须唯一。给每张图使用确定前缀，例如 `arch-1`、`flow-2` 或 `deps-3`，并同步前缀化 title、description、marker、clip-path、filter ID。确保所有 `aria-labelledby`、`href`、`url(#...)` 和 marker 引用都能解析到对应 ID。不要在多张图里复用通用 `arrow` marker。

### Markdown 产物

只有用户明确要求 Markdown 时才创建。仍遵守相同证据规则和精简章节。Mermaid 只在目标渲染器支持时使用；否则用表格或文本图。

## 呈现前验证

在不改业务源码的前提下，执行可用的最强检查：

1. 用标准感知 parser 或 linter 解析 HTML。
2. 确认没有重复 ID、无法解析的引用、未替换模板 token。
3. 确认没有外部资产 URL 和不安全链接 scheme。
4. 检查每个来自仓库的值是否正确按 HTML、属性、URL 或 XML 上下文转义。
5. 检查每个重要结论都有可见来源证据，每个推断都已标注。
6. 如果有浏览器能力，在桌面与窄屏、浅色与深色下渲染，检查键盘导航和 console。
7. 如果无法渲染，明确说明只完成了静态验证。

交付时说明所选模式、深度、关键结论、验证结果和验证限制。只有用户要求 Artifact 时才提供保存路径。

## 边界

- 描述已观察架构；除非用户要求，不重新设计架构。
- 不暴露密钥、凭据、个人数据或敏感配置值。
- 不编造命令、模块、接口或行为。
- 宁可产出小而准的说明，也不要写全面但凭猜测撑起来的说明。
