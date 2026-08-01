---
name: subagent-driven-development
description: 需要把已拆好的任务交给子 agent 实现、分析、修复、测试或审查时使用；CardLoop 强制继承当前主 agent 的模型与推理强度，且子 agent 不得再派生。
---

# Subagent Driven Development（子 agent 执行流程）

## 前提

- 任务已经用 `task-breakdown` 拆清楚，或范围足够小。
- 文件集边界明确。
- 子 agent 的使用本身有价值：隔离上下文、并行分析、独立审查或长任务分担。

## 强制配置

派发与改代码直接相关的任务时，必须在提示里写明：

```text
模型配置：继承当前主 agent 的模型与推理强度。
只有工具无法继承且不显式指定会导致降级时，才指定与当前主 agent 等效的配置。
不得降级模型，不得改用其他更弱配置。
不得再派生子 agent。
```

## 派发内容

每个子 agent 只拿：

- 任务目标。
- 可改和不可改范围。
- 必读规范路径。
- 真相来源。
- 验收标准。
- 输出格式。

不要把隐藏结论、预期答案或未验证猜测塞给子 agent。

## 工作区限制

- 不自动创建、切换、删除分支、tag、worktree。
- 如需 worktree，必须先取得用户当轮明确许可，并转用 `using-git-worktrees`。
- 子 agent 产出要由主 agent 核实 diff 和验证证据，不能直接相信“已完成”。

## 收口

- 子 agent 返回后，主 agent 读取改动、验证证据和 known gaps。
- 高风险交付再触发 reviewer。
- 不通过则退回具体问题，不让子 agent 盲目重试同一方案三次以上。

