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
- Unity Inspector 暴露配置必须补中文 `Sirenix.OdinInspector.LabelText` / `Tooltip` / `Header`，说明这个值影响什么、由谁配置、错误配置会怎样。当前项目资产实际由 Odin `OdinEditor` 绘制，Odin Inspector 已在插件入口索引登记；不得另造平行字段标签系统。
- 这里的“Inspector 暴露配置”包括 `[SerializeField]` 字段、会显示在 Inspector 的 `public` 字段、ScriptableObject 配置字段、编辑器窗口参数、验证工具参数和其它给内容作者直接调整的字段。
- 新增或改写暴露字段时，字段符号本身继续使用英文代码命名；至少用中文 `[LabelText("现实含义")]` 表达字段含义。存在配置风险、单位、取值范围、引用职责归属或旧数据兼容影响时，必须同步写中文 `Tooltip`。有分组时使用中文 `Header`。
- Unity 的 `InspectorNameAttribute` 只用于枚举值显示名，不得再用于普通序列化字段。Odin 或其它 Inspector 插件仍须先登记插件落点，才能成为新的作者入口依赖。
- 简单赋值、自说明字段和一眼能懂的私有方法不强行补注释，避免把代码翻译成中文。
- 需要新增或审查注释时，使用系统 skill `D:\codex-home\skills\code-comments\SKILL.md`；本项目当前没有对应的项目内 `code-comments` skill。

## 命名

- `.spec` 目录和 skill 目录使用 kebab-case。
- GameCore 作为通用框架时，运行时默认值、编辑器入口、作者菜单、存档目录、输入绑定 key、配置文件名和生成物默认名使用 `GameCore` 或职责名；不得使用当前 Unity 工程名、未来游戏名或来源工程名。当前工程名只允许出现在仓库路径、项目事实、迁移记录、验证记录等“说明当前工作区”的文档位置。
- 项目侧正式玩法资产、素材文件、Prefab、ScriptableObject、Sprite Library、场景实例和正式测试场景入口优先中文命名；尤其是给策划、关卡、技能或表现作者直接选择的 SO 资产，文件名和 Inspector 显示名都应使用中文表达现实含义。
- 游戏素材目录按 Unity / 参考工程约定使用英文类别名，具体自有资源文件名使用中文现实名称。当前正式入口为 `Assets/Art/Sprites`、`Assets/Art/Textures`、`Assets/Art/Materials`、`Assets/Art/Prefabs`、`Assets/Audio/SFX`；后续新增材质、Prefab、Sprite Library 等类别时，先使用行业约定英文目录，再在目录内使用中文资源名。不得把约定俗成的资源类别目录写成 `素材/图片/音效/特效` 这类中文分类。
- 从参考模板改造后归 CardLoop 使用的自有素材必须按上述目录规则迁入项目自有资源目录，并把资源文件名、Prefab 根对象名和作者可见地址改成中文现实名称。只有第三方原件留在参考目录、外部协议键、代码符号或工具强制 ASCII 时才保留英文。
- 中文资源名是项目正式作者入口，不是临时测试例外；不得因为文件名包含中文、Unicode 或空格外的非 ASCII 字符，就把自有图片、音频、材质、粒子、Prefab 或场景实例改回英文。真正需要 ASCII 的位置必须能指出外部工具、协议或构建链的硬约束。
- 粒子特效、特效材质、特效贴图、音效和其它美术 / 表现资产从参考模板吸收为自有资源时，不保留 `Puff`、`Hit`、`Projectile` 这类来源工程英文素材名作为正式资产名；资产文件名、Prefab 根对象名和给作者选择的资源地址应改成中文现实名称。对应 C# 类型、枚举值、字段和方法仍保持英文代码符号，但英文符号必须表达 CardLoop 自己的领域语义，例如正式卡牌烟雾使用 `CardSmoke`，不沿用来源模板行话 `Puff`。
- 从参考模板字段迁移到自有字段后，若项目自有场景、Prefab、SO 和测试资产中已无旧序列化字段存量，应删除 `FormerlySerializedAs` 等旧来源兼容标记；只有仍需保护真实存量资产时才保留兼容标记，并在迁移记录说明原因。
- 由测试场景生成器或编辑器工具重建、但会被正式玩法配置通过资源系统引用的自有素材，也按项目素材规则放入 `Assets/Art/Sprites`、`Assets/Art/Textures`、`Assets/Art/Materials`、`Assets/Art/Prefabs`、`Assets/Audio/SFX` 等标准资源目录并使用中文现实名称；只有纯测试夹具才允许带“测试”前缀。
- `CreateAssetMenu` 的 `menuName`、`fileName` 等作者入口优先使用中文；只有唯一 ID、外部协议键、跨工具 ASCII 键或第三方来源名需要保留英文。
- C# 类名、结构体名、接口名、枚举名、方法名、字段符号、属性符号、事件符号和命名空间必须按项目现有英文符号风格保持稳定；中文通过 Inspector 特性、菜单名、资产名、注释和文档承载，不把运行时代码符号强行改成中文。
- 自有玩法顶级模块统一拼作 `Gameplay`，不得新增历史拼写 `GamePlay`。正式 C# 命名空间按真实职责分为 `Gameplay.Content`、`Gameplay.Actions`、`Gameplay.Tabletop`、`Gameplay.Scenarios`，牌桌拥有的行动运行对象进入 `Gameplay.Tabletop.Actions`；编辑器工具在 `Gameplay.Editor.<职责>`，测试在 `Gameplay.Tests`。目录、程序集根命名空间和作者菜单必须同步使用这一拼写与层级。
- 命名空间、程序集和目录已经提供模块上下文时，类型名不得重复模块或项目名。`Gameplay.Content` 下使用 `ContentIndex`，`Gameplay.Actions` 下使用 `ActionDefinition`，不得使用 `GameplayContentIndex`、`GameplayActionDefinition`；只有跨命名空间确实存在无法通过限定名解决的冲突，或外部协议明确要求模块前缀时，才保留前缀并在专项文档说明原因。
- 命名参考按职责而不是来源工程名裁决：StackCraft 使用 `Quest`、`QuestManager`、`EncounterDefinition`，GameCore 使用 `ResourceSystem`、`SaveSystem`，EX-GAS 使用 `AbilitySystemCell`、`GameplayTagController`。CardLoop 吸收职责时沿用这种“命名空间提供上下文、类型名表达职责”的方式，不复制 `StackCraft`、`GameCore` 或 `Gameplay` 前缀。
- Gameplay 正式玩法层命名优先使用游戏、桌游和 Unity 语义。作者静态配置用 `Definition`，运行时可写事实用 `State`，内容技术 SO 基类可用 `Asset`，查询/登记用 `Index` / `Registry`，纯计算器用 `Resolver` / `Evaluator` / `Validator` / `Selector`，测试场景装配器用 `Harness`。启动/单局编排只有真正承担新局、读档、场景组合、保存恢复或生命周期调度时才用 `Director`；`Director` 即使继承 `AGameSystem` 也不重复追加 `System`，应使用 `ScenarioDirector`，不得使用 `DirectorSystem` 或 `ScenarioDirectorSystem`。单局状态边界只有在成熟框架校准和项目职责归属审查后确认需要独立状态容器时才用 `RuntimeContext`；牌桌用 `Tabletop` / `Board` / `CardView`，运行时协作者用 `System` / `RuntimeSystem`。
- `Bootstrap`、`Provider`、`Store`、`Router`、`Service`、`Controller` 等词只有在对接第三方 / Unity 既有概念，或确实承担对应职责时才允许使用，并必须能说明职责归属、生命周期和验收点。不得为了显得架构完整使用网页端、后端或通用框架空壳名，也不得用命名包装掩盖旧职责没有重构。
- 第三方原始目录、代码符号和兼容外部 ID 保留原名，不为美观强行改。

