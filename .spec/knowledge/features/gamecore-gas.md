---
name: gamecore-gas
description: GAS 官方文档入口、GameCore 与 EX-GAS 的正式集成边界、项目侧薄集成点和偏离登记；防止旧能力系统或临时中转层回流。
metadata:
  type: doc
  status: 已交付
---

# GameCore / EX-GAS 官方入口与集成边界

本文件不是 GAS 使用方法的唯一真相源。GAS / EX-GAS 的正式用法优先看插件自带文档和官方仓库；本文件只做 CardLoop 项目侧索引、正式集成点登记和偏离记录。新增或保留项目侧集成点前，必须先证明官方扩展点或现有职责归属不能直接承担；没有登记依据的中转层不视为正式框架能力。

## 官方文档入口

| 入口 | 角色 |
|------|------|
| `Assets/Plugins/GAS/package.json` | 插件版本、官方仓库和文档 URL。当前本地包为 `com.exhard.exgas` / `2.0.4`。 |
| `Assets/Plugins/GAS/SKILL.md` | EX-GAS 2.0 自定义 AbilityLogic、GameplayCue、MMC、AbilityTask、TargetCatcher、XParam 和生成管线规则。 |
| `Assets/Plugins/GAS/Wiki/EX-GAS.md` | EX-GAS 总体设计：ASC / Ability / GameplayEffect / GameplayCue / Attribute / Tag。 |
| `Assets/Plugins/GAS/Wiki/Ability.md` | Ability 设计原则、激活标签条件和策划 / 程序分工。 |
| `Assets/Plugins/GAS/Wiki/GameplayEffect.md` | GameplayEffect 的数值职责、Duration / Period / GrantedAbilities / Modifiers / Tags / Cue。 |
| `Assets/Plugins/GAS/Wiki/GameplayCue.md` | Cue 只做表现提示，不应修改数值或实际玩法结果。 |
| `Assets/Plugins/GAS/Wiki/MMC.md` | Modifier Magnitude Calculation 的属性数值计算入口。 |
| `Assets/Plugins/GAS/CHANGELOG.md` | GAS 版本迁移事实和历史行为变化。 |

## 当前阶段

- GameCore 当前是通用框架候选，不是具体游戏业务落地。
- EX-GAS 是能力身份、规则、消耗、冷却、阻断、时间轴、命中帧、GameplayEffect 和 Cue 的默认职责归属。
- GameCore 只承接输入、角色状态、资源/表现系统、2D 空间语义、存档和编辑器体验中 EX-GAS 本体无法直接表达的最薄适配。
- 测试只能保护正式职责边界；不得用大量迁移测试长期替代使用文档。
- 旧项目技能样例、旧业务表数据和迁移防回流测试不能把项目侧中转层“护”成正式职责。

## 真相源

| 事项 | 真相源 |
|------|--------|
| 能力身份 | EX-GAS Ability 表 / 生成的 Ability Code |
| 时间轴与命中帧 | EX-GAS Timeline |
| 伤害、状态和条件效果 | EX-GAS GameplayEffect |
| 表现触发 | EX-GAS Cue |
| 项目输入、Prefab/Icon、资源引用 | `exgas.abilityGameCore` 项目配置 |
| 角色当前属性、死亡、存档和 UI 投影 | GameCore 角色/资源/持久化职责 |
| 2D 目标捕获几何 | EX-GAS TaskApplyEffects + 已登记 TargetCatcher |

## 官方建议裁决

- Ability、Timeline、AbilityTask、TargetCatcher、XParam 的新增或修改必须遵守 `Assets/Plugins/GAS/SKILL.md`：`[BeanField]`、Setter、空值占位、`LayerMask` / enum 的 `LubanType = "int"`、以及完整生成管线。
- 数值变化、持续效果、周期效果、授予能力、Tag 条件和 Cue 触发默认由 GameplayEffect 承担；项目侧不得另建同职责持续效果系统作为正式主线。
- CuePlay 类只允许转发表现，例如动画、音频、反馈；不得改属性、改 Buff、做目标选择、结算伤害或改变技能身份。
- 2D TargetCatcher 可以作为官方 TargetCatcher 扩展点保留，但必须只返回目标 ASC，不拥有伤害、效果或技能规则。
- 项目侧文档记录“当前项目怎么接入”和“为什么偏离”，不复写 GAS 官方教程。

## 使用入口

1. 在 EX-GAS Ability 表中创建或选择能力，能力逻辑优先指向 Timeline。
2. 在 EX-GAS Timeline 中配置 TaskDoCost、TaskPlayCue、TaskApplyEffects 等正式任务。
3. 在 GameplayEffect 中配置伤害、条件伤害、控制 Tag、CueOnApply 等规则和表现触发。
4. 在 `exgas.abilityGameCore` 只配置 GameCore 必需的输入触发方式、Prefab/Icon 和资源引用。
5. 若需要编辑 2D 命中范围，使用 EX-GAS 时间轴窗口里的 TaskApplyEffects / TargetCatcher，不新建项目侧命中框作者入口。
6. 保存后通过 Luban 生成结果进入 `Assets/DataGenerated/Luban/Json/GAS`；不得手改生成 JSON。

## 已登记正式集成点

