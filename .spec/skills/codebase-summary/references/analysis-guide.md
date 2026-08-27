# Architecture Analysis Guide（架构分析指南）

仓库级架构分析时读取本指南；窄问题只取相关章节。

## 内容

- 仓库定位
- 入口点和接口
- 模块与数据流
- 运行、质量和验证
- 图示选择

## 仓库定位

先读取仓库指引和既有文档。检查 manifest 与构建配置，确认：

- 仓库形态：单包、monorepo、服务、库、CLI、Unity 工程或应用。
- 主要语言、运行时、框架和包边界。
- 文档记录的开发、测试、构建和发布命令。
- 生成物、第三方源码、fixture、构建输出和其他应排除的目录。

manifest 和可执行配置是强证据。README 或设计文档只是待验证说法；当代码或配置可能不同步时，以当前代码和项目规范为准。

## 入口点和接口

从配置好的入口开始追踪，而不是只看惯例文件名。

可检查：

- manifest 的 `main`、`bin`、scripts、workspace 或插件声明。
- 应用启动函数和可执行 package。
- HTTP 路由、GraphQL schema、RPC 服务、webhook 或事件消费者。
- CLI 命令注册和参数解析。
- 导出的库接口。
- UI 路由和根组件。
- worker、定时任务、migration 和 serverless handler。
- 容器入口和部署命令。
- Unity 项目的 scene、asmdef、package、Editor 菜单、生成器入口和运行时 MonoBehaviour / ScriptableObject 生命周期入口。

对每个接口记录：谁会调用它、如何触发、可见鉴权或信任边界、进入的第一个内部模块。

## 模块与数据流

模块边界要从 package 结构、公开导出、import 方向、依赖注入和测试中推断。只有证据支持时，才分类为：

- 展示 / 交付层。
- 应用编排层。
- 领域 / 业务逻辑层。
- 持久化和数据模型。
- 外部适配和集成。
- 配置与共享基础设施。
- Unity 资源、序列化资产、编辑器工具、运行时系统和第三方插件边界。

追踪一到两条代表性路径，例如：

- request -> handler -> service -> repository -> database。
- event -> consumer -> processor -> publisher。
- UI action -> state -> API -> rendered result。
- Unity scene / prefab -> MonoBehaviour lifecycle -> domain service -> ScriptableObject / asset / event。

记录依赖方向和重要状态转换。除非仓库显式命名该模式，否则把架构模式标为推断。

## 运行、质量和验证

只有在能回答问题或匹配所选深度时，才检查这些区域：

- 数据库 schema 和 migration。
- cache、queue、对象存储和第三方服务。
- 鉴权、授权和信任边界。
- 环境变量名称和配置来源，不展示具体值。
- 单元、集成、端到端和契约测试布局。
- 构建、打包、发布、观测和部署配置。
- Unity Package Resolve、脚本编译、PlayMode/EditMode 测试、资源导入和 Console 错误来源。

测试经常是公开行为的清晰证据。实现和测试都重要时，两边都引用。

## 图示选择

使用能讲清结构的最少图：

- **高层图**：用户、宿主、可运行单元、数据存储、资源系统和外部服务。
- **执行流图**：一条代表性请求、命令、事件、UI 交互或 Unity 生命周期路径。
- **模块依赖图**：主要内部边界和依赖方向。
- **类图**：领域对象、接口、组合/继承关系。
- **状态图**：玩法状态、任务状态、资源生命周期或异步流程状态。

如果表格比图更清楚，就不要强行画图。图中标签必须使用仓库里的真实名字，并在图旁展示来源证据。