## 作者源身份与局部 key

- 内容唯一 ID 是存档、联机、Mod 和编辑器引用的正式身份；默认由作者源自动生成，不把 Unity GUID、YooAsset 地址、资源路径、文件名或运行时实例号并列成第二套身份。
- Unity ScriptableObject 内容资产首次没有内容 ID 时，使用“资产文件名的可读片段 + Unity GUID 短 hash”生成默认 ID 并写回资产字段；Unity GUID 只作为一次性生成种子，不作为运行时内容 ID 对外暴露。
- 已生成内容 ID 不随文件改名、移动、YooAsset 地址变化自动更新。需要改 ID 时，必须按迁移处理所有引用、存档、测试内容和 Mod 依赖，不允许静默漂移。
- 行动槽位、随机结果分支、局部生成物索引等只在单个作者资产内部解释的 key，不属于策划业务字段。它们必须由所属资产、创建工具、校验器或编辑器下拉自动维护；Inspector 不得要求作者手打字符串。
- 如果一个局部引用可以由上下文唯一推导，例如单槽位行动的结果来源，作者源应允许省略并由运行时在提交前明确解析；如果上下文不唯一，必须通过正式作者工具选择，不能猜测、兜底或默认第一项。
- 每次发现手填内部 key、重复配置、第二套身份或浅包装时，必须横向搜索同职责同类入口。证据明确的同类问题当轮一起删除或重构；暂不能动的必须写进对应模块的吸收/重构清单。

## 生成物

- 生成物不得手改；必须改生成源并重新生成。
- `.meta`、GUID、Unity 资源引用必须作为闭包处理，不得只移动或改主文件。

## Skill frontmatter

- 项目 `.spec/skills/<name>/SKILL.md` 只要求 `name` 和 `description`。
- description 必须写清触发场景，不把完整 SOP 堆在描述里。
- 详细做法放正文，相关细节放 references 或项目 docs。
