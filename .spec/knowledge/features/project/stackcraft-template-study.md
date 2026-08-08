---
name: stackcraft-template-study
description: StackCraft 模板导入后的设置恢复结论、框架吸收裁决与 Gameplay 后续底座设计约束。
metadata:
  type: feature
  status: 设计中
---

# StackCraft 模板研究裁决

## 记录状态

- 本文记录 2026-08-01 导入 `Assets/StackCraft/` 后的当轮裁决。
- StackCraft 当前只作为参考模板和候选底座保留；静态导入不等于 CardLoop 已正式启用它的运行时、场景、输入、存档或渲染配置。
- 用户口径：该恢复的项目配置可以恢复；如果模板有自己的渲染配置可以保留；主要目标是参考其框架设计，结合《卡牌生存：无限》搭建游戏地基；若模板完全胜任，后续可再评估直接采用。

## 当轮恢复结论

- `ProjectSettings/` 中被模板覆盖的全局项目设置应回到 CardLoop 基线，避免模板接管项目身份、构建场景、输入模式、质量/图形设置和标签层。
- `Assets/StackCraft/Settings/` 保留为模板自带配置，不作为全局项目设置启用。当前保留项包括 `Default_Card_Settings.asset`、`SRM_Default.asset` 与 `URP/URP_Asset.asset`、`URP/URP_Renderer.asset`、`URP/URP_GlobalSettings.asset`。
- `Packages/manifest.json` 与 `Packages/packages-lock.json` 的当前差异没有证据表明来自 StackCraft 模板覆盖，不能按“模板该恢复项”处理；后续若要收口，需单独以本地 UPM 包来源和 Unity 编译结果为真相源裁决。

## 模板范围

- 导入路径：`Assets/StackCraft/`。
- 静态规模：约 102 个 C# 脚本、278 个 `.asset`、31 个 prefab、150 张 png、3 个 Unity 场景，以及若干材质、音频、模型、shadergraph 和文档。
- 主要目录：`Card`、`Crafting`、`Core`、`Combat`、`Quest`、`Pack`、`Encounter`、`SaveSystem`、`Trading`、`UI`、`Settings`、`Resources`、`Scenes`。
- 当前未发现 StackCraft 自带 `.asmdef`，脚本会落入 Unity 默认程序集；正式接入前必须先做程序集隔离或迁移到 Gameplay 自有模块边界。

## 可参考的职责内核

- **桌面卡牌表面**：`CardInstance`、`CardStack`、`Board`、`CardManager` 展示了卡牌拖拽、堆叠、边界限制、重叠解算、生成和销毁的基本闭环，可作为牌桌交互的参考。
- **卡牌定义模型**：`CardDefinition` 用 ScriptableObject 表达卡牌身份、显示、类别、阵营、战斗属性、掉落、食物、装备和职业变更，是“卡牌承载多种对象”的有用样例。
- **堆叠规则矩阵**：`StackingRulesMatrix` 用卡牌类别到卡牌类别的矩阵表达能否堆叠，可参考其编辑器体验；但 Gameplay 需要升级为符号、标签、动作上下文和世界规则共同参与的规则判定。
- **配方与行动进度经验**：`RecipeDefinition`、`CraftingManager`、`CraftingTask` 展示了材料数量、消耗模式、持续时间、连续配方、随机权重和产物生成；这些只能作为 Gameplay 新行动/配方职责的参考，不能与旧 GameCore 背包配方并行成为正式系统。
- **运行演示闭环**：Pack、Quest、Encounter、Combat、DayCycle、SaveSystem 和 UI 能证明模板是一个完整小型成品，而不是零散代码片段。
- **渲染与视觉配置**：StackCraft 的 URP 资产、材质、卡面和场景可作为视觉参考或候选配置，不应直接覆盖 CardLoop 全局图形设置。

## 不宜直接接管的部分

