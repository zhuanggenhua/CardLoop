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
- 战斗数值正式归 GNS/EX-GAS 数值链；StackCraft 的攻击、防御、攻速、命中、闪避、暴击率和暴击倍率只能作为模板效果复现的临时参数，不能恢复成 `CombatStats` 或第二套角色属性真相。
- GameCore 只承接其 2D 场景角色的输入、资源/表现系统、空间语义、存档和编辑器体验中 EX-GAS 本体无法直接表达的最薄适配。Gameplay 的角色卡直接使用 EX-GAS `AbilitySystemCell`，不通过 GameCore 再包一层。
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
| 角色属性身份、当前值和上限 | EX-GAS Attribute / AttributeSet 表及其生成代码；运行时值由角色 ASC 持有 |
| 战斗模板临时数值 | GNS/EX-GAS 属性、MMC 或 GameplayEffect 参数；StackCraft 字段只作迁移输入，不作正式属性模型 |
| 2D 场景角色的死亡和 UI 投影 | GameCore `CharacterBase` 与 UI 职责；只能消费 GAS 属性，不得另存一套可写属性真相 |
| 角色 GAS 长期状态快照 | Gameplay `CharacterCard` 读取 EX-GAS `AbilitySystemCell` 公开方法后生成 `CharacterAbilitySystemSnapshot`；EX-GAS 不提供完整业务存档 API |
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

## Gameplay 静态标签作者入口

- Gameplay 内容的静态标签字段只保存 EX-GAS 的整数标签码。作者选择直接复用 EX-GAS `GeneralGasChoiceHelper.Tags()`；它读取 GAS 已生成的作者数据，Gameplay 不复制为本地标签表、枚举或字符串符号表。
- `Gameplay.Editor.ContentValidationMenu` 在编辑器校验时同样读取这一个官方选择结果。内容引用了不存在的标签码时报告 `CONTENT_TAG_UNKNOWN`；若 GAS 作者数据本身为空，则报告 `CONTENT_TAG_AUTHORING_SOURCE_EMPTY`。校验过程中的临时集合只服务本次检查，不保存为项目标签目录。
- 内容自身的标签层级比较使用 `TagHelper.HasTag(实际标签码, 查询标签码)`；角色当前持有的固有或临时标签使用其 `AbilitySystemCell` 的正式查询。整数相等只可判断同一码，不能替代父子层级语义。
- 此入口的程序集依赖是 `Gameplay.Editor -> com.exhard.exgas.general`。它仅用于作者选择和编辑器校验，不建立 Gameplay 运行时标签服务，也不修改 EX-GAS 插件源码。

## 已登记正式集成点

| 集成点 | 允许职责 | 禁止职责 |
|------|----------|----------|
| `TimelineActiveAbility` / `FormalAbilityInputGateRuntime` | 把本地输入、缓冲、按住释放转换成 EX-GAS Ability 激活请求；读取 EX-GAS Timeline 的前后摇节奏。 | 不保存弹匣、换弹、连发、命中、伤害、冷却或表现规则。 |
| `exgas.abilityGameCore` | 为 EX-GAS Ability 提供 GameCore 必需的 Prefab/Icon/输入配置。 | 不承载能力身份、消耗、冷却、命中、伤害或 Cue 真相。 |
| `Gas2DTargetCatchers` / `CatchArea*2D` | 给 EX-GAS TaskApplyEffects 提供 GameCore 2D 空间和命中目标解析。 | 不在场景或 Ability Prefab 上另做第二套 Hitbox 真相。 |
| `GameplayEffectDamageIntegration` / `GameplayEffectDamageSystem` | 把 EX-GAS GameplayEffect 表配置解析为程序集内部的伤害执行数据；在 ECS 效果查询结束后结算，并把最终生命变化写回目标 ASC。 | 不在投射物、TaskApplyEffects、能力壳或反馈代码里保存第二份伤害数值、类型、缩放或生命真相。 |
| `CharacterAttributes` | 读取 EX-GAS 生成的 `FightUnit` 属性集、属性编号和默认配置，并为 `CharacterSheet` 应用角色差异覆盖。 | 不手写属性编号、稳定 ID、显示名或第二份属性目录；不恢复已删除的 `Stats / EStat` 属性模型。 |
| `GameCore.GasIntegration` / `GasGeneratedConfigIntegration` | 作为生成配置程序集与 GameCore 运行时的显式程序集边界，注册项目已登记的 Ability、Timeline 和 GameplayEffect 配置解析入口。 | 不把项目手写代码放回生成目录，不复制 Luban 表数据，不成为新的规则作者源。 |
| `FormalGameplayTagCatalog` | 维护 GameCore 需要识别的 EX-GAS Tag 映射。 | 不用本地布尔状态长期镜像 GAS Tag。 |
| `CuePlayGameCoreAnimator` / `CuePlayGameCoreAudio` / `CuePlayGameCoreFeedback` | 把 EX-GAS Cue 转发到 GameCore 已有动画、音频和反馈系统。 | 不承担规则结算、目标选择、伤害或技能身份。 |
| `GasTimelineHitboxSceneHandle` | 在 EX-GAS 时间轴窗口中辅助编辑已登记 2D TargetCatcher 参数。 | 不新建第二套保存入口、自动读表或自动刷新缓存。 |
| `Gameplay.Editor.ContentValidationMenu` | 复用 `GeneralGasChoiceHelper.Tags()` 校验内容静态标签是否仍存在于 EX-GAS 作者数据中。 | 不建立本地标签表、运行时标签查询器或另一条 GAS 初始化链。 |
| `CharacterAbilitySystemSnapshot` / `CharacterCard.CreateRuntimeStateSnapshot` | Gameplay 角色卡保存非战斗长期 GAS 事实：ASC 等级、当前角色 ASC 预设声明的属性 `BaseValue`，以及装备卡快照；恢复时用当前作者源重新创建 Cell，再覆盖基础值并重算当前值。 | 不导出完整 ASC，不保存临时标签、活动 Ability、Cooldown、GameplayEffect、Cue、Timeline、ECS Entity、CurrentValue 派生缓存或项目内容 ID；Gameplay 不直接读取 ECS Buffer。 |

