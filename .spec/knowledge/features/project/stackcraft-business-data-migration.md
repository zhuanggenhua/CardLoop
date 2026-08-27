---
name: stackcraft-business-data-migration
description: StackCraft 业务数据迁移清单：对账代表性业务竖切、剩余业务 out-of-scope 范围和删除参考目录门槛时查。
metadata:
  type: feature
  status: 设计中
---

# StackCraft 业务数据迁移清单

## 当前结论

2026-08-27 纠偏：用户要求当前阶段先去掉原创或临时业务，只保留 StackCraft 模板业务方便对照。本文中关于“代表性业务验收已完成”的旧口径只保留为历史线索；新的有效结论以 [`stackcraft-functional-parity-matrix.md`](stackcraft-functional-parity-matrix.md) 和任务卡 `../../../tasks/stackcraft-parity-absorption.md` 为准。

“机制效果已通过”不等于“模板业务数据已全量一致”。当前 Gameplay 框架已经证明能承接 StackCraft 的牌桌、行动、任务、卡包、交易、日终、战斗和 UI 反馈机制；StackCraft 原始业务 `.asset` 没有全部转换成 CardLoop 作者源。

当前用户已把业务验收范围收窄为“代表性验收”：不要求完整实现 StackCraft 全部业务内容，只要求证明 CardLoop 自有框架能承接有代表性的模板业务竖切。因此，剩余业务资产不再是当前阶段完成阻塞；它们是后续可选迁移或明确放弃范围。

2026-08-17 已新增并通过只读审计：

```powershell
node .spec/tools/stackcraft-business-representative-audit.mjs
```

该审计证明 Starter / Beginning 两个代表性业务竖切已经映射到 CardLoop 作者源：Starter 固定槽位、Beginning 三次打开槽位、权重、5 个配方候选、10% 配方概率、Beginning 商贩价格 `3` 和解锁任务数 `3` 均与 StackCraft 参考资产对齐。它不证明 273 个 StackCraft 原业务 `.asset` 已经全量迁移。

因此当前阶段的结论是：**代表性业务验收已完成，模板业务全量迁移不属于当前完成条件**。`Assets/StackCraft` 是否删除是单独的破坏性动作，不能由代表性验收自动推出。

真正删除 `Assets/StackCraft` 前必须满足：

1. 用户当轮明确授权删除参考目录。
2. 先裁决参考真相源：继续保留 `Assets/StackCraft` 作为参考目录，或把代表性审计需要的来源资产 / 参数冻结到项目参考快照并改造审计脚本。
3. `node .spec/tools/gameplay-static-preflight.mjs` 通过，证明正式链路没有依赖参考目录。
4. `node .spec/tools/stackcraft-business-representative-audit.mjs` 通过，证明代表性业务竖切仍可追溯；若已删除 `Assets/StackCraft`，该脚本必须先改为读取 frozen reference snapshot。
5. 剩余未迁移业务资产被明确记录为当前阶段 out-of-scope，或另行迁移完成。
6. 删除后重新跑 Unity 编译和必要 PlayMode，证明删除没有破坏当前正式链路。

## 原始数据规模

来源根目录：`Assets/StackCraft/Resources`。

| 类别 | 原始数量 | 当前状态 |
|---|---:|---|
| 卡牌 | 103 | 只明确转换 Starter / Beginning 竖切相关卡牌和地基测试卡。 |
| 卡包 | 11 | 已转换 `00_Pack_Starter`、`01_Pack_Beginning`；其余 9 个待迁移或待放弃。 |
| 配方 | 90 | 已转换 Beginning 里用于展示研究发现的 5 张配方卡 / 行动；其余待迁移或待放弃。 |
| 任务 | 66 | 任务机制已实现多种事实类型，但原任务链内容未全量转换。 |
| 遭遇 | 3 | 日终遭遇机制已实现，原遭遇内容未全量转换。 |

## 已明确迁移的业务竖切