| 集成点 | 允许职责 | 禁止职责 |
|------|----------|----------|
| `TimelineActiveAbility` / `FormalAbilityInputGateRuntime` | 把本地输入、缓冲、按住释放转换成 EX-GAS Ability 激活请求；读取 EX-GAS Timeline 的前后摇节奏。 | 不保存弹匣、换弹、连发、命中、伤害、冷却或表现规则。 |
| `exgas.abilityGameCore` | 为 EX-GAS Ability 提供 GameCore 必需的 Prefab/Icon/输入配置。 | 不承载能力身份、消耗、冷却、命中、伤害或 Cue 真相。 |
| `Gas2DTargetCatchers` / `CatchArea*2D` | 给 EX-GAS TaskApplyEffects 提供 GameCore 2D 空间和命中目标解析。 | 不在场景或 Ability Prefab 上另做第二套 Hitbox 真相。 |
| `FormalGameplayEffectDamageBridge` / `FormalGameplayEffectDamageHelper` / `FormalGameplayEffectDamageSystem` | 把 GameplayEffect 中的正式伤害载荷应用到 GameCore 角色当前生命值和反馈链；现有类名含 Bridge 是迁移遗留命名，后续重构时应按正式集成点命名收口。 | 不在 TaskApplyEffects 或能力壳里写伤害数值。 |
| `FormalGameplayAttributeSet` / `FormalAttributeCatalog` | 维护 GameCore 属性与 EX-GAS Attribute code 的映射。 | 不创建第二套属性职责。 |
| `FormalGameplayTagCatalog` | 维护 GameCore 需要识别的 EX-GAS Tag 映射。 | 不用本地布尔状态长期镜像 GAS Tag。 |
| `CuePlayGameCoreAnimator` / `CuePlayGameCoreAudio` / `CuePlayGameCoreFeedback` | 把 EX-GAS Cue 转发到 GameCore 已有动画、音频和反馈系统。 | 不承担规则结算、目标选择、伤害或技能身份。 |
| `GasTimelineHitboxSceneHandle` | 在 EX-GAS 时间轴窗口中辅助编辑已登记 2D TargetCatcher 参数。 | 不新建第二套保存入口、自动读表或自动刷新缓存。 |

## 当前排除项

| 对象 | 裁决 | 依据 |
|------|------|------|
| `TaskApplyWorldElement` / `XParamApplyWorldElement` | 不作为 GameCore / EX-GAS 正式集成点保留。 | 本地表源中只服务 `Flamethrower / 持续喷火` 旧能力样例；属于旧玩法数据把世界元素系统挂到 GAS Timeline 的迁移中转层，不是 GAS 官方建议下的通用扩展主线。 |
| `Temporal*Effect` / `ITemporalEffect` / 本地 `TemporalEffect` 存档与 UI | 已从 GameCore 通用框架中删除。 | EX-GAS 官方 GameplayEffect 已拥有 Duration、Period、GrantedAbilities、Modifiers、Tags、RemoveGameplayEffectsWithTags 和 Cue 触发；本地 Temporal 另做持续时间、tick、授予/压制能力、净化、存档和 UI 展示，属于第二套效果职责。后续如需 buff/debuff 展示或净化，应基于 EX-GAS GameplayEffect / Tag / Cue 正式入口重新设计薄投影。 |

## 全局运行时生命周期

- EX-GAS 公开生命周期现为 `GASManager.Initialize()` 创建进程内唯一 `EX_GAS_World`，`Run()` / `Stop()` 控制运行状态，`Shutdown()` 完成最终释放和静态状态复位。项目侧关闭入口只调用这些正式 API，不反射改写插件私有字段。
- `GASManager.Shutdown()` 会停止系统、完成未结束任务、释放已绑定 `AbilitySystemCell` 的属性原生数组、释放标签图数组与哈希表、移除 PlayerLoop、销毁 World，并清空事件与静态状态；`FormalAbilityRuntimeBootstrap` 负责在 `GameManager` 关闭时调用它。
- 修复前的带栈诊断把 68 笔 Persistent 分配全部定位到标签图：33 个标签的父数组与子数组共 66 笔，加原生哈希表内部 2 笔。当前稳定回归证据为 `Logs/TestResults-GamePlay-4.7-AllEditMode-Final.xml` 的 335 通过、1 条条件不适用跳过、0 失败，以及 `Logs/TestResults-GamePlay-4.7-AllPlayMode-Final.xml` 的 `6/6`；对应三份最终日志均未出现 `Leak Detected` 或未释放原生集合。
- 当前证据覆盖标签图、绑定角色属性集和现有全量测试路径；Ability / GameplayEffect 中其它 Persistent 配置组件尚未做逐类型专项生命周期审计，不能越权声称插件所有原生内存路径都已证明正确。

## 新集成点门槛

新增或保留任何项目侧集成点前必须满足全部条件：

- 已确认 EX-GAS、Unity 原生能力或现有 GameCore 职责归属无法直接表达该职责。
- 已读取 GAS 插件自带文档和官方仓库入口，确认该集成点使用的是官方扩展点，而不是迁移旧项目时自造的旁路。
- 集成点职责能用一句话说清，并且只连接边界，不拥有规则真相。
- 这里的登记表已更新，说明使用入口、禁止职责和验收方式。
- 有最小验证覆盖集成边界，而不是用业务剧本测试替代职责文档。
- 若集成点只是为了兼容旧 FantasyWord 数据，必须标成迁移兼容，并给出删除条件。

## 测试口径

- 保留少量框架合同测试，证明 EX-GAS 职责与 GameCore 职责的边界没有断。
- 迁移防回流测试只保留代表性用例；不得把旧项目所有技能、地图、商店、宝箱或剧情语义长期当成框架测试。
- 发现测试只是在保护未登记中转层时，优先审查该中转层是否该删；不先加更多测试。
