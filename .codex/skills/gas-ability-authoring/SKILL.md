---
name: gas-ability-authoring
description: "CardLoop 的 EX-GAS 技能制作、修改、排查和验收流程。用于 Ability、Timeline、GameplayEffect/Cue、TargetCatcher、普攻、背刺、蓄力攻击、命中范围预览；避免旧 AbilitySheet、旧执行资产或第二套命中框回流。"
---

# CardLoop GAS Ability Authoring

## 官方文档优先

动手前先读 GAS 插件自己的文档；本 skill 只是 CardLoop 项目侧适配，不替代 EX-GAS 官方建议：

- `Assets/Plugins/GAS/package.json`：确认 EX-GAS 版本、官方文档 URL 和仓库入口。
- `Assets/Plugins/GAS/SKILL.md`：确认 AbilityLogic、GameplayCue、MMC、AbilityTask、TargetCatcher、XParam 和生成管线规则。
- `Assets/Plugins/GAS/Wiki/EX-GAS.md`
- `Assets/Plugins/GAS/Wiki/Ability.md`
- `Assets/Plugins/GAS/Wiki/GameplayEffect.md`
- `Assets/Plugins/GAS/Wiki/GameplayCue.md`
- `Assets/Plugins/GAS/Wiki/MMC.md`
- `Assets/Plugins/GAS/CHANGELOG.md`

项目文档 `.spec/knowledge/features/gamecore-gas.md` 只登记 CardLoop 如何接入、哪些桥接被允许、哪些旧迁移残留已排除。

## 先锁定

动手前先明确四件事：

- **问题对象**：具体是哪一个 Ability Code、Timeline ID、GameplayEffect ID、Cue、TargetCatcher 或打开中的时间轴窗口。
- **真相来源**：优先是 GAS 插件自带文档、EX-GAS 表、EX-GAS 时间轴窗口、Luban 生成结果、当前打开 Unity 窗口状态和现有测试证据。
- **目标入口**：默认是 `EXTool/EX-GAS/时间轴技能编辑器`、EX-GAS Ability / Timeline / GameplayEffect / Cue 表，以及项目侧已登记的 GAS 扩展。
- **验收口径**：回到用户原始位点验证，例如当前打开窗口、当前预览实例、当前命中帧、当前 SceneView 手柄、真实普攻链路或定向测试。

缺任一项时，只补证据；不要先改代码或补 UI。

## 单一真相

- 技能身份、规则、消耗、冷却、阻断、时间轴、命中帧、伤害、Cue 触发，默认由 EX-GAS 承担。
- 项目侧只能保留已登记的薄扩展：`exgas.abilityGameCore`、`CuePlayGameCoreAnimator`、`CuePlayGameCoreAudio`、`CuePlayGameCoreFeedback`、`CatchAreaPolygon2D`、正式伤害桥等。
- `TaskApplyWorldElement` / `XParamApplyWorldElement` 属于旧 `Flamethrower / 持续喷火` 样例桥接，不是当前 GameCore / EX-GAS 正式主线；不要把它作为新技能制作入口。
- 不恢复 `AbilitySheet`、`ActiveAbilitySheet`、`PassiveAbilitySheet`、`MeleeAbilityExecutionAsset` 或项目侧第二套时间轴作为正式技能的正式入口。
- 如果 EX-GAS 缺能力，优先在 GAS 主轴或项目 fork patch 内补正式能力；不要新造并行制作窗口或缓存页。

## 制作流程

1. **Ability**
   - 在 EX-GAS Ability 表中新建或选择 Ability。
   - `AbilityLogic` 选 `ALTimeline`。
   - `AbilityLogic.Param.ID` 指向 Timeline ID。
   - 项目输入、根模式和本地门控放在 `exgas.abilityGameCore`，只引用 EX-GAS Ability Code。

2. **Timeline**
   - 在 EX-GAS Timeline 中配置 `LifeTime` 和轨道。
   - `TaskDoCost` 负责消耗。
   - `TaskPlayCue -> CuePlayGameCoreAnimator` 负责装备系统动作键。
   - `TaskApplyEffects + TargetCatcher` 负责命中帧目标捕获和应用 GameplayEffect。

3. **动画与素材**
   - `CuePlayGameCoreAnimator.AnimationName` 填装备系统动作键，例如 `Attack` 或 `ChargedAttack`。
   - GAS 表不配置 `Attack_Up`、`Attack_Down`、`Attack_Left`、`Attack_Right` 这类方向动画名；方向变体由 Animator / 装备动画系统根据开火时角色朝向解析。
   - 像素攻击动作优先用 Animator 子状态机或项目装备动画解析做离散方向状态；Blend Tree 主要用于移动、待机等连续朝向动作，不作为普攻方向变体的默认承载。
   - CardLoop 当前素材组织是角色动作和武器层分离；武器攻击与武器自带特效随装备/武器动作播放。
   - 只有用户拆出正式独立特效素材后，才新增独立特效 Cue；不要用临时特效 Prefab 冒充完成。

