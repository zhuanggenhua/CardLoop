---
name: testing
description: 测试与验收规范：说明 CardLoop 的验证分层、TDD 适用范围、bug 验收和完成声明证据。
metadata:
  type: doc
  status: 已交付
---

# 测试与验收规范

## 验证分层

- 静态证据：文件、diff、配置、资源引用、GUID、脚本结构、索引链接。
- 轻量 smoke：快速验证入口未断、脚本可运行、菜单/资源/引用存在。
- Unity Editor 自动化：通过当前项目允许的自动化入口读取场景、Console、对象、资源和测试状态。
- EditMode / PlayMode 测试：用于核心运行时逻辑、回归 bug、高风险合同。
- 截图或图面核验：只证明用户可见表现，不替代源码、资源、配置和规则证据。

## Unity 验证标准流程

Unity 验证是“当前项目真实入口能运行”的证据，不是外部来源吸收、源码等价或架构正确性的唯一证据。进入 Unity 编辑器、batchmode、自动化工具或 PlayMode / EditMode 前，必须先完成能脱离 Unity 的代码级预检。

预检至少包括：

- 当前源码扫描：没有恢复旧 Manager / 单例 / DTO / 第二套事件、第二套资源加载、第二套状态或旧来源项目路径。
- 配置与资源对照：新增或修改的 `.asset` / `.prefab` / `.meta` 中 GUID、地址、字段名和生成器常量一致。
- 非 Unity 静态校验：相关文件的 `git diff --check`、`.spec` lint、规范测试、脚本级生成器 / 索引检查均已完成；如果没有 `.sln` / `.csproj` 或独立脚本测试入口，必须明确说明无法脱离 Unity 编译。

当前文档和 Gameplay 静态预检入口：

```powershell
node .spec/tools/spec-lint.mjs
node .spec/tools/gameplay-static-preflight.mjs
```

只有预检无法覆盖的事实，才进入 Unity 阶段，例如 Unity C# 编译、Inspector 序列化回读、Prefab / Scene 实例引用、YooAsset 收集结果、真实输入链、PlayMode 运行结果和玩家可见画面。

每次启动 Unity、进入 PlayMode、跑自动化测试或做场景级验收前，必须先写清四项前提：

- 问题对象：本轮验证的具体功能、场景、测试或编辑器动作。
- 真相来源：当前源码、序列化资源、测试文件、日志或用户指定入口中的哪一个。
- 目标入口 / 环境：使用已打开编辑器、batchmode、自动化工具、Test Runner 还是人工打开的场景。
- 验收口径：本轮只证明编译、资源引用、测试断言、玩家链路、视觉表现，还是规则等价。

写清前提后，必须先跑项目 guard，而不是直接手写 Unity 命令：

```powershell
node .spec/tools/unity-verify.mjs status
node .spec/tools/unity-yaml-guard.mjs
node .spec/tools/unity-verify.mjs preflight --mode batch
```

`unity-verify` 的 `preflight` 会自动先跑 `unity-yaml-guard`。手工直接运行 `unity-yaml-guard` 只用于在不进入 Unity 的情况下确认 `.meta`、`.unity`、`.prefab` 和常见 Unity YAML 文件没有空文件、非法 GUID、损坏文件头或脚本替换残留。

验证层级按以下顺序裁决，能用低层级证明时不得升级到更重链路：

1. 源码 / 配置 / 序列化对照：用于证明职责、参数、GUID、fileID、引用和规则映射。
2. 编辑器静态查询：用于证明场景对象、组件、资源、Console 和编译状态。
3. EditMode / PlayMode 测试：用于证明可重复的运行时规则、回归 bug 和公开契约。
4. 统一测试场景 smoke：用于证明新正式链路在真实入口可用。
5. 截图 / 录像 / 图面验收：只用于用户可见表现；不得替代源码和规则结算证据。

## Unity 进程与工具链纪律

