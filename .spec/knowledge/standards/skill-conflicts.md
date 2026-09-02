---
name: skill-conflicts
description: 外部 skill 与现有 airule、AGENTS、项目 skills 的整合矩阵：说明保留、合并、改写和已收口项。
metadata:
  type: doc
  status: 已交付
---

# Skill 整合与冲突矩阵

本文件记录外部 skill 被审查后与 CardLoop 现有规范的关系。原则：**外部内容只作候选，执行红线以现有系统/项目规则为准**。

## 已合并为项目 `.spec` skill

| 上游候选 skill | 本项目落点 | 处理结果 |
|------------------|------------|----------|
| `spec-steward` | `.spec/skills/spec-steward` | 合并。吸收“放对位置、同步索引、反复错误升级”，并改成本项目五层落点规则。 |
| `before-you-code` | `.spec/skills/before-you-code` | 合并。改为四项前提锁定 + CardLoop 渐进披露入口。 |
| `systematic-debugging` | `.spec/skills/systematic-debugging` | 合并。叠加“原始 bug 描述不得改写”和“止血不等于修复”。 |
| `verification-before-completion` | `.spec/skills/verification-before-completion` | 合并。保留“证据先于声明”，适配 Unity/文档/资源验收。 |
| `task-breakdown` | `.spec/skills/task-breakdown` | 合并。保留任务拆解，移除自动 worktree 假设。 |
| `writing-plans` | `.spec/skills/writing-plans` | 改写为路由。长期计划仍以 `D:\codex-home\skills\planning-with-files\SKILL.md` 为准。 |
| `test-driven-development` | `.spec/skills/test-driven-development` | 改写。保留 TDD 方法，但不采用跨项目一刀切强制。 |
| `receiving-code-review` | `.spec/skills/receiving-code-review` | 合并。保留“先核实再改，不表演式认同”。 |
| `brainstorming` | `.spec/skills/brainstorming` | 合并。用于设计/需求未收敛场景，默认先方案后实施。 |
| `subagent-driven-development` | `.spec/skills/subagent-driven-development` | 安全改写。保留任务交接和审查思想；子 agent 模型配置与派发约束由该 skill 统一承载。 |
| `using-git-worktrees` | `.spec/skills/using-git-worktrees` | 安全改写。默认禁止自动创建/切换/删除 worktree，除非用户当轮明确许可。 |

## 已收口的现有项目 skill

| 现有 skill | 状态 | 原因 |
|------------|------|------|
| `aibridge` | 未迁入项目 skill | 源项目未提供可复用脚本入口；Unity 自动化优先查项目薄入口 `.spec/skills/unity-skills/SKILL.md` 和本项目本地包 `Packages/com.besty.unity-skills`、`Packages/com.aibridge.unity`。 |
| `gas-ability-authoring` | 已迁入并薄化为 `.spec/skills/gas-ability-authoring` | CardLoop EX-GAS 作者流程入口；旧普攻 / 背刺 / 蓄力攻击 / 2D 动作角色案例已移出 active 事实，不再作为当前业务依据。 |
| `safe-image-reading` | 未迁入项目 skill | 源项目中该 skill 已暂停且指向旧备份路径；图片展示和验收按系统 skill `D:\codex-home\skills\show-image-to-user\SKILL.md`。 |
| `unity-tilemap-2d` | 已移出 active 项目 skill | 原正文绑定俯视角像素开放世界地图，与当前 CardLoop 卡牌生存主线不匹配；未来若出现正式 Tilemap 需求，再从 UnitySkills / 官方文档抽取当前项目需要的最小入口。 |
| `unity-timeline-signal-debug` | 已移出 active 项目 skill | 当前 `Assets` 未发现 Timeline Signal 使用；通用 Timeline 能力由 UnitySkills `timeline` 模块和官方文档承接，未来出现正式 Signal 链路再抽取项目薄入口。 |
| `D:\codex-home\skills\code-comments` | 保留为系统 skill | 中文注释和 Unity Inspector 说明专项能力；本项目当前没有对应项目 skill。 |
| `unity-ui-development` | 已迁入 `.spec/skills/unity-ui-development` | UGUI / UI Toolkit 专项能力，仍由项目文档路由。 |
| `unity-skills` | 保留薄入口 `.spec/skills/unity-skills/SKILL.md` | 根 `SKILL.md` 是唯一项目 skill；上游模块、references 和 schema 统一读取本地包 `Packages/com.besty.unity-skills/unity-skills~`，不在 `.spec` 复制成第二份资料镜像，也不注册成多个项目子 skill。 |
| `D:\codex-home\skills\planning-with-files` | 保留为长期计划真相源 | 用户已明确“长期计划”指向该 skill。 |
| `D:\codex-home\skills\self-evolving-skills` | 保留为系统 skill 生命周期能力 | `.spec/skills/spec-steward` 只管本仓 `.spec`，不替代系统 skill-lab 流程。 |

