---
status: in_progress
---

# StackCraft 模板业务等价吸收纠偏

本任务只跟踪当前纠偏：用 CardLoop 自有 Gameplay / GameCore 框架承接 StackCraft 模板业务效果，并先去掉夹带的原创或临时策划业务，方便逐项对照。

## 当前事实

- 用户已确认：干净 StackCraft 模板的开卡包和拖拽手感更好，且不存在“角色放到灌木丛后拖灌木丛，角色闪一下，灌木丛盖到角色上方”的问题。
- 因此当前问题不是修 StackCraft 参考工程，而是 CardLoop 等价吸收偏离；必须定位偏离来自输入、命中、堆叠、动画、配置、资源或夹带业务中的哪一段。
- 之前的截图、测试和完成勾选只能作为历史线索，不能继续证明“已经对齐”。后续所有完成声明必须回到干净模板同操作、同源码参数、同资源配置和当前正式 owner 对账。

## 当前范围

- 参考真相源：`C:\Gamedev\Unity\demo\StackCraft - Card Stacking Survival Game 1.0.0\Assets\StackCraft`。
- 当前目标工程：`C:\Gamedev\Unity\Project\CardLoop`。
- 先处理三条核心闭包：开卡包、拖拽/拆堆/合堆手感、卡牌视觉/层级/动画参数。
- 暂不把《卡牌生存：无限》原创策划、日终扩展、原创战斗数值、自动存档策略、任务日志扩展或临时 UI 展示纳入对照完成条件。

## 执行清单

- [x] 更新系统 `absorb-reference` skill：参考吸收必须先建立干净参考基线，交互手感必须源码/资源/参数闭包对账。
- [ ] 降级并重写当前 StackCraft 吸收完成口径：不得再声称已全量完成或可删模板。
- [ ] 静态对照干净模板与 CardLoop：输入入口、点击命中排序、拖拽时序、堆叠顺序、动画参数、渲染层级。
- [ ] 删除或隔离当前同态链中的原创/临时业务：日终扩展、原创战斗数值、保存/暂停/标题展示、非模板任务日志扩展、其它不属于模板对照的内容。
- [ ] 修正正式 Gameplay 链路中导致开卡包和拖拽手感偏离的代码/资源/配置。
- [ ] 重新运行静态预检、代表业务审计、`.spec` lint 和 Unity YAML guard。
- [ ] 静态闭包通过后，再进入 Unity 编译/编辑器验证；遇到场景重载弹窗、UnitySkills 阻塞或崩溃时立即停下取证，不重复压工具。
- [ ] 只有源码/资源/配置对账通过后，才生成干净模板与 CardLoop 当前图进行最终观察；截图不得替代参数对账。
- [ ] 删除 `Assets/StackCraft` 只能在用户单独授权后执行，并且删除前必须先建立新的 frozen reference snapshot 或保留外部参考真相源。

## 重点待查文件

- 参考源码：`CardStack.cs`、`CardController.cs`、`CardInstance.cs`、`Default_Card_Settings.asset`、`SRM_Default.asset`。
- 当前正式链路：`TabletopCardStack.cs`、`TabletopCardDragInput.cs`、`TabletopView.cs`、`TabletopCardView.cs`、`FoundationTestSceneHarness.cs`。
- 当前对账脚本：`.spec/tools/gameplay-static-preflight.mjs`、`.spec/tools/stackcraft-business-representative-audit.mjs`。

## 禁止口径

- 不把 CardLoop 里的偏离说成模板 bug。
- 不把“测试通过 / 截图存在 / 初始画面相近”说成手感和交互已对齐。
- 不在模板业务对照阶段继续追加原创策划功能。
- 不用 Unity 端到端替代源码、Prefab、Material、输入和参数对账。