4. **命中范围**
   - `TaskApplyEffects.IDs` 填 GameplayEffect ID。
   - `TargetCatcher` 优先用 `CatchAreaPolygon2D` 表达施法者本地空间里的真实近战范围。
   - `points` 保存一份本地多边形；运行时由 TargetCatcher 按命中帧施法者的当前位置与当前朝向变换到世界空间。
   - GAS 激活上下文保存本次请求的起点、可选输入瞄准方向和可选主目标；输入方向不自动等于 Timeline 任务执行时的最终方向。
   - `CatchAreaBox2D`、`CatchAreaCircle2D`、`CatchAreaPolygon2D` 这类施法者本地空间捕获器读取命中帧的施法者姿态；施法者缺少 `Movable` 或有效朝向时直接报错并拒绝命中。
   - 普通攻击依靠正式攻击状态阻止前摇期间普通转向，因此命中帧读取当前姿态仍会保持起手方向；闪现、突进、强制转身技能只需在命中 Task 前更新施法者位置与朝向。
   - `layer` 保存可命中目标层。
   - 横版、3D 或俯视角默认都不要求为左/右/上/下分别配置判定；只有动作视觉明显不对称且手感验证失败时，才考虑方向 override。
   - 不在场景里手摆武器 Hitbox 子物体作为技能真相。

5. **GameplayEffect**
   - 普通伤害写 `FormalDamage`。
   - 背刺等条件附加伤害写同一个 GE 上的 `FormalConditionalDamage`。
   - 伤害数值不写在 `TaskApplyEffects` 里。
   - 受击反馈、冲击、无敌时间、飘字等表现参数优先在 GE 侧配置。

6. **保存与导出**
   - 时间轴调整后点击 EX-GAS 时间轴窗口原保存按钮。
   - 表入口在 `EX_GAS_Config/ProjectConfigTable/exgas_config/Datas`。
   - Luban 生成结果进入 `Assets/DataGenerated/Luban/Json/GAS`。
   - 不手改生成 JSON。

## 预览与编辑

- 打开 `EXTool/EX-GAS/时间轴技能编辑器`。
- 选择 Timeline，例如普攻 `101`。
- 在 `预览实例` 里指定当前场景角色，例如 `EquipmentSystemDemoCharacter`；不要自动绑定。
- 选中 `TaskApplyEffects -> CatchAreaPolygon2D` 片段。
- 选中后应自动跳到命中帧并刷新预览；SceneView 中显示按当前预览对象朝向变换后的多边形手柄。
- 拖中心点移动整体，拖顶点调整轮廓，右键插入或删除顶点。
- 如需检查不同朝向，编辑器只能切换预览朝向或调整预览对象朝向；不得保存四份方向判定。
- 调整后仍用 EX-GAS 原保存按钮写回表；不要新增自动保存、自动读表、自动重建缓存或第二套保存入口。

## 排查顺序

1. 当前窗口是否已经打开并选中正确 Timeline。
2. `预览实例` 是否为空。
3. 选中的是否是 `TaskApplyEffects`，不是 GameplayEffect 配置页。
4. 当前 Task 的 TargetCatcher 参数是否为 `CatchAreaPolygon2D` 或已支持的 2D 捕获器。
5. SceneView 手柄能否解析当前时间轴窗口、当前 Inspector 对象和预览实例。
6. Console 是否有 Odin/Unity 兼容错误，例如 `MissingFieldException`、`OdinPropertyException`、`SmartFloatField`、`SmartIntField`。
7. 若是运行时不生效，再查 Ability 激活、Timeline 推帧、TargetCatcher 捕获、GameplayEffect 应用、正式生命值变化。

## 禁止动作

- 不为“页面看起来有数据”增加打开窗口自动读表、构造时读表、自动刷新、自动保存或自动选中。
- 不新造项目侧替代窗口、替代时间轴、替代命中框保存入口。
- 不把 TopDownEngine 的武器 Prefab / CharacterAbility 运行时直接混进 EX-GAS 技能主轴。
- 不把 2DRPGEngine 的时间轴或编辑器当作正式运行时 owner。
- 不把多方向素材误解为多方向 GAS 配置；一个动作键、一条 Timeline、一份本地命中形状是默认结构。
- 不把攻击方向做成所有 Ability 的必填配置；激活上下文中的输入方向只是可选请求数据，默认近战 TargetCatcher 以执行帧施法者姿态为准。
- 不把插件小报错止血说成根因修复；必须回到原位点验证。
- 不在未验证当前窗口状态时调用 `ShowWindow()` 重新打开窗口冒充用户原始位点。

## 需要深挖时

- GameCore / EX-GAS 正式边界：先查 `.spec/knowledge/features/gamecore-gas.md`；未登记桥接不得当作正式框架能力。
- 完整制作流程：以 EX-GAS 表、`EX_GAS_Config`、`Assets/DataGenerated/Luban`、`.spec/knowledge/features/gamecore-gas.md` 和当前 Unity 验证为真相源。
- 插件边界与验证规则：先查 `.spec/rules/system.md` 和 `docs/FantasyWord-framework-migration.md`。
- Unity 自动化：优先查全局 `D:\codex-home\skills\unity-skills\SKILL.md`，再结合本项目本地包 `Packages/com.besty.unity-skills`、`Packages/com.aibridge.unity`。