`Gameplay.Tabletop.CharacterCard` 不是 GameCore / EX-GAS 之间的新集成层：它继承 `TabletopCard` 并直接拥有 EX-GAS `AbilitySystemCell`。一个逻辑角色只能由它或 `CharacterBase` 之一拥有 ASC；不得以两者同时表示同一角色，也不得加 resolver、adapter 或同步副本来掩盖双状态。

## 当前排除项

| 对象 | 裁决 | 依据 |
|------|------|------|
| `TaskApplyWorldElement` / `XParamApplyWorldElement` | 不作为 GameCore / EX-GAS 正式集成点保留。 | 本地表源中只服务 `Flamethrower / 持续喷火` 旧能力样例；属于旧玩法数据把世界元素系统挂到 GAS Timeline 的迁移中转层，不是 GAS 官方建议下的通用扩展主线。 |
| `Temporal*Effect` / `ITemporalEffect` / 本地 `TemporalEffect` 存档与 UI | 已从 GameCore 通用框架中删除。 | EX-GAS 官方 GameplayEffect 已拥有 Duration、Period、GrantedAbilities、Modifiers、Tags、RemoveGameplayEffectsWithTags 和 Cue 触发；本地 Temporal 另做持续时间、tick、授予/压制能力、净化、存档和 UI 展示，属于第二套效果职责。后续如需 buff/debuff 展示或净化，应基于 EX-GAS GameplayEffect / Tag / Cue 正式入口重新设计薄投影。 |

## 当前实现状态与缺口

- `CharacterSheet` 不再保存旧 `Stats` 或等级缩放数组；角色作者入口是 `CharacterAttributeOverride[]`，属性码、默认值和钳制规则由 EX-GAS `FightUnit` 表提供。`CharacterAttributes.CreateConfig` 只克隆正式配置并应用角色差异，重复、未知、超出表格钳制范围或非法覆盖会立即抛错，不静默修正作者输入。
- 角色首次创建 ASC 时直接使用 `CharacterSheet.CreateAttributeSetConfig`。旧 `AttributeBootstrapBuffer`、`Stats`、`EStat` 和 `FormalAttributeCatalog` 已删除；ASC 初始化前读取属性属于生命周期错误，会立即抛出异常。
- `UIStat`、`UIStatBar` 和 `UICharacterInfo` 读取角色公开的 EX-GAS 属性码查询入口。当前仓库没有 prefab 或 scene 引用这些 UI 脚本，因此本切片没有真实可截图入口。
- 当前资源与上限已拆成不同 GAS 属性：`Health / MaxHealth`、`Mana / MaxMana`、`Stamina / MaxStamina`。资源变化修改对应当前属性的基础值后调用 `AttributeHelper.RecalculateCurrentValue`，不直接写 `CAttributeData.CurrentValue`。Gameplay 角色卡 ASC 快照已由 `CharacterAbilitySystemSnapshot` 接管，只保存 ASC 等级和当前预设声明属性的 `BaseValue`；恢复时重新创建 Cell 后覆盖基础值，让 EX-GAS 正常计算 `CurrentValue`，不把当前派生值作为第二真相写回。
- 投射物的发射参数、运行状态和存档只保存 `impactGameplayEffectId`。命中或爆炸时从 EX-GAS 配置取得正式 GameplayEffect；动态命中方向只通过效果实例上的 `MCGameplayEffectImpactOverride` 传入，不复制伤害数值、类型、缩放或击退配置。
- `GameplayEffectDamagePayload` 与 `DamageDescriptor` 都是程序集内部、不可变的表格转换执行数据，不是 Inspector 或 Mod 作者入口。旧 `GameplayEffectDamageApplier`、`AEffect` / `IEffect` 和可绕过 GameplayEffect 的 `HealOrDamagePlayer` 已删除。
- 旧 `AddOrRemoveMana` 可序列化命令同样已删除：技能消耗和恢复不能再通过项目命令直接修改角色法力，必须由 EX-GAS Ability 的 Cost GameplayEffect 或正式 GameplayEffect Modifier 表达。`CharacterBase` 只在 GameCore 内部保留资源写入语义，供复活、升级等角色生命周期使用；它们不对 Gameplay 或内容作者暴露为第二条效果入口。
- `GameplayEffectDamageSystem.CreateResolutionRolls` 优先读取效果实体上的权威随机种子组件；牌桌战斗由 `ScenarioRun -> Tabletop -> Battle` 派生种子，并在激活 Ability 时写入 GameplayEffect，避免正式牌桌战斗依赖 ECS Entity 索引。尚未迁入牌桌聚合的旧 2D 场景能力仍保留 Entity 索引本地种子兼容分支；该分支不是联机或回放真相，未来若迁入同一正式战斗入口，必须删除兼容分支。
- 伤害和条件的固定枚举仍是当前内置规则限制；需要可由 Mod 扩展的伤害语义前，必须先按 EX-GAS GameplayTag / GameplayEffect 条件的正式能力重新裁决，不能在 GameCore 再造枚举或本地标签表。
- StackCraft 的命中 / 闪避 / 暴击与 RPS 克制已由 GNS/EX-GAS 伤害链接管：`DamageSolver` 使用模板源码的命中差值钳制、减法防御、克制倍率、命中后暴击顺序和暴击倍率；攻击力输入来自正式 GameplayEffect 的 `FlatDamage + Attack * ScalingFactor`，RPS 来源 / 目标语义来自 EX-GAS `Combat.*` 标签和 `FormalDamage.Matchups`，不恢复 `CombatStats` 或 `CombatType`。纯牌桌角色的实际伤害 / Miss / Critical / 优势 / 劣势结果通过 `AbilitySystemDamageResolvedPresentationEvent` 投影到 `TabletopCardView`，并在地基测试场景驱动现有 `CameraShake`；该事件只服务表现，不承担规则结算。
- StackCraft 的投射物前摇、战斗起手 / 命中音效、未命中 / 暴击音效，以及 `HitUI` 式命中图标和 punch 缩放，已经由 `Battle`、`TabletopViewSettings`、`TabletopProjectileView` 和 `TabletopCardView` 接管。它们都是牌桌表现链，只播放反馈，不承担伤害或命中规则结算；Unity 场景重建与 PlayMode 回归仍需等编辑器独占后补跑。

