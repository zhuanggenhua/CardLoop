---
name: stackcraft-functional-parity-matrix
description: StackCraft 等价吸收完成矩阵：判断“自有框架承接模板业务”是否完成、端到端和目标对照还差什么时查。
metadata:
  type: doc
  status: 实施中
---

# StackCraft 等价吸收完成矩阵

本文件只回答一个问题：**CardLoop 是否已经用自有 Gameplay / GameCore 框架等价承接 StackCraft 模板业务。**

当前结论：**未完成，正在重审纠偏。** 之前的截图、PlayMode、静态脚本和勾选只能作为历史线索；用户已经用干净模板反馈了开卡包、拖拽手感和合堆层级差异，因此不得继续声称“效果一致”“可以删模板”或“已完整复刻”。

## 当前真相源

- 干净参考：`C:\Gamedev\Unity\demo\StackCraft - Card Stacking Survival Game 1.0.0\Assets\StackCraft`。
- 当前工程：`C:\Gamedev\Unity\Project\CardLoop`。
- 当前任务卡：`../../../tasks/stackcraft-parity-absorption.md`。
- 系统参考吸收流程：`D:\codex-home\skills\absorb-reference\SKILL.md`。

## 完成定义

只有下列 5 项同时成立，才能说“StackCraft 模板业务已被当前框架等价吸收”：

1. 干净参考模板能编译或可静态读取到目标模块，并且有明确同操作路径。
2. CardLoop 只在本轮对照链里运行 StackCraft 模板业务；原创策划、临时扩展、演示 UI 和非模板规则不参与完成判断。
3. 参考源码 / Prefab / Material / Sprite / Audio / Scene / ProjectSettings 中承载玩家可见效果的参数，已经映射到 CardLoop 正式 owner，且没有旧 `Manager`、旧单例、固定场景名、第二套资源、第二套事件或第二套状态回流。
4. 开卡包、拖拽、拆堆、合堆、放置、卡牌视觉、层级和动画参数经过源码与序列化配置对账；截图只用于最终观察差异，不替代参数闭包。
5. Unity 编译、必要编辑器回读、运行链路和最终截图都基于新鲜证据通过；若用户现场反馈与完成声明冲突，以用户现场反馈触发重审。

## 当前验收矩阵

| 验收面 | 当前状态 | 下一步 |
|---|---|---|
| 干净参考基线 | 部分完成：干净模板已能编译，但需要回到同操作路径记录开卡包、拖拽、合堆的源码/资源证据 | 读取干净模板 `CardStack`、`CardController`、`CardInstance`、卡牌设置和相关 Prefab/材质 |
| CardLoop 对照范围 | 未完成：当前同态链混有原创/临时业务和过宽完成声明 | 删除或隔离非模板业务，更新任务卡和对照脚本口径 |
| 开卡包对账 | 待重审 | 对照模板点击/使用卡包的输入、消耗次数、生成卡牌、生成位置和动画 |
| 拖拽/拆堆/合堆手感 | 待重审，用户已指出当前偏离 | 对照命中排序、堆叠列表顺序、拖拽 lead card、跟随阻尼、释放目标和层级排序 |
| 卡牌视觉/层级/动画 | 待重审，不能只用历史截图 | 对照卡面 Sprite、材质、字体、尺寸、Collider、SortingOrder、StackStep、移动时长和粒子 |
| 正式 owner 回流检查 | 待重跑 | 静态扫描 `Gameplay` / `GameCore` 是否仍依赖 `CryingSnow`、`Assets/StackCraft` 或旧模板入口 |
| Unity 验证 | 暂不进入 | 代码/资源静态闭包通过后再按 `testing.md` 的 guard 进入 Unity |
| 删除模板判断 | 未完成 | 删除前必须先建立 frozen reference snapshot 或确认继续用外部干净模板作为参考真相源，并取得用户单独授权 |

## 业务闭包

当前只保留 StackCraft 模板业务对照：

- Starter / Beginning 卡包生成、打开、次数消耗、加权抽取和新卡生成。
- 卡牌拖拽、释放、拆堆、合堆、放置限制、牌堆跟随和渲染层级。
- 卡牌表面、卡包表面、HUD 图标、粒子、材质、字体、碰撞体和基础动画。
- 模板中玩家能直接感知的商贩、收购点、基础配方/任务/战斗效果，只在其进入对应对照闭包时纳入；不得由原创策划替代。

当前排除在本轮完成判断外：

- 《卡牌生存：无限》原创职业、剧本、局外成长、Mod API、联机规则和教程内容。
- 自行扩展的日终策略、自动存档策略、原创战斗命中/闪避/暴击、非模板 UI 面板和测试展示内容。
- 只为了测试方便存在的状态、按钮、任务或资源；它们不能证明模板业务对齐。

## 防误判规则

- 参考工程没有的 bug，CardLoop 有，默认结论是“吸收偏离”，不是“参考工程也需要修”。
- 静态脚本只证明它检查的字段；没有对象级字段对账就不能声称 Prefab、材质、动画或手感已对齐。
- Unity 端到端只证明当前项目入口可运行；没有干净模板同操作证据时，不能证明“复刻参考效果”。
- 完成一项必须在本矩阵和任务卡同时更新；未覆盖项保持未完成。
