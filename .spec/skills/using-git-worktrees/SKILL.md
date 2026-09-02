---
name: using-git-worktrees
description: 当用户明确要求或授权使用 git worktree 隔离开发时使用；默认只做检测和说明，未经当轮许可不得创建、切换或删除 worktree。
---

# Using Git Worktrees（受控 worktree 流程）

## 默认策略

CardLoop 项目默认禁止擅自创建、切换、重建或删除 worktree。外部模板里的 worktree 隔离思想只作为可选能力，不能作为默认动作。

## 允许使用的条件

只有用户当轮明确说可以使用 worktree，或明确要求隔离工作区时，才进入创建流程。

## 使用前检查

1. 说明目标：为什么需要隔离。
2. 说明影响：会创建哪个目录、哪个分支名、是否会改 `.gitignore`。
3. 确认可逆性：如何清理，哪些操作有丢失风险。
4. 等用户确认。

## 禁止

- 不因“并行更快”自动创建 worktree。
- 不自动切换分支。
- 不作为正式入口保留 worktree 或分支，除非用户确认丢弃或已成功合并。
- 不把 worktree 流程写成项目默认开发流程。