- **扩展边界不足**：当前卡牌类别、配方类别、战斗类型、装备槽等大量语义是枚举和硬编码字段；这不适合多世界、多规则、职业树、Mod 和关卡编辑器作为一等入口。
- **Mod 不友好**：核心数据通过 `Resources.LoadAll` 扫描固定目录，缺少外部 Mod 包、版本、依赖、校验、覆盖顺序和热加载边界。
- **关卡编辑器不完整**：模板有场景、任务和配方编辑器样例，但没有面向剧本目标、世界规则、地图池、事件池、初始购买池和符号配方的统一作者源。
- **输入和平台假设偏旧**：多处直接使用 `Input.GetMouseButton`、`Input.mousePosition`、`KeyCode` 和 `PlayerPrefs`；Gameplay 后续需要保留新 Input System、可测试输入适配和更清晰的设置职责。
- **运行时耦合较重**：大量系统通过 `public static Instance` 串联，并绑定固定场景名、固定存档槽和 `Application.persistentDataPath` JSON 文件；正式接入前要先拆出内容数据、运行时状态、场景切换和存档职责。
- **小队/角色语义不足**：模板的角色更接近 Stacklands 式卡牌单位；Gameplay 需要独立角色实体，记录职业、经历、特性、技能、阵营、可控性和跨世界成长。
- **符号配方能力不足**：模板配方按具体 `CardDefinition` 匹配材料；Gameplay 明确需要“满足某类符号即可”的配方条件，所以需要标签、谓词或规则模块。

## 当前输入兼容状态

- 已为 StackCraft 参考样例新增 `Assets/StackCraft/Scripts/Core/StackCraftInput.cs`，把模板脚本里的指针位置、鼠标按钮、滚轮和取消键读取改为 Unity 新 Input System。
- 已把 `Assets/StackCraft/Scenes/Title.unity` 与 `Assets/StackCraft/Prefabs/UI/UIRoot.prefab` 的旧 `StandaloneInputModule` 替换为 `InputSystemUIInputModule`，避免在 `activeInputHandler: 1` 时触发旧输入 API 异常。
- 已新增编辑器菜单 `Gameplay/StackCraft/Fix Reference Input Compatibility`，用于在 Unity 内重新扫描 StackCraft 场景和 UI 预制体，并把 UI 输入模块绑定到 `Assets/InputSystem_Actions.inputactions`。
- 当前验证只覆盖静态扫描和 `.spec` 链接检查；因为 Unity Editor 当前已打开且 REST/CLI 未响应，还没有完成 StackCraft PlayMode 操作验收。

## 当前裁决

- **保留**：完整保留 `Assets/StackCraft/`，作为可运行模板、视觉参考和框架研究对象。
- **不接管**：不让 StackCraft 的 `ProjectSettings`、构建场景、全局 URP/Quality、PlayerSettings、旧输入配置接管 CardLoop。
- **不整包启用**：在完成 Mod、关卡编辑器、角色实体、世界规则和输入/存档职责裁决前，不把 StackCraft 直接作为正式底座。
- **优先改造吸收**：后续以职责切片吸收桌面卡牌、堆叠规则、行动进度、配方经验和演示 UI；保留模板源码作对照，不把示例世界观、枚举语义、固定目录或 StackCraft 配方执行链当成 CardLoop 正式事实。
- **逐系统吸收表**：后续模块设计以 [`stackcraft-system-reference-matrix.md`](stackcraft-system-reference-matrix.md) 为准；冲突项放在该矩阵末尾统一追踪。

## 后续建议

- 首个可玩切片应优先验证：角色卡拖到地点卡，弹出行动选项，执行探索/采集，产生资源或事件，并能按天结算饥饿。
- 数据底座应先定义 Gameplay 自己的内容职责：卡牌、角色、地点、行动、配方、符号、世界规则、剧本目标和初始购买池。
- Mod/关卡编辑器从第一版就要进入数据结构设计：即使 UI 暂缓，也要让数据格式、校验和加载顺序可扩展。
- 若后续评估“直接采用 StackCraft”，最低需要先完成：程序集隔离、Unity 编译、示例场景运行、输入模式适配、存档路径隔离、Resources 加载替代方案和符号配方原型。
