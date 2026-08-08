---
name: data-serialization
description: CardLoop 数据入口速查：记录项目自己的 DatabaseRegistry 稳定引用、ScriptableObject 作者源、SerializableDictionary 和目标冷却存档边界。
metadata:
  type: ai-quick-reference
  role: project-reference
  source: project-source + package-entry
  status: 已交付
  verified_at: 2026-08-04
  update_triggers: database-schema-change, persistence-contract-change, serializable-dictionary-change, content-id-change
---

# 数据与序列化项目入口

## 用途

区分作者配置资产、运行时对象、稳定引用和可恢复存档状态，避免把 Unity 对象引用、资源地址、GUID 和运行时实例号混成多套内容身份。

## 官方 / 包入口

`SerializableDictionary` 当前只确认了本地包入口和项目字段使用，没有找到可以替代项目源码契约的完整官方手册：

- [SerializableDictionary package.json](../../../../Assets/Plugins/azixMcAze.SerializableDictionary/package.json)
- 项目数据库和存档入口以本卡列出的源码为准。

本卡不是 `SerializableDictionary` API 手册；它只说明该容器在 CardLoop 中承担的字段存储职责。

## 项目正式入口

| 现实需求 | 项目入口 | 所有权 |
|---|---|---|
| 内容资产基类 | `GameCore.DatabaseEntry` | ScriptableObject 内容作者源。 |
| 数据库注册与查找 | `GameCore.DatabaseRegistry` | GUID 到数据库资产的注册和转换。 |
| 运行时稳定引用 | `GameCore.DatabaseEntryReference<T>` | 运行时只依赖 GUID，不依赖对象引用。 |
| 编辑器注册 | `DatabaseRegistry.Editor.cs` 的 `Register`、`Unregister`、`SetEntries` | 编辑器维护数据库集合。 |
| 可序列化字典 | `azixMcAze.SerializableDictionary<TKey,TValue>` | Unity 字段容器，不是数据库或 ID 系统。 |
| 目标独立冷却 | `PerTargetCooldown<TTarget>` / `PerTargetCooldownDataBlock<TTarget>` | 实例运行时状态及其存档块。 |
| 角色正式能力码 | `CharacterSheet` 的能力码查询方法 | EX-GAS 能力编号由项目内容资产保存，能力实现回到 EX-GAS。 |

## 生命周期与身份

`DatabaseRegistry` 的 key 是数据库资产 GUID。编辑器下 `DatabaseEntryReference<T>` 将对象引用同步为 GUID；运行时通过 `DatabaseRegistry.LoadFromReference` 或 `GUIDToDatabaseEntry` 找回资产。

数据库资产必须先登记，才能创建稳定引用。对象删除、GUID 转换和缺失引用清理由注册表编辑器入口维护，业务不能随意写另一份字典。

`CharacterSheet` 用 `SerializableDictionary<int, int>` 保存“正式 EX-GAS 能力编号 → 解锁等级”，通过 `GetAvailableFormalGasAbilitiesAtLevel` 和 `GetFormalGasAbilitiesUnlockedAtLevel` 返回能力码。Gameplay 不应另建能力名称表或技能实例表。

`PerTargetCooldown<TTarget>` 只保存目标到剩余秒数的临时映射。持有者调用 `Update` 推进时间，存档时调用 `CreateDataBlock`，读档时调用 `LoadDataBlock`；存档使用 `PersistableReference<TTarget>`，不是直接保存 Unity 对象引用。

## 最小真实示例

数据库稳定引用：

```csharp
DatabaseEntryReference<CharacterSheet> reference =
    database.CreateReference(characterSheet);

CharacterSheet loadedSheet = database.LoadFromReference(reference);
```

查询角色在某等级可用的正式 EX-GAS 能力码：

```csharp
int[] abilityCodes = characterSheet
    .GetAvailableFormalGasAbilitiesAtLevel(level);
```

目标冷却和存档块：

```csharp
var cooldown = new PerTargetCooldown<CharacterActor>();
cooldown.StartCooldown(target, 1.5f);
cooldown.Update(deltaTime);
PerTargetCooldownDataBlock<CharacterActor> saved = cooldown.CreateDataBlock();
```

## 常见错误

- 从未登记资产创建稳定引用；`TryCreateReference` 会失败，`CreateReference` 会抛异常。
- 把 Unity 对象、文件路径、YooAsset 地址和数据库 GUID 保存成多个并列内容 ID。
- 直接把 `SerializableDictionary` 当作运行时数据库或作者源。
- 把 `PerTargetCooldown` 的内存对象 key 直接当作存档格式。
- 用 `PerTargetCooldown` 代替 EX-GAS Ability / GameplayEffect 冷却或标签条件。
- 直接读取 `CharacterSheet` 私有字典，而不是使用公开能力码查询方法。

## 禁止做法

- 不为同一内容再建 string name、整数 ID 或 Mod key 作为作者手工维护的第二身份。
- 不在 Gameplay 侧自建 EX-GAS Ability、Attribute、GameplayTag 表来复制正式入口。
- 不把只为 Inspector 方便的对象引用当成运行时持久化真相。
- 不让序列化容器反过来拥有内容注册、资源加载或生成代码职责。

## 源码证据

- 数据库：[`DatabaseEntry.cs`](../../../../Assets/Scripts/GameCore/Runtime/Database/DatabaseEntry.cs)、[`DatabaseRegistry.cs`](../../../../Assets/Scripts/GameCore/Runtime/Database/DatabaseRegistry.cs)、[`DatabaseRegistry.Editor.cs`](../../../../Assets/Scripts/GameCore/Runtime/Database/DatabaseRegistry.Editor.cs)。
- 稳定引用：[`DatabaseEntryReference.cs`](../../../../Assets/Scripts/GameCore/Runtime/Database/DatabaseEntryReference.cs)。
- 角色能力码：[`CharacterSheet.cs`](../../../../Assets/Scripts/GameCore/Runtime/Database/Characters/CharacterSheet.cs) 的 `m_formalGasAbilitiesPerLevel`、`GetAvailableFormalGasAbilitiesAtLevel`、`GetFormalGasAbilitiesUnlockedAtLevel`。
- 目标冷却：[`PerTargetCooldown.cs`](../../../../Assets/Scripts/GameCore/Runtime/Combat/PerTargetCooldown.cs) 的 `StartCooldown`、`Update`、`CreateDataBlock`、`LoadDataBlock`。