- 启动 Unity 前先检查当前项目是否已有 `Unity.exe`、`AssetImportWorker`、`UnityShaderCompiler` 进程和 `Temp/UnityLockfile`；已有可用编辑器时优先复用，不为单个逻辑切片重复开普通 Editor。
- 不得并发启动多条 Unity 验证路径；batchmode、普通 Editor 和自动化工具同一时间只能有一个主验证入口。
- 普通 Editor 只用于确实需要编辑器状态、场景对象或人工观察的验证；纯编译、纯测试、纯源码对照优先不用普通 Editor。
- 不得绕过 `.spec/tools/unity-verify.mjs` 手写新的 Unity 启动 / 测试命令，除非 guard 本身损坏；绕过时必须先说明损坏证据和等价的手动检查结果。
- batchmode 测试必须同时检查 Unity 退出码和 `--testResults` 文件。退出码为 0 但 XML 缺失或为空时，只能判定为验证基础设施失败。
- 截图 / 视觉证据类 PlayMode 用例不得通过无头 batchmode 运行；这类用例依赖真实 GameView / 渲染帧和图片写入。

## TDD 适用范围

通用 red-green 流程、公开边界、独立预期值和测试分类以系统 skill `D:\codex-home\skills\tdd\SKILL.md` 为准；本节只规定 CardLoop 的选择和验收口径。

进入 TDD 前，必须锁定真实业务行为、当前公开入口和可观察结果。只有框架形状、程序集结构、未来接口或没有消费者的抽象时，使用静态检查、架构守卫或 smoke，不把它们当成业务 TDD。

CardLoop 的 AI 驱动开发不采用“所有生产代码都严格 TDD”的一刀切方式。玩法、UI 手感、职业、剧本、关卡编辑器、联机 / Mod API 形状仍在变化时，先用设计记录、功能裁决、源码 / 配置 / 资源对账、原型场景、截图 / 日志 / playtest 和统一测试场景验收约束方向；等规则成为正式行为、接入长期边界或出现复现 bug 后，再提升为行为测试或回归测试。

必须优先补失败测试或最小复现：

- 新增核心运行时逻辑。
- 修复已复现 bug。
- 修改无测试保护但风险高的规则结算、数据加载、保存、生成、同步、UI 状态机。
- 第三次复发的同类错误。

可以不机械补同粒度测试：

- 纯文档或规范重构。
- 小范围注释、索引、路由调整。
- 一次性取证脚本。
- 只改变说明文字、不改变行为的内容。
- 仍在验证方向的玩法探索、交互手感、UI 原型、关卡规则草案和 Mod / 联机 API 草案。

即使不写测试，也必须给出对应的验证证据或说明为什么本轮只需要静态验证。

## 测试分类

- 行为、回归和已确认的公开契约：优先使用 EditMode / PlayMode 测试。
- 程序集、依赖、类型形状和职责边界：使用静态校验或 Editor 架构守卫，不计入业务行为覆盖。
- Unity 入口、资源、场景、编译链路和编辑器状态：使用 smoke / Editor 自动化，不替代业务测试。

反射检查方法或类型是否存在、检查私有实现、用 Stub 自证框架行为时，归入架构守卫或辅助验证。

## PlayMode 状态隔离

- PlayMode 测试触碰 SaveSystem、SaveKit、PlayerPrefs、Application.persistentDataPath、临时资源包、全局配置或其它跨用例持久状态时，必须在 UnitySetUp 中配置独立临时目录或独立命名空间，并在 UnityTearDown 中重置入口和删除本轮临时数据。
- 不得让测试默认读写真正运行目录的存档槽位。若测试目标就是验证正式持久目录，必须在用例名和验证说明中写清，并只操作本轮创建的槽位。
- 因历史运行残留导致的槽位满、配置残留或全局状态污染，属于测试隔离缺陷；修复应优先收口测试入口，不用清空用户真实数据来让测试转绿。

## Bug 验收

- 先写清用户原始症状的保真版，保留关键限定词和数量范围。
- 再写清当前证据命中的症状，两者不一致时不得冒充已经修复原 bug。
- 修复后必须回到原始位点验证。
- 如果改动只是止血、跳过、兜底或降噪，必须标为“缓解”，不能叫“修复”。

## 完成声明

声称完成前必须回答：

- 哪个命令、截图、场景、日志、数据或链接证明这件事？
- 是否是本轮新鲜证据？
- 是否覆盖用户原始目标，而不是只覆盖相邻问题？
- 有哪些 known gaps 或未验证项？
