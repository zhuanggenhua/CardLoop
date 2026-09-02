---
name: gamecore-gas
description: GAS 官方文档入口、GameCore 与 EX-GAS 的正式集成边界、项目侧薄集成点和偏离登记；防止旧能力系统或临时中转层回流。
metadata:
  type: doc
  status: 已交付
---

# GameCore / EX-GAS 集成边界

本文件只写 CardLoop 项目侧的 EX-GAS 集成边界，不替代 GAS 插件文档，也不记录旧实现流水。旧版实现状态、模块编号、历史测试和动作游戏案例不保留为 active 项目事实。

## 文档角色

- `canonical-source`：本文件裁决 GameCore / Gameplay 如何接入 EX-GAS，以及哪些项目侧集成点允许存在。
- `adapter`：`ai-quick/ex-gas-runtime.md` 校准 EX-GAS 2.0.4 当前可调用接口，属于项目速查，不是第二套 GAS 设计规范。
- `upstream`：`Assets/Plugins/GAS/SKILL.md`、`Assets/Plugins/GAS/Wiki/`、`Assets/Plugins/GAS/package.json` 和 `CHANGELOG.md` 是插件用法真相源。
- `archive`：历史实现状态只作追溯证据，不能替代代码阶段对当前源码、表源、Prefab、场景和测试的重新验证。

## 官方入口

| 入口 | 用途 |
| --- | --- |
| `Assets/Plugins/GAS/package.json` | 插件版本、仓库和文档 URL。 |
| `Assets/Plugins/GAS/SKILL.md` | AbilityLogic、GameplayCue、MMC、AbilityTask、TargetCatcher、XParam 和生成管线规则。 |
| `Assets/Plugins/GAS/Wiki/EX-GAS.md` | ASC、Ability、GameplayEffect、GameplayCue、Attribute、Tag 总设计。 |
| `Assets/Plugins/GAS/Wiki/Ability.md` | Ability 激活、条件、消耗和作者分工。 |
| `Assets/Plugins/GAS/Wiki/GameplayEffect.md` | 数值变化、持续、周期、授予、标签和 Cue 触发。 |
| `Assets/Plugins/GAS/Wiki/GameplayCue.md` | Cue 表现职责。 |
| `Assets/Plugins/GAS/Wiki/MMC.md` | 属性数值计算入口。 |
| `Assets/Plugins/GAS/CHANGELOG.md` | 版本迁移事实。 |

## 职责边界

- EX-GAS 拥有 Ability、Timeline、GameplayEffect、GameplayCue、GameplayTag、Attribute 和 ASC 运行时状态。
- YokiFrame 只提供无游戏语义的底座原语，不承载 GAS 业务含义。
- GameCore 只提供跨游戏可复用的输入、资源、表现、存档、生命周期和配置扩展点。
- Gameplay 只提供 CardLoop 的卡牌、牌桌、剧本、战斗、任务、开包、内容数据和策略。
- 外部项目或旧动作游戏样例只能作为参考证据；不得把它们的技能系统、角色姿态、武器 Prefab、旧数据表或场景 Hitbox 当成 CardLoop 当前真相。

## 真相源

| 事项 | 正式真相源 |
| --- | --- |
| 技能身份、消耗、冷却、激活条件 | EX-GAS Ability / AbilitySpec |
| 时间轴、前后摇、命中帧 | EX-GAS Timeline / AbilityTask |
| 伤害、治疗、状态、持续和周期效果 | EX-GAS GameplayEffect |
| 表现触发 | EX-GAS GameplayCue；Cue 只转发表现 |
| 标签语义 | EX-GAS GameplayTag / `TagHelper` |
| 属性身份和值计算 | EX-GAS Attribute / AttributeSet / ASC |
| 项目输入、资源、Prefab/Icon、UI 投影 | GameCore 扩展点 + Gameplay 数据 |
| 牌桌角色长期事实、存档和联机命令 | Gameplay 领域对象；需要 GAS 状态时用公开运行时接口重建 |