### ASC 长期状态契约

官方 `EX-GAS-2.0` README 与本地 `2.0.4` 源码已经重新校准，完整接口证据见 [`ai-quick/ex-gas-runtime.md`](ai-quick/ex-gas-runtime.md)。当前接口事实是：

- Cell 可读取 ASC 等级、按已知属性码读取 `BaseValue` / `CurrentValue`、按已知技能码取得 `AbilitySpec`。
- Cell 在玩家构建中不能枚举全部固有标签、属性集或已授予技能；相关标签和属性集枚举被 `UNITY_EDITOR` 包围，技能全集只在 Cell 未公开的 Controller 上。
- 官方 `GASWatcher` 是编辑器调试工具，不是业务存档 API。
- 官方把 RPC/网络同步列为 3.0 后续计划，当前 2.0.4 没有可直接复用的 GAS 网络复制契约。

模块 8.3 已按 UE GAS 职责校准后裁决：不修改 EX-GAS，不导出完整 ASC。当前角色内容定义引用的 ASC 预设是能力结构来源；角色实例只保存当前已成立的长期运行事实，也就是 ASC 等级和预设所声明属性的 `BaseValue`。恢复时用当前预设重建 Cell，再覆盖这些基础值。预设结构缺失或快照出现未知/重复属性时拒绝恢复，不通过反射、Watcher 或 ECS Buffer 旁路。

当前尚未成立永久技能授予、永久标签变化和职业成长的正式角色领域入口，因此本轮不提前保存这些未来集合。等对应业务成立时，先由角色长期状态拥有变化事实，再用 EX-GAS 配置重建；只有确认某类事实只能由 GAS 拥有且 UE GAS 同职责也要求由 ASC 暴露时，才重新提出插件扩展。活动 Ability、Cooldown、持续/周期 GE、临时标签、Cue、Timeline 进度和战斗随机流继续明确排除。

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
- 2026-08-10 已在打开的 Unity Editor Test Runner 重新验证：`FormalDamagePipelineEditModeTests` 为 `7/7`，`GameCore.Tests` EditMode 为 `89/89`，`Gameplay.Tests` EditMode 为 `65/65`，`Gameplay.Tests` PlayMode 为 `9/9`，均为零失败。覆盖角色覆盖、EX-GAS 默认值和钳制、ASC 初始化前读取报错、属性码级查询与事件、伤害写入当前生命、快照恢复后当前值重算、法力消耗与上限分离，以及投射物只携带 GameplayEffect ID 的合同。
- 迁移防回流测试只保留代表性用例；不得把旧项目所有技能、地图、商店、宝箱或剧情语义长期当成框架测试。
- 发现测试只是在保护未登记中转层时，优先审查该中转层是否该删；不先加更多测试。
