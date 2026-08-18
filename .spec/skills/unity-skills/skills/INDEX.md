---
name: unity-skills-index
description: 'Unity Skills 索引入口。用于按功能路由到 GameObject、Scene、Asset、Script、Prefab、Material、Test 等子技能。'
---

# Unity Skills - Module Index

Module docs. Start with [../SKILL.md](../SKILL.md) for mode switching and schema-first rules.

> **Multi-instance**: For version-specific projects, call `unity_skills.set_unity_version(...)` first.
> **Schema-first**: Use `GET /skills/schema` or `unity_skills.get_skill_schema()` for exact signatures. Load module docs for workflow guidance and guardrails.

## Modules

> **Mode legend** (v1.9.0+, caller-facing — describes what the caller can do, not the C# attribute):
> - `SA` — module skills mostly run directly in **all three modes** (Approval / Auto / Bypass) without a grant.
> - `FA` — module skills mostly require **user grant** under Approval (single-shot one-step execution); under Auto / Bypass they run directly with audit only.
> - `Mixed` — module is split between SA and FA; check per-skill `mode` returned by `GET /skills` before calling.
> - Suffix `*` — module contains auto-forbidden skills (Delete / Play Mode / Domain Reload / high-risk). These return `MODE_FORBIDDEN` under Approval and Auto; only **Bypass** runs them, **or** the user can permanently allow them via the Allowlist. Never attempt grant for them.
>
> Labels are guidance only; the per-skill `mode` field on `GET /skills` is authoritative.

| Module | Mode | Description | Batch Support |
|--------|:----:|-------------|---------------|
| [gameobject](./gameobject/MODULE.md) | FA* | Object create/move/parent | Yes |
| [component](./component/MODULE.md) | Mixed* | Component add/remove/configure | Yes |
| [material](./material/MODULE.md) | FA | Material property edits | Yes |
| [light](./light/MODULE.md) | FA | Light create/configure | Yes |
| [prefab](./prefab/MODULE.md) | FA | Prefab create/apply/spawn | Yes |
| [asset](./asset/MODULE.md) | SA* | Asset refresh/find/info | Yes |
| [batch](./batch/MODULE.md) | SA | Batch and async jobs | Built-in |
| [ui](./ui/MODULE.md) | FA | UGUI Canvas/UI creation | Yes |
| [uitoolkit](./uitoolkit/MODULE.md) | Mixed* | UXML/USS/UIDocument | No |
| [script](./script/MODULE.md) | SA* | Script create/read/update | Yes |
| [scene](./scene/MODULE.md) | SA* | Scene load/save/query | No |
| [editor](./editor/MODULE.md) | SA* | Play/select/undo/redo | No |
| [animator](./animator/MODULE.md) | FA | Animator controllers | No |
| [shader](./shader/MODULE.md) | Mixed* | Shader create/list | No |
| [shadergraph](./shadergraph/MODULE.md) | Mixed* | Shader Graph create/inspect/blackboard edit/constrained node editing | No |
| [graphics](./graphics/MODULE.md) | Mixed | GraphicsSettings / QualitySettings / SRP assets | No |
| [volume](./volume/MODULE.md) | Mixed* | Volume / VolumeProfile / VolumeComponent | No |
| [postprocess](./postprocess/MODULE.md) | FA* | Modern URP/HDRP post-processing | No |
| [urp](./urp/MODULE.md) | Mixed* | URP asset / renderer / renderer features | No |
| [decal](./decal/MODULE.md) | Mixed* | URP Decal Projector workflow | Yes |
| [console](./console/MODULE.md) | SA | Log capture/filter | No |
| [validation](./validation/MODULE.md) | SA* | Broken reference checks | No |
| [importer](./importer/MODULE.md) | Mixed | Texture/audio/model import | Yes |
| [cinemachine](./cinemachine/MODULE.md) | FA* | VCam operations | No |
| [probuilder](./probuilder/MODULE.md) | FA* | ProBuilder mesh edits | No |
| [xr](./xr/MODULE.md) | FA | XRI setup | No |
| [terrain](./terrain/MODULE.md) | FA | Terrain create/paint | No |
| [physics](./physics/MODULE.md) | Mixed | Raycast/overlap/gravity | No |
| [navmesh](./navmesh/MODULE.md) | Mixed* | NavMesh bake/query | No |
| [timeline](./timeline/MODULE.md) | FA* | Timeline tracks/clips | No |
| [workflow](./workflow/MODULE.md) | SA* | Task snapshots/undo | No |
| [cleaner](./cleaner/MODULE.md) | SA* | Unused/duplicate assets | No |
| [smart](./smart/MODULE.md) | FA* | Query/layout/auto-bind | No |
| [perception](./perception/MODULE.md) | SA | Scene/project analysis | No |
| [camera](./camera/MODULE.md) | FA | Scene View camera | No |
| [event](./event/MODULE.md) | Mixed* | UnityEvent wiring | No |
| [package](./package/MODULE.md) | Mixed* | UPM install/query | No |
| [project](./project/MODULE.md) | SA | Project info/settings | No |
| [profiler](./profiler/MODULE.md) | SA | Perf statistics | No |
| [optimization](./optimization/MODULE.md) | Mixed | Asset optimization | No |
| [sample](./sample/MODULE.md) | Mixed* | Demo/test skills | No |
| [debug](./debug/MODULE.md) | SA | Compile/system diagnostics | No |
| [test](./test/MODULE.md) | Mixed | Unity Test Runner | No |
| [bookmark](./bookmark/MODULE.md) | SA | Scene View bookmarks | No |
| [history](./history/MODULE.md) | SA | Undo/redo history | No |
| [scriptableobject](./scriptableobject/MODULE.md) | Mixed* | ScriptableObject assets | No |
| [netcode](./netcode/MODULE.md) | Mixed* | Netcode for GameObjects setup, prefabs, lifecycle, host/server/client | Yes |
| [yooasset](./yooasset/MODULE.md) | Mixed* | YooAsset hot-update: build bundles, Collector CRUD, BuildReport asset/dependency analysis, PlayMode runtime validation, Reporter/Debugger/AssetArtScanner tools | Yes |
| [dotween](./dotween/MODULE.md) | Mixed* | DOTween Pro DOTweenAnimation editor-time configuration (add/batch/stagger/tune) | Yes |

## Advisory Design Modules

These modules provide design guidance only.

| Module | Description |
|--------|-------------|
| [project-scout](./project-scout/MODULE.md) | Inspect existing project |
| [architecture](./architecture/MODULE.md) | Plan system boundaries |
| [adr](./adr/MODULE.md) | Record tradeoffs |
| [performance](./performance/MODULE.md) | Review hot paths |
| [asmdef](./asmdef/MODULE.md) | Plan asmdef deps |
| [blueprints](./blueprints/MODULE.md) | Small-game blueprints |
| [script-roles](./script-roles/MODULE.md) | Assign class roles |
| [scene-contracts](./scene-contracts/MODULE.md) | Define scene wiring |
| [testability](./testability/MODULE.md) | Extract testable logic |
| [patterns](./patterns/MODULE.md) | Choose patterns |
| [async](./async/MODULE.md) | Choose async model |
| [inspector](./inspector/MODULE.md) | Design authoring UX |
| [scriptdesign](./scriptdesign/MODULE.md) | Review script structure |
| [netcode-design](./netcode-design/MODULE.md) | Netcode source-anchored rules (lifecycle/ownership/RPC/variables/spawn/scene/transport/pitfalls) |
| [yooasset-design](./yooasset-design/MODULE.md) | YooAsset v2.3.18 source-anchored rules (init/default-package shortcuts/playmode/handles/loading/update/filesystem/build/pitfalls) |
| [addressables-design](./addressables-design/MODULE.md) | Addressables dual-version (1.22.3 Unity 2022 / 2.9.1 Unity 6) source-anchored rules (init/handles/loading/scene/update/download/assetref/pitfalls) with migration table |
| [unitask-design](./unitask-design/MODULE.md) | UniTask 2.5.10 source-anchored rules (basics/playerloop/cancellation/composition/conversion/asyncenumerable/triggers/pitfalls) |
| [dotween-design](./dotween-design/MODULE.md) | DOTween 1.3.015 source-anchored rules (basics/tween/sequence/shortcuts/ease/lifetime/integration/pitfalls) |
| [shadergraph-design](./shadergraph-design/MODULE.md) | ShaderGraph dual-version source-anchored rules (versions/node subset/recipes/pitfalls/review) |
| [yaml-editing](./yaml-editing/MODULE.md) | Safe hand-edit rules for serialized YAML (.unity/.prefab/.asset/.meta/ProjectSettings) when REST cannot reach — reference/fileID repair, .meta/GUID safety, ProjectSettings patch, merge conflict |

## Batch-First Rule

When a task touches `2+` objects in Auto / Bypass mode (or after a successful grant under Approval), prefer `*_batch` skills over repeated single-item calls.

## Skill Naming Convention

Skills follow `<module>_<action>` or `<module>_<action>_batch`.
Use schema to verify the exact prefix list.
Special: `scene_analyze`, `hierarchy_describe`, `project_stack_detect` → `perception`; `job_*` → `batch`.
If a skill name does not match a valid prefix or a schema result, do not invent it.