## 允许的项目侧薄集成点

这些是代码阶段允许审查或保留的集成类别；是否已经存在、是否仍正确，必须用当前源码重验：

| 集成类别 | 允许职责 | 禁止职责 |
| --- | --- | --- |
| Ability 输入门控 | 把 Gameplay / GameCore 输入请求转换为 EX-GAS Ability 激活请求。 | 不保存技能身份、冷却、伤害、命中、弹匣或表现规则。 |
| GameCore Ability 配置 | 给 EX-GAS Ability 附加跨游戏可复用的输入、Prefab、Icon 或资源引用。 | 不承载 Ability、GE、Tag 或 Cue 规则真相。 |
| TargetCatcher 扩展 | 用 EX-GAS 官方扩展点解析当前项目真实空间中的目标 ASC。 | 不新建第二套 Hitbox、伤害或目标选择规则。 |
| CuePlay 转发 | 将 EX-GAS Cue 转到 GameCore 动画、音频、反馈或 UI 投影。 | 不结算数值、不改标签、不选目标。 |
| 伤害 / 属性集成 | 把 GameplayEffect 配置转成执行数据，并把结果写回 ASC。 | 不在投射物、行动、UI 或临时脚本保存第二份伤害 / 生命真相。 |
| 标签作者辅助 | 复用 EX-GAS 生成作者数据供 Gameplay 内容选择和校验。 | 不建立 Gameplay 本地标签表、枚举镜像或运行时标签服务。 |
| 长期状态快照 | 只保存产品确实需要跨局延续、且能用公开接口恢复的长期事实。 | 不导出完整 ASC，不保存临时 GE、Cue、Timeline、ECS Entity 或派生 CurrentValue。 |
| 生命周期启动 / 关闭 | 在 GameCore 进程生命周期中初始化、运行、停止和释放 EX-GAS。 | 不反射改写插件私有状态，不绕过插件正式 API。 |

## 明确排除

- 不恢复项目侧 AbilitySheet、ActiveAbilitySheet、PassiveAbilitySheet、旧执行资产、旧时间轴或旧命中框作者入口。
- 不把旧世界交互 Task、旧动作样例或旧来源项目表数据作为新能力入口。
- 不用本地 Temporal / Buff / Debuff / Cooldown 系统复制 EX-GAS GameplayEffect 已承担的职责。
- 不为了牌桌战斗临时跑通而直接复用旧 2D 场景角色姿态、`Movable`、武器 Prefab 或场景 Hitbox。
- 不用测试护栏、迁移兼容、窗口缓存或生成目录文件把未登记中转层保护成正式职责。

## 新增或保留集成点门槛

新增、保留或修改项目侧 GAS 集成点前，必须同时满足：

- 已读取插件官方入口和 `ai-quick/ex-gas-runtime.md`，确认 EX-GAS 当前公开能力。
- 已按 `framework-layering.md` 判定职责属于 GameCore 扩展点还是 Gameplay 业务实现。
- 能一句话说明该集成点连接的两端和允许职责。
- 不拥有 EX-GAS 已承担的规则真相，也不制造第二套状态、标签、属性、命中或伤害。
- 本文件表格已登记集成类别、禁止职责和代码阶段验证方式。
- 如果只是旧数据兼容，必须标明删除条件和负向断言。

## 代码阶段重验清单

进入代码重构或继续外部参考对齐前，至少重验：

- 当前源码是否仍存在旧 AbilitySheet、旧执行资产、Temporal 效果、旧世界元素 Task、旧动作角色依赖或第二套标签 / 属性 / 伤害真相。
- 牌桌战斗、角色卡、行动结算、投射物和 UI 投影是否只通过 EX-GAS / GameCore / Gameplay 的单一 owner 修改状态。
- `ai-quick/ex-gas-runtime.md` 中记录的 EX-GAS 公开接口是否仍与当前插件源码一致。
- 任何旧测试、旧截图或历史 PlayMode 结果只作为线索，不能证明当前源码已经完成集成。
