---
name: unity-serialization-safety
description: Unity 场景、Prefab、资源和 YAML 序列化文件的安全写入规范。
metadata:
  type: doc
  status: 已交付
---

# Unity 序列化写入安全

本文件回答“什么时候能手工或脚本写 Unity 序列化文件、写完怎么证明没破坏结构”。修改 `.unity`、`.prefab`、`.asset`、`.controller`、`.anim`、`.mat`、`.meta` 或 `ProjectSettings/*.asset` 前必须查。

## 写入前

- 重新读取当前磁盘文件和目标文件的当前工作区状态；不能用旧扫描、旧 diff、旧工具回执或记忆覆盖现场。
- 优先做字段级、组件级、对象块级或编辑器定点保存；只有文件截断、对象块破碎、无法局部修复或用户明确要求灾难恢复时，才允许整文件重写。
- 写入范围必须用 `fileID`、对象块头、Prefab GUID、target fileID、propertyPath 或足够唯一的上下文锚定；不得只用 `m_Sprite`、`m_Material`、`m_Text` 这类重复字段短上下文替换。
- 用户已经手动调过场景或 Prefab 时，当前现场优先；无法区分用户改动和本轮误改时，停止写入并先列出可疑对象、组件 ID 和 diff。

## YAML 结构守卫

- 进入 Unity 编辑器、Unity 自动化或 batchmode 前，先用项目工具做静态结构检查；正式入口是：

```powershell
node .spec/tools/unity-yaml-guard.mjs
```

该工具只检查会破坏 Unity 导入的结构问题：空 `.meta`、缺失或非法 GUID、`.unity` / `.prefab` 文件头损坏、Unity 对象块缺失，以及 `$1` 这类脚本替换残留。行尾风格、历史插件格式和普通换行差异不能作为损坏结论。
- 整文件写回时必须保留 Unity YAML preamble、对象块原顺序和尾部空白；结果必须仍以标准头开头：
  - `%YAML 1.1`
  - `%TAG !u! tag:unity3d.com,2011:`
  - 第一条 `--- !u!...` 对象块
- 禁止只拼对象块生成最终 `.unity` / `.prefab`；必须写回完整文件结构。
- 禁止用会被 PowerShell 提前展开的字符串直接写 Unity 序列化文件。涉及 `$1`、`$&`、反向引用或正则替换时，必须使用 `apply_patch`、单引号 here-string、Node 脚本的字面字符串，或先在临时副本上验证输出；不得把 shell 字符串拼接当作 Unity YAML 写入器。
- 写入后立即回读并检查：标准头数量、对象块数量、重复 fileID、本地 fileID 引用、GameObject 组件块是否存在、组件块 `m_GameObject` 是否指回正确宿主。
- 批量替换必须保持字段缩进不变；缩进就是层级真相。写回后至少抽查关键字段仍在正确对象块和正确层级。

## Prefab 与场景实例

- 修改 Prefab asset 后，如果存在明确目标场景消费者，必须核对目标场景里对应 PrefabInstance override 是否覆盖本轮字段。
- 对齐方式必须按 `Prefab GUID + target fileID + propertyPath`，不能只按节点名或组件类型猜。
- 如果场景 override 没有场景专属含义，且目标值应该跟随 Prefab，应清理该 override，而不是把 override 改成同值后继续保留。
- 清理 override 前必须确认目标字段、当前 Prefab 值和场景差异；位置、尺寸、排序、场景对象引用等确有场景差异的字段继续保留。
- 任务明确是 Prefab 本体恢复时，默认只写 Prefab asset 及直接依赖；场景实例只读核对和记录风险。确实必须写场景时，先说明场景路径、对象路径、组件、字段和为什么 Prefab 无法表达。
- 任务明确是场景实例、关卡专属参数、场景内唯一引用或场景级显隐时，才把 `.unity` 作为写入目标。

## 资源本体核对

- 用户反馈 Prefab、Sprite、Material、Texture、粒子或 UI 节点本体为空、预览不对、字段为空时，优先查资源本体字段和 GUID / fileID，不用运行时日志替代。
- Sprite 的矩形、九宫格 border、pivot、outline、自动/网格切片等元数据归 Unity Importer / Sprite Editor Data Provider 所有，不得手工拼写或批量替换 `.meta` 来改这些数据。需要修改时，必须通过 Unity Editor、UnitySkills importer 能力或官方 `ISpriteEditorDataProvider` 路线，并先检查 importer 是否支持对应编辑能力；能力检查失败就是停止条件，不能绕过后继续写入。
- 粒子 `ParticleSystemRenderer` 必须按 Renderer 模块核对：`m_RenderMode`、`m_NormalDirection`、材质、排序层、遮罩、粒子尺寸、材质 shader、贴图槽和透明队列。
- 截图、录屏或编辑器观察只能作为视觉目标和差异定位证据，不能单独反推玩家、相机、出生点、物理对象或玩法对象 Transform。

## 写入后验收

- 写完后必须用同一路径回读目标块，确认命中的是目标对象，不是同名同类组件。
- 对 UI、Prefab、场景层级或组件挂载的写入，必须至少核对：同一父节点下是否出现同名 / 同职责副本、同一对象是否多出重复组件、原正式入口是否被新副本绕开。
- 如果写入路径包含 Prefab 导出、实例 Apply、覆盖同步或批量回写，必须额外证明这次命中已有对象，而不是新追加一份。
- `git diff --check` 和 `.spec` / 文档校验不能替代 Unity 资源结构回读；它们只证明文本层没有明显格式错误。

## 已打开场景被外部写盘

- 如果场景生成器、YAML 修复或外部进程改写了 Unity 正在打开的 `.unity` 文件，Unity 弹出重载确认是预期保护，不是用户手动改动，也不是编译错误。
- 这类弹窗必须按目标场景处理：确认磁盘改动属于本轮目标后，优先接受磁盘版本；如果不接受，Unity 内存里的旧场景后续保存可能覆盖本轮写盘结果。
- 需要自动确认时，只允许使用项目工具 `node .spec/tools/unity-confirm-scene-reload.mjs --project CardLoop --scene <SceneName> --confirm-reload-dialog`；该工具不是通用窗口点击器，不能用于未知弹窗。
