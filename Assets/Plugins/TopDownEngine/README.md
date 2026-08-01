# TopDownEngine Imported Subset

这里存放已经正式导入当前 Unity 工程的 `TopDown Engine` 子集。

当前策略：

- 只导入可直接编进当前项目、且对俯视角样板有明确价值的运行时闭包
- 保留原始目录语义，尽量整段复制，不在导入阶段做项目式重写
- 明确剔除当前不需要的可选集成层，例如 `ScriptsCinemachine`、`ScriptsPostProcessing`
- 本目录随带的 demo 图片、Tile、Prefab、场景、音频和 UI 资源只作为 TopDownEngine 参考资源或插件依赖资源保留，不是 FantasyWord 正式美术来源；正式角色、怪物、地图、道具和换装素材以 MiniFantasy 为基线。

同步入口：

- `scripts/Sync-TopDownRuntimeSubset.ps1`

原始镜像来源：

- `ReferenceSources/TopDownEngine`
