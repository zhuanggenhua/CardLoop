---
name: gas-ability-authoring
description: "CardLoop 的 EX-GAS 作者入口：裁决 Ability、Timeline、GameplayEffect/Cue、TargetCatcher、生成管线和项目薄集成，避免旧技能系统或动作游戏样例回流。"
---

# CardLoop GAS Ability Authoring

本 skill 只规定 CardLoop 如何使用 EX-GAS。EX-GAS 的字段、表结构、生成器和编辑器窗口细节以插件自带文档和当前源码为准；旧普攻、背刺、蓄力攻击、2D 动作角色和历史预览案例不保留为 active 项目事实。

## 先读入口

- 插件作者入口：`Assets/Plugins/GAS/package.json`、`Assets/Plugins/GAS/SKILL.md`、`Assets/Plugins/GAS/Wiki/`、`Assets/Plugins/GAS/CHANGELOG.md`。
- 项目边界入口：`.spec/knowledge/features/gamecore-gas.md`。
- 分层入口：`.spec/knowledge/standards/framework-layering.md`。
- 需要 Unity 自动化时：`.spec/skills/unity-skills/SKILL.md` 和项目验证规范。

## 前提锁定

动手前必须明确四项：

- **问题对象**：具体 Ability Code、Timeline ID、GameplayEffect ID、Cue、TargetCatcher、生成配置或编辑器窗口。
- **真相来源**：插件文档、EX-GAS 表源、Luban 生成结果、当前项目源码 / 资源、或用户指定的 Unity 现场。
- **目标入口**：EX-GAS 正式表 / 时间轴窗口 / 生成管线，或 `.spec/knowledge/features/gamecore-gas.md` 已登记的项目薄集成点。
- **验收口径**：本轮要证明作者数据、生成结果、项目集成边界、运行时效果、玩家可见反馈，还是仅做文档裁决。

缺任一项时只补证据，不改代码、表、场景、Prefab 或插件源码。

## 单一真相

- 技能身份、消耗、冷却、阻断、时间轴、命中帧、GameplayEffect、GameplayCue 和 GameplayTag 默认归 EX-GAS。
- GameCore 只提供跨游戏通用的输入、资源、表现、存档和生命周期扩展点；Gameplay 只提供 CardLoop 业务数据和策略。
- 项目侧集成点必须登记在 `.spec/knowledge/features/gamecore-gas.md`；未登记的中转层、包装层、缓存页或第二套时间轴不进入正式链路。
- CardLoop 牌桌 / 卡牌战斗不能直接复用旧 2D 动作角色的场景姿态、武器 Prefab、旧 AbilitySheet 或旧执行资产来冒充同职责完成。
- 旧世界交互 Task、旧动作样例和其它动作 RPG 案例只可作为历史线索，不是新能力入口。

## 作者流程

1. 在 EX-GAS Ability 表创建或选择能力身份。
2. 用 EX-GAS Timeline 表达时间、消耗、Cue、命中 / 效果应用等顺序。
3. 用 GameplayEffect 表达伤害、状态、条件、持续、周期、授予和标签变化。
4. 用 GameplayCue 只转发表现，不承担规则结算。
5. 只有 EX-GAS 官方扩展点确实需要项目语义时，才使用已登记 TargetCatcher、CuePlay 或 GameCore 配置扩展。
6. 生成结果进入正式生成目录；不得手改生成 JSON，不得把生成目录变成项目手写代码区。

## TargetCatcher 裁决

- TargetCatcher 是 EX-GAS 命中 / 目标选择扩展点，不是 GameCore 或 Gameplay 的第二套战斗规则。
- 牌桌卡牌、拖拽、堆叠和开包规则默认回 Gameplay 牌桌聚合，不因使用 GAS 就新建场景 Hitbox 真相。
- 2D / 3D / 牌桌目标捕获必须按当前对象的真实空间语义单独裁决；旧动作角色的本地多边形、朝向或 `Movable` 依赖不能自动套到 CardLoop 牌桌。
- 如果需要新增捕获器，先在 `gamecore-gas.md` 登记允许职责、禁止职责和验证方式，再实施。

## 禁止动作

- 不恢复项目侧 AbilitySheet、ActiveAbilitySheet、PassiveAbilitySheet、旧执行资产或第二套技能时间轴。
- 不把动作游戏例子、旧引擎样例或旧来源项目能力样例当成 CardLoop 当前业务。
- 不为了编辑器窗口方便而新增自动读表、自动保存、自动刷新缓存或替代窗口。
- 不修改 EX-GAS 插件源码来绕过项目职责未收口；确需改 fork 时先写清官方职责缺口、项目需求和验证入口。
- 不把测试绿、窗口有数据或历史截图当成技能链完成；完成声明必须回到当轮目标入口和新鲜证据。

## 输出要求

交付 GAS 相关改动时至少说明：

- 本轮处理的 EX-GAS 对象和项目 owner。
- 采用的插件正式入口或已登记项目集成点。
- 明确没有新增第二套能力、标签、命中、伤害、Cue 或生成真相。
- 验证证据；如果本轮只做文档裁决，说明未进入代码 / Unity 阶段。