当前宿主适配结果见 `.spec/skills/spec-steward`：`.agents/skills`、`.claude/skills`、`.claude/agents` 已作为宿主发现入口收口，`.codex/skills` 不再作为项目 skill 来源。本文件只记录整合证据，不承载执行口径。

## 已按当前指令收口的原待决策项

| 原冲突点 | 当前处理 |
|----------|----------|
| `.spec/skills/spec-steward` 与 `self-evolving-skills` | 不再等待决策：`.spec/skills/spec-steward` 管本项目 `.spec`；`self-evolving-skills` 继续管系统/自有 skill 生命周期。 |
| 上游强 TDD 与项目务实测试策略 | 不再等待决策：项目采用务实 TDD；需要强制测试先行的模块以后直接写入对应模块规范。 |
| 上游 worktree 并行流程与项目禁止擅自 worktree | 不再等待决策：默认禁止自动 worktree，只有用户当轮明确授权才可走 `using-git-worktrees`。 |
| 项目知识库入口 | 不再等待决策：`.spec/knowledge/features/project/` 是 CardLoop 后续项目事实入口；旧来源项目的同名业务知识库没有迁入。 |

## Unity-Technologies/skills 官方候选裁决

审查对象：`https://github.com/Unity-Technologies/skills` 当前 `skills/*/SKILL.md` 正文。该仓库是 Unity 官方 agent skill 集合，但 CardLoop 已有项目唯一 skill 入口 `.spec/skills`；因此不得整包安装到 active skill 路径，也不得让 `.agents/skills`、`.claude/skills` 或官方同名 skill 成为第二套权威。是否吸收只看有效增量：同名、相似、内容更多或来自官方，都不能单独作为合并理由。

