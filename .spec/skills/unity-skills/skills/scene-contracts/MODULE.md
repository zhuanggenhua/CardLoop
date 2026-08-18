---
name: unity-scene-contracts
description: 'Unity 场景契约入口。用于必备对象、组件依赖、bootstrap、引用 wiring 和场景组成说明。'
---

# Unity Scene Contracts

Use this skill when scene setup needs to be explicit instead of relying on hidden runtime lookups.

## Define

- Required root objects
- Required components on each root
- Which references are assigned in Inspector
- Which objects act as bootstrap/installers
- Which objects are runtime-spawned
- Which assumptions should be validated early

## Output Format

- Scene object contract
- Bootstrap sequence
- Inspector wiring rules
- Validation rules
- Hidden dependency risks

## Guardrails

> **Mode**: Documentation only — no REST skills to gate; load freely under any operating mode (Approval / Auto / Bypass).

- Prefer explicit scene wiring over chains of runtime `Find`.
- Keep bootstrap objects small and focused.
