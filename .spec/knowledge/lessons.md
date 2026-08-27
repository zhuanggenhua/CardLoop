---
name: lessons
description: CardLoop 反复经验升级池，记录尚未升级为正式规范或 skill 的经验。
metadata:
  type: doc
  status: 已交付
---

# Lessons（经验升级池）

本文件只记录 CardLoop 中反复出现、且还没有升级为正式规范或 skill 的经验。

- Unity 自动化卡死后继续压请求、改用第二套工具或尝试自动关闭 Editor 已多次导致验证事故；已升格到 [`standards/testing.md`](standards/testing.md) 与 [`features/ai-quick/unity-automation-tools.md`](features/ai-quick/unity-automation-tools.md)，并由 `.spec/tools/unity-verify.mjs` 与 `.spec/tools/unityskills-ensure.mjs` 执行。
- 手写 / 脚本改 Unity 序列化文件时，PowerShell `$1` 等替换占位符会被宿主提前展开，曾导致 `.meta` YAML 损坏；已升格到 [`standards/unity-serialization-safety.md`](standards/unity-serialization-safety.md)，并由 `.spec/tools/unity-yaml-guard.mjs` 执行。
