# CardLoop

Unity 卡牌生存项目。项目规范、知识库、任务卡和框架分层统一在 `.spec/` 下维护。

## 入口

- AI / 协作者入口：[`AGENTS.md`](AGENTS.md)
- `.spec` 中心文档：[`.spec/AGENTS.md`](.spec/AGENTS.md)
- 知识导航：[`.spec/knowledge/README.md`](.spec/knowledge/README.md)
- 硬红线：[`.spec/rules/system.md`](.spec/rules/system.md)
- 框架分层：[`.spec/knowledge/standards/framework-layering.md`](.spec/knowledge/standards/framework-layering.md)

## 当前口径

- YokiFrame 是底座层，只放无游戏业务语义的基础能力。
- GameCore 是通用游戏解决方案层，提供存档、输入、资源、UI、事件、对象池等可复用能力和扩展点。
- Gameplay 是 CardLoop 业务层，承载卡牌、牌桌、剧本、任务、战斗、开包和项目内容。
- GameCore 需要特化时，默认做 GameCore 扩展点 + Gameplay 实现 / 数据 / 策略；只有无游戏语义的原语才下沉到 YokiFrame。
- 代码重构必须等文档分层、索引和 `.spec` 校验收口后再进行。

历史计划、进度、架构 HTML 和旧来源项目引入证据不再作为当前入口；需要追溯时以 git 历史和当前 `.spec` 事实为准。
