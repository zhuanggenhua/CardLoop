---
name: test-driven-development
description: 为 CardLoop 的核心逻辑、新玩法切片、已复现 bug 和高风险公开契约选择 TDD、回归测试或 Unity 验证方式；通用 red-green 流程以系统 tdd skill 为准。
---

# CardLoop TDD 适配层

通用 TDD 流程的唯一真相源是 `D:\codex-home\skills\tdd\SKILL.md`。CardLoop 的测试/TDD 适用政策唯一正文是 [`.spec/knowledge/standards/testing.md`](../../knowledge/standards/testing.md)。本 skill 只负责把具体任务导向正确验证方式，不重复政策正文。

## 执行顺序

1. 先读 [`.spec/knowledge/standards/testing.md`](../../knowledge/standards/testing.md)，按其 TDD 适用范围、验证分层、Bug 验收和完成声明口径分类本轮任务。
2. 如果该标准判定为严格 TDD，继续读取 `D:\codex-home\skills\tdd\SKILL.md`，按 red-green-refactor 执行，并保留 RED 证据。
3. 如果不进入严格 TDD，按 `testing.md` 选择静态校验、架构守卫、smoke、EditMode / PlayMode、Unity 状态、日志、截图或资源检查，并说明为什么不是业务 TDD。
4. 汇报时写清验证类型：严格 TDD、回归测试、公开契约测试、架构守卫、smoke、场景验收或人工证据；不得混称。

## 禁止

- 不在本 skill 里重新定义 CardLoop 的 TDD 适用政策；政策变化先改 `testing.md`。
- 不把结构测试、反射检查、Stub 自证或 smoke 冒充业务 TDD。
- 不把实现后补的测试倒称为 TDD。