| 来源 | CardLoop 承接方式 | 备注 |
|---|---|---|
| `00_Pack_Starter.asset` | `CardPackDefinition` + 地基测试作者源 | Starter 显示名、描述和固定槽位已按模板映射。 |
| `01_Pack_Beginning.asset` | `CardPackDefinition` + `PackVendorDefinition` + 地基测试作者源 | Beginning 卡包、商贩、部分卡牌、部分配方候选和生物基础行为已映射。 |
| StackCraft 基础图片 | `Assets/Art/Sprites/StackCraft` | 只表示素材已复制为项目自有 GUID，不表示业务数据已转换。 |
| StackCraft 音效 / 粒子反馈 | `Assets/Audio/SFX`、`Assets/Art/Prefabs` | 用于机制反馈，不代表全部模板表现资源已用到。 |

代表性竖切验收入口：`node .spec/tools/stackcraft-business-representative-audit.mjs`。该脚本只读 StackCraft 参考资产和 CardLoop 当前作者源，不启动 Unity，不修改资源。

## 当前代表性完成门槛

| 门槛 | 证据 |
|---|---|
| 模板正式依赖没有回流 | `node .spec/tools/gameplay-static-preflight.mjs` 通过。 |
| 代表性业务参数对齐 | `node .spec/tools/stackcraft-business-representative-audit.mjs` 通过。 |
| 文档不再把代表性验收冒充全量业务迁移 | 本文明确区分“代表性完成”和“全量迁移未做”。 |
| 剩余业务不阻塞当前阶段 | 未迁移卡牌 / 卡包 / 配方 / 任务 / 遭遇登记为后续可选迁移或放弃范围。 |

## 未迁移业务范围

| 范围 | 不能直接继承的原因 | 正确迁移方式 |
|---|---|---|
| 剩余卡牌资产 | StackCraft 旧 `CardDefinition` 混合食物、装备、结构、敌人、价值物和特殊空类型；直接复制会回流旧 schema。 | 按 CardLoop 的 `CardDefinition`、`FoodCardDefinition`、`CharacterCardDefinition`、`EquipmentCardDefinition`、`ChestCardDefinition`、`PackVendorDefinition` 等作者源转换。 |
| 剩余卡包资产 | 旧卡包依赖 `Resources.LoadAll`、旧配方发现和旧卡牌定义。 | 转为 `CardPackDefinition`，槽位、权重、配方候选和商贩价格分别落到正式作者源。 |
| 剩余配方资产 | 旧 `RecipeDefinition.Execute` 直接产生世界副作用。 | 转为 `ActionDefinition` 条件和结果意图；执行仍由 `ActionResultSettlement` / `ScenarioRun` / `Tabletop` 等正式 owner 完成。 |
| 原任务链 | 固定 `QuestType` 枚举和旧 `QuestManager` 不适合 Mod 扩展。 | 转为 `QuestDefinition` 与具体 `QuestTaskDefinition` 子项，事实由当前单局 `ScenarioRun` 提交。 |
| 原遭遇 | 旧遭遇直接依赖模板卡牌定义和日终管理器。 | 转为 `ScenarioDayEncounterRule` 或后续剧本事件作者源，生成结果必须使用当前牌桌和权威随机链。 |

## 后续可选迁移顺序

以下顺序只在后续决定继续吸收 StackCraft 业务内容时执行，不属于当前代表性验收完成条件：

1. 生成完整业务数据对照表：原路径、原显示名、原类型、引用资源、引用卡牌、迁移状态、目标 CardLoop 作者源。
2. 先迁移剩余卡包，因为卡包能暴露大量缺失卡牌和配方引用。
3. 再迁移被卡包引用的卡牌，按当前作者源类型拆分，不保留 StackCraft 大一统字段。
4. 再迁移配方为行动定义，保持“只声明条件和结果意图，执行由正式结算模块完成”。
5. 最后迁移任务和遭遇内容，验证它们能驱动已迁移的卡牌、卡包和行动。

## 删除参考目录前门槛

- 参考真相源已经裁决：保留 `Assets/StackCraft`，或建立 frozen reference snapshot 并让对账脚本改读快照。
- `node .spec/tools/gameplay-static-preflight.mjs` 通过。
- `node .spec/tools/stackcraft-business-representative-audit.mjs` 通过。
- 剩余业务资产已被用户接受为当前阶段 out-of-scope，或已经另行迁移完成。
- 用户当轮明确授权删除 `Assets/StackCraft`。
- 删除 `Assets/StackCraft` 后 Unity 编译 `0` 错误。
- 删除 `Assets/StackCraft` 后全量 PlayMode 通过。