| 官方 skill | CardLoop 处理 | 原因 |
|------------|---------------|------|
| `unity-cli` | 已迁入 `.spec/skills/unity-cli`，作为受限项目入口 | 正文覆盖 Unity CLI、Unity Hub、Editor 安装、许可证、项目创建、构建测试和 Unity MCP；本项目原先只有工具职责矩阵，没有实际 `unity-cli` skill。已抽取有效增量为“CLI 入口、版本门禁、help 优先、本机命令能力不得按上游最新版假设”，不复制重复的 UnitySkills / batchmode 流程。 |
| `unity-package-management` | 已迁入 `.spec/skills/unity-package-management`，作为受限项目入口 | 正文指出 Unity CLI 不负责 UPM，包变更应走 `UnityEditor.PackageManager.Client` 且不能默认手改 `Packages/manifest.json`。项目虽已有 UnitySkills 内部 `package` 模块和插件索引，但缺少“用户说装包/UPM”时触发的项目 skill；已吸收为入口与门禁，不复制重复安装脚本。 |
| `ui`、`ui-ugui`、`ui-uitk`、`ui-imgui` | 有效增量已并入 `.spec/skills/unity-ui-development`；不单独注册 | 官方正文的有效增量不是 API 清单，而是 UI 系统路由、IMGUI 只作 legacy 维护、Figma 自动导入不可承诺、视觉 UI 不自动等于新增脚本、现有层级优先局部修改。这些已写入现有 UI 唯一入口；Canvas / UXML / USS 的具体执行仍走 UnitySkills `ui` / `uitoolkit` 模块。 |
| `initialize-ai-navigation` | 暂不注册；NavMesh 任务出现时作为参考 | 正文绑定 Unity AI Navigation、NavMesh Surface / Agent / Obstacle；项目虽已安装 `com.unity.ai.navigation`，但当前阶段没有正式 NavMesh 玩法目标。 |
| `build-live-game` | 暂不注册 | 正文覆盖 Unity Gaming Services、认证、云存档、云代码、远程配置、成就、排行榜、Battle Pass、Matchmaker 等上线后运营后端；当前阶段不得提前接入 live service。 |
| `implement-in-app-purchases` | 暂不注册 | 正文覆盖 Unity IAP、商品目录、两段式购买、收据校验、订阅、D2C 和从第三方内购迁移；当前没有商业化接入目标。 |
| `levelplay-unity-integration` | 暂不注册 | 正文覆盖 LevelPlay 广告聚合、激励视频、插屏、横幅、iOS/Android 隐私与 SDK 迁移；当前没有广告变现目标。 |
| `setup-multiplayer-services` | 不作为联机实现入口；只保留为 Unity 外围服务候选 | 官方正文的 Multiplayer 指 Unity Gaming Services 的 Sessions / Lobby / Relay / Matchmaker / Dedicated Game Server 等服务链，不是 Mirror 这类游戏内网络库。CardLoop 联机后端 ADR 已选 Mirror；因此它不能接管联机实现，只可能在未来需要 Unity 账号、匹配、房间或 Relay 服务时重新评估。 |
| `setup-vivox-voice-chat` | 暂不注册；未来语音/文字聊天需求再评估 | 正文绑定 Unity Vivox、Unity Authentication、频道、事件订阅、麦克风权限和语音设置；当前没有聊天服务目标。 |
| `localization` | 候选参考；不主动安装 Unity Localization | 正文覆盖 `com.unity.localization`、String / Asset Tables、CJK 字体、TMP 和 Addressables。当前项目未进入多语言产品目标，也不能为“中文显示”直接安装 Localization；只把通用 CJK / TMP 风险由 `.spec/skills/unity-ui-development` 承接。 |
| `optimize-audio` | 候选参考；不替代 BroAudio / 项目音频入口 | 正文聚焦 Unity 6 音频导入、压缩、Load Type、平台设置和 Mixer 成本；本项目已登记 BroAudio，音频任务先读项目插件入口。 |
| `optimize-text-mesh-pro` | 有效增量已并入 `.spec/skills/unity-ui-development`；不单独注册 | 正文对 CardLoop 有价值的是中文/CJK 字体和运行时性能门禁：静态主字体 + 动态 fallback、`Clear Dynamic Data On Build`、AutoSize 禁用时机、World Space 文本选择、Canvas rebuild 隔离、Material Preset 和 TMP Sprite Asset 源纹理类型。这些属于 UI/TMP 子门禁，不需要单独 active skill。 |
| `optimize-web` | 暂不注册；WebGL/WebGPU 发布目标出现时再评估 | 正文覆盖 Unity WebGL/WebGPU 包体、首载、浏览器性能、KTX、shader variant 和服务端压缩；当前没有 Web 发布目标。 |
| `physics-3d-collision` | 候选参考；不作为默认物理排障入口 | 正文是 3D PhysX 碰撞/Trigger/Raycast 排障清单；CardLoop 当前不是 3D 物理驱动项目，且已有 UnitySkills 物理模块。 |
| `sprite-editor` | 安全增量已并入 `.spec/knowledge/standards/unity-serialization-safety.md`；工作流暂不注册 | 正文真正有价值的是“Sprite rect / border / pivot / outline / slicing 归 importer 和 `ISpriteEditorDataProvider`，不能手改 `.meta`，修改前必须做 capability check”。这个是资源写入安全红线，已放入序列化安全规范；自动切片脚本等到真实素材管线任务再评估。 |
| `shader-graph-create-custom-node` | 候选参考；按需并入 Shader Graph 任务 | 正文很窄，只指导把 HLSL 函数包装成 Shader Graph 自定义节点；项目已有 UnitySkills 的 ShaderGraph 设计模块。 |
| `urp-postprocessing` | 候选参考；不单独注册 | 正文覆盖 URP Volume、Bloom、Tonemapping、DOF、Vignette 等后处理配置；项目已有 URP / postprocess / volume 模块，具体视觉任务再吸收。 |
| `validate-urp-render-graph-renderer-feature` | 候选审查 skill；出现自定义 Render Graph RendererFeature 时再迁入 | 正文是 Unity 6+ URP Render Graph RendererFeature 审查清单；当前没有对应代码，不提前注册。 |
| `new-unity-project` | 排除 | 正文用于从零创建新 Unity 项目、安装 Editor、初始化源控和包；CardLoop 已是现有项目。 |

执行口径：官方 skill 正文可作为上游候选资料读取；只有当用户目标实际命中对应领域，并且能证明该正文有本项目现有 `.spec/skills` 未覆盖的有效增量时，才按 `spec-steward` 合并或迁入。相似但重复的内容只保留为上游参考，不复制进项目。迁入后必须以 `.spec/skills/<name>/SKILL.md` 为唯一项目入口，并同步索引、冲突矩阵和校验。

裁决摘要：`unity-cli` 与 `unity-package-management` 已迁入为受限项目入口；UI / TMP 的有效增量已并入 `.spec/skills/unity-ui-development`；Sprite Editor 的安全增量已并入 `.spec/knowledge/standards/unity-serialization-safety.md`。其它官方 skill 仅作上游候选，只有出现真实项目缺口才继续抽取有效增量。

