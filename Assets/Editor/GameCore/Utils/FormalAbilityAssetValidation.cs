using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using UnityEditor;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 正式 EX-GAS 能力资产审计工具。
    /// 它检查能力 Prefab、图标、Timeline 和 Cue 是否来自正式数据链路，不回退旧能力表。
    /// </summary>
    public static class FormalAbilityAssetValidation
    {
        /// <summary>
        /// 编辑器 UI 可直接展示的一条验证问题。
        /// </summary>
        [Serializable]
        public sealed class ValidationIssue
        {
            public string Message { get; set; } = string.Empty;
            public MessageType Severity { get; set; } = MessageType.Error;
        }

        /// <summary>
        /// 批量审计输出结果。
        /// 用于自动化验证写 JSON 和编辑器窗口展示。
        /// </summary>
        [Serializable]
        public sealed class AuditResult
        {
            public bool Success;
            public string Message = string.Empty;
            public AuditIssue[] Issues = Array.Empty<AuditIssue>();
        }

        /// <summary>
        /// 单条能力审计问题。
        /// 字段保持字符串化，方便 Unity JsonUtility 和外部脚本读取。
        /// </summary>
        [Serializable]
        public sealed class AuditIssue
        {
            public string AbilityPath = string.Empty;
            public string AbilityName = string.Empty;
            public string AbilityType = string.Empty;
            public string Issue = string.Empty;
            public string Severity = string.Empty;
        }

        public static List<ValidationIssue> CollectIssues(int formalGasAbilityCode)
        {
            List<ValidationIssue> issues = new();
            if (formalGasAbilityCode <= 0)
            {
                issues.Add(CreateIssue("EX-GAS Ability Code 必须为正数。"));
                return issues;
            }

            CollectFormalGasAbilityRuntimeConfigIssues(formalGasAbilityCode, issues);
            return issues;
        }

        private static void CollectFormalGasAbilityRuntimeConfigIssues(
            int formalGasAbilityCode,
            List<ValidationIssue> issues)
        {
            if (!FormalGasAbilityRuntimeConfigResolver.TryResolveRuntimeConfig(
                    formalGasAbilityCode,
                    out FormalGasAbilityRuntimeConfig config))
            {
                issues.Add(CreateIssue(
                    $"EX-GAS Ability {formalGasAbilityCode} 缺少项目侧运行配置。正式 Prefab、图标和挂载根节点必须来自 exgas.abilityGameCore，不得回退项目侧旧能力表。"));
                return;
            }

            CollectRuntimeResourceIdentityIssues(formalGasAbilityCode, config, issues);

            if (!config.TryLoadPrefab(out GameObject prefab) || prefab == null)
            {
                issues.Add(CreateIssue(
                    $"EX-GAS Ability {formalGasAbilityCode} 缺少可加载的正式 Ability Prefab。运行时拒绝回退项目侧旧能力表 prefab。"));
                return;
            }

            AbilityBase abilityBase = prefab.GetComponent<AbilityBase>();
            if (abilityBase == null)
            {
                issues.Add(CreateIssue(
                    $"EX-GAS Ability {formalGasAbilityCode} 的正式 Ability Prefab 根节点缺少 {nameof(AbilityBase)} 组件。"));
            }

            if (formalGasAbilityCode == GAS.Runtime.XAbility.ABILITY_Attack)
            {
                if (!config.TryLoadIcon(out Sprite icon) || icon == null)
                {
                    issues.Add(CreateIssue(
                        $"EX-GAS Ability {formalGasAbilityCode} 缺少可加载的正式图标。基础攻击图标必须来自 exgas.abilityGameCore.IconPath/IconGuid，不得回退已删除的旧能力表图标字段。"));
                }

                CollectBasicAttackCueIssues(formalGasAbilityCode, issues);
            }
        }

        private static void CollectRuntimeResourceIdentityIssues(
            int formalGasAbilityCode,
            FormalGasAbilityRuntimeConfig config,
            List<ValidationIssue> issues)
        {
            if (FormalGasAbilityResourceLoader.IsEditorAssetPath(config.PrefabPath))
            {
                issues.Add(CreateWarning(
                    $"EX-GAS Ability {formalGasAbilityCode} 的 PrefabPath 仍是编辑器项目路径：{config.PrefabPath}。它只能作为编辑器证据，正式运行时应改为 GameCore 数据库 PrefabReference GUID 或 ResourceSystem / YooAsset 地址。"));
            }
            else if (string.IsNullOrWhiteSpace(config.PrefabPath) &&
                     ResolveDatabaseEntry<PrefabReference>(config.PrefabGuid) == null)
            {
                issues.Add(CreateWarning(
                    $"EX-GAS Ability {formalGasAbilityCode} 缺少正式 Prefab 资源引用。PrefabGuid 必须指向 DatabaseRegistry 中的 PrefabReference，或 PrefabPath 必须是玩家构建可解析的资源地址。"));
            }

            if (!string.IsNullOrWhiteSpace(config.IconPath) &&
                FormalGasAbilityResourceLoader.IsEditorAssetPath(config.IconPath))
            {
                issues.Add(CreateWarning(
                    $"EX-GAS Ability {formalGasAbilityCode} 的 IconPath 仍是编辑器项目路径：{config.IconPath}。图标正式运行时应改为 GameCore 数据库 SpriteReference GUID 或 ResourceSystem / YooAsset 地址。"));
            }
            else if (string.IsNullOrWhiteSpace(config.IconPath) &&
                     !string.IsNullOrWhiteSpace(config.IconGuid) &&
                     ResolveDatabaseEntry<SpriteReference>(config.IconGuid) == null)
            {
                issues.Add(CreateWarning(
                    $"EX-GAS Ability {formalGasAbilityCode} 的 IconGuid 未指向 DatabaseRegistry 中的 SpriteReference。"));
            }
        }

        private static void CollectBasicAttackCueIssues(
            int formalGasAbilityCode,
            List<ValidationIssue> issues)
        {
            string timelineJson = ReadProjectText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbtimelineability.json");
            string gameplayCueJson = ReadProjectText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplaycue.json");
            string gameplayEffectJson = ReadProjectText("Assets/DataGenerated/Luban/Json/GAS/exgas_tbgameplayeffect.json");

            if (string.IsNullOrWhiteSpace(timelineJson) || !ContainsAbilityTimeline(timelineJson, formalGasAbilityCode))
            {
                issues.Add(CreateIssue(
                    $"EX-GAS Ability {formalGasAbilityCode} 缺少可审计的正式 Timeline。基础攻击表现必须通过 EX-GAS Timeline / GameplayEffect / Cue 路径闭合。"));
                return;
            }

            string abilityCueJson = ExtractAbilityCueJson(timelineJson, gameplayCueJson, gameplayEffectJson, formalGasAbilityCode);
            if (!ContainsResolvableGameCoreAudioCue(abilityCueJson))
            {
                issues.Add(CreateIssue(
                    $"EX-GAS Ability {formalGasAbilityCode} 缺少可解析的正式普攻音效 Cue。出手/命中音效必须走 TaskPlayCue 或 GameplayEffect CueOnApply -> CuePlayGameCoreAudio，不得回退旧能力表音效字段。"));
            }

            if (ContainsResolvableMountPrefabCue(abilityCueJson))
            {
                issues.Add(CreateWarning(
                    $"EX-GAS Ability {formalGasAbilityCode} 当前配置了独立特效 CueMountPrefab。项目当前素材组织是角色动作与武器层分离，武器攻击和武器特效同属装备/武器动作；只有后续把正式独立特效素材拆出后，才应新增独立特效 Cue。"));
            }
        }

        public static AuditResult InspectFormalGasAbilities(params int[] formalGasAbilityCodes)
        {
            int[] abilityCodes = formalGasAbilityCodes is { Length: > 0 }
                ? formalGasAbilityCodes
                : new[] { GAS.Runtime.XAbility.ABILITY_Attack };

            List<AuditIssue> issues = new();
            foreach (int abilityCode in abilityCodes)
            {
                List<ValidationIssue> validationIssues = CollectIssues(abilityCode);
                for (int i = 0; i < validationIssues.Count; i++)
                {
                    issues.Add(CreateAuditIssue(abilityCode, validationIssues[i]));
                }
            }

            int errorCount = CountIssuesBySeverity(issues, MessageType.Error);
            int warningCount = CountIssuesBySeverity(issues, MessageType.Warning);
            int infoCount = CountIssuesBySeverity(issues, MessageType.Info);

            return new AuditResult
            {
                Success = errorCount == 0,
                Message = CreateAuditMessage(errorCount, warningCount, infoCount),
                Issues = issues.ToArray()
            };
        }

        private static bool ContainsAbilityTimeline(string timelineJson, int abilityCode)
        {
            if (abilityCode <= 0)
            {
                return false;
            }

            return abilityCode == GAS.Runtime.XAbility.ABILITY_Attack
                ? timelineJson.Contains("\"ID\": 101", StringComparison.Ordinal)
                : timelineJson.Contains($"\"ID\": {abilityCode}", StringComparison.Ordinal);
        }

        private static string ExtractAbilityCueJson(
            string timelineJson,
            string gameplayCueJson,
            string gameplayEffectJson,
            int abilityCode)
        {
            List<int> timelineCueIds = new();
            List<string> timelineInlineCueJson = new();
            List<int> effectIds = new();
            CollectCueEvidenceForAbilityTimeline(timelineJson, abilityCode, timelineCueIds, timelineInlineCueJson, effectIds);
            string directCueJson = string.Join("\n", CreateCueJsonForIds(gameplayCueJson, timelineCueIds), string.Join("\n", timelineInlineCueJson));
            string effectCueJson = CreateCueJsonForEffectIds(gameplayEffectJson, gameplayCueJson, effectIds);
            return $"{directCueJson}\n{effectCueJson}";
        }

        private static void CollectCueEvidenceForAbilityTimeline(
            string timelineJson,
            int abilityCode,
            List<int> timelineCueIds,
            List<string> timelineInlineCueJson,
            List<int> effectCueIds)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(timelineJson);
                foreach (JsonElement timeline in document.RootElement.EnumerateArray())
                {
                    if (!TimelineMatchesAbility(timeline, abilityCode))
                    {
                        continue;
                    }

                    CollectCueEvidenceFromTracks(timeline, timelineCueIds, timelineInlineCueJson, effectCueIds);
                    return;
                }
            }
            catch (JsonException)
            {
                string fallback = abilityCode == GAS.Runtime.XAbility.ABILITY_Attack ? "\"ID\": 101" : $"\"ID\": {abilityCode}";
                if (timelineJson.Contains(fallback, StringComparison.Ordinal))
                {
                    timelineCueIds.Add(0);
                }
            }
        }

        private static bool TimelineMatchesAbility(JsonElement timeline, int abilityCode)
        {
            if (!timeline.TryGetProperty("ID", out JsonElement idElement) ||
                !idElement.TryGetInt32(out int timelineId))
            {
                return false;
            }

            return abilityCode == GAS.Runtime.XAbility.ABILITY_Attack ? timelineId == 101 : timelineId == abilityCode;
        }

        private static void CollectCueEvidenceFromTracks(
            JsonElement timeline,
            List<int> timelineCueIds,
            List<string> timelineInlineCueJson,
            List<int> effectCueIds)
        {
            if (!timeline.TryGetProperty("Tracks", out JsonElement tracks) ||
                tracks.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement track in tracks.EnumerateArray())
            {
                if (!track.TryGetProperty("TaskClips", out JsonElement taskClips) ||
                    taskClips.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement taskClip in taskClips.EnumerateArray())
                {
                    if (!taskClip.TryGetProperty("Task", out JsonElement task))
                    {
                        continue;
                    }

                    CollectCueEvidenceFromTask(task, timelineCueIds, timelineInlineCueJson, effectCueIds);
                }
            }
        }

        private static void CollectCueEvidenceFromTask(
            JsonElement task,
            List<int> timelineCueIds,
            List<string> timelineInlineCueJson,
            List<int> effectCueIds)
        {
            if (!task.TryGetProperty("$type", out JsonElement typeElement))
            {
                return;
            }

            string taskType = typeElement.GetString();
            if (taskType == "TaskPlayCue")
            {
                if (task.TryGetProperty("Param", out JsonElement param) &&
                    param.TryGetProperty("CueLogic", out JsonElement cueLogic))
                {
                    AddInlineCueEvidence(cueLogic, timelineCueIds, timelineInlineCueJson);
                }
            }
            else if (taskType == "TaskApplyEffects" &&
                     task.TryGetProperty("Param", out JsonElement param) &&
                     param.TryGetProperty("IDs", out JsonElement ids) &&
                     ids.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement id in ids.EnumerateArray())
                {
                    if (id.TryGetInt32(out int effectId))
                    {
                        effectCueIds.Add(effectId);
                    }
                }
            }
        }

        private static void AddInlineCueEvidence(JsonElement cueLogic, List<int> cueIds, List<string> inlineCueJson)
        {
            if (cueLogic.TryGetProperty("ID", out JsonElement idElement) &&
                idElement.TryGetInt32(out int cueId))
            {
                cueIds.Add(cueId);
                return;
            }

            if (cueLogic.TryGetProperty("$type", out JsonElement typeElement) &&
                typeElement.GetString() is string cueType)
            {
                if (cueType == "CuePlayGameCoreAudio")
                {
                    inlineCueJson.Add(cueLogic.GetRawText());
                    return;
                }

                if (cueType == "CueMountPrefab")
                {
                    inlineCueJson.Add(cueLogic.GetRawText());
                    return;
                }

                cueIds.Add(cueType switch
                {
                    "CuePlaySound" => -1,
                    _ => 0
                });
            }
        }

        private static string CreateCueJsonForIds(string gameplayCueJson, List<int> cueIds)
        {
            if (cueIds.Count == 0)
            {
                return string.Empty;
            }

            string result = string.Empty;
            foreach (int cueId in cueIds)
            {
                if (cueId == -1)
                {
                    result += "{\"$type\":\"CuePlaySound\"}\n";
                }
                else if (cueId == -2)
                {
                    result += "{\"$type\":\"CueMountPrefab\"}\n";
                }
                else if (cueId == -3)
                {
                    result += "{\"$type\":\"CuePlayGameCoreAudio\"}\n";
                }
                else if (cueId > 0)
                {
                    string cueJson = ExtractCueJsonById(gameplayCueJson, cueId);
                    if (!string.IsNullOrWhiteSpace(cueJson))
                    {
                        result += cueJson + "\n";
                    }
                }
            }

            return result;
        }

        private static string CreateCueJsonForEffectIds(string gameplayEffectJson, string gameplayCueJson, List<int> effectIds)
        {
            if (effectIds.Count == 0 || string.IsNullOrWhiteSpace(gameplayEffectJson))
            {
                return string.Empty;
            }

            HashSet<int> cueIds = new();
            try
            {
                using JsonDocument document = JsonDocument.Parse(gameplayEffectJson);
                foreach (JsonElement effect in document.RootElement.EnumerateArray())
                {
                    if (!effect.TryGetProperty("ID", out JsonElement idElement) ||
                        !idElement.TryGetInt32(out int effectId) ||
                        !effectIds.Contains(effectId))
                    {
                        continue;
                    }

                    CollectCueIdsFromEffectProperty(effect, "CueOnApply", cueIds);
                    CollectCueIdsFromEffectProperty(effect, "CueOnTick", cueIds);
                    CollectCueIdsFromEffectProperty(effect, "CueOnAdd", cueIds);
                    CollectCueIdsFromEffectProperty(effect, "CueOnRemove", cueIds);
                    CollectCueIdsFromEffectProperty(effect, "CueOnActivate", cueIds);
                    CollectCueIdsFromEffectProperty(effect, "CueOnDeactivate", cueIds);
                }
            }
            catch (JsonException)
            {
                return string.Empty;
            }

            return CreateCueJsonForIds(gameplayCueJson, new List<int>(cueIds));
        }

        private static void CollectCueIdsFromEffectProperty(JsonElement effect, string propertyName, HashSet<int> cueIds)
        {
            if (!effect.TryGetProperty(propertyName, out JsonElement ids) ||
                ids.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement id in ids.EnumerateArray())
            {
                if (id.TryGetInt32(out int cueId) && cueId > 0)
                {
                    cueIds.Add(cueId);
                }
            }
        }

        private static bool ContainsResolvableGameCoreAudioCue(string abilityCueJson)
        {
            if (!ContainsCueType(abilityCueJson, "CuePlayGameCoreAudio"))
            {
                return false;
            }

            foreach (string audioResolverGuid in ExtractGameCoreAudioResolverGuids(abilityCueJson))
            {
                if (!string.IsNullOrWhiteSpace(audioResolverGuid) &&
                    ResolveAudioClipResolver(audioResolverGuid) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsResolvableMountPrefabCue(string abilityCueJson)
        {
            if (!ContainsCueType(abilityCueJson, "CueMountPrefab"))
            {
                return false;
            }

            foreach (string prefabPath in ExtractMountPrefabPaths(abilityCueJson))
            {
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    continue;
                }

                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null ||
                    ResolveDatabaseEntry<PrefabReference>(prefabPath)?.prefab != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCueType(string abilityCueJson, string cueType)
        {
            return !string.IsNullOrWhiteSpace(abilityCueJson) &&
                   (abilityCueJson.Contains($"\"$type\":\"{cueType}\"", StringComparison.Ordinal) ||
                    abilityCueJson.Contains($"\"$type\": \"{cueType}\"", StringComparison.Ordinal));
        }

        private static IEnumerable<string> ExtractGameCoreAudioResolverGuids(string abilityCueJson)
        {
            if (string.IsNullOrWhiteSpace(abilityCueJson))
            {
                yield break;
            }

            foreach (string cueJson in SplitJsonObjects(abilityCueJson))
            {
                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(cueJson);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (document)
                {
                JsonElement cue = GetCueLogicElement(document.RootElement);
                if (!cue.TryGetProperty("$type", out JsonElement typeElement) ||
                    typeElement.GetString() != "CuePlayGameCoreAudio" ||
                    !cue.TryGetProperty("Param", out JsonElement param) ||
                    !param.TryGetProperty("AudioResolverGuid", out JsonElement guidElement))
                {
                    continue;
                }

                yield return guidElement.GetString();
                }
            }
        }

        private static IEnumerable<string> ExtractMountPrefabPaths(string abilityCueJson)
        {
            if (string.IsNullOrWhiteSpace(abilityCueJson))
            {
                yield break;
            }

            foreach (string cueJson in SplitJsonObjects(abilityCueJson))
            {
                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(cueJson);
                }
                catch (JsonException)
                {
                    continue;
                }

                using (document)
                {
                JsonElement cue = GetCueLogicElement(document.RootElement);
                if (!cue.TryGetProperty("$type", out JsonElement typeElement) ||
                    typeElement.GetString() != "CueMountPrefab" ||
                    !cue.TryGetProperty("Param", out JsonElement param) ||
                    !param.TryGetProperty("PrefabPath", out JsonElement prefabPathElement))
                {
                    continue;
                }

                yield return prefabPathElement.GetString();
                }
            }
        }

        private static JsonElement GetCueLogicElement(JsonElement root)
        {
            return root.TryGetProperty("CueLogic", out JsonElement cueLogic)
                ? cueLogic
                : root;
        }

        private static IEnumerable<string> SplitJsonObjects(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                yield break;
            }

            int depth = 0;
            int start = -1;
            bool inString = false;
            bool escaped = false;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                    }

                    depth++;
                    continue;
                }

                if (c != '}')
                {
                    continue;
                }

                depth--;
                if (depth == 0 && start >= 0)
                {
                    yield return json[start..(i + 1)];
                    start = -1;
                }
            }
        }

        private static AudioClipResolver ResolveAudioClipResolver(string audioResolverGuid)
        {
            return ResolveDatabaseEntry<AudioClipResolver>(audioResolverGuid);
        }

        private static T ResolveDatabaseEntry<T>(string guid)
            where T : DatabaseEntry
        {
            GameConfig config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameData/GameCore/GameConfig.asset");
            DatabaseRegistry database = null;
            if (config != null)
            {
                SerializedObject serializedConfig = new(config);
                database = serializedConfig.FindProperty("m_databaseRegistry")?.objectReferenceValue as DatabaseRegistry;
            }

            return database == null ? null : database.GUIDToDatabaseEntry<T>(guid);
        }

        private static string ExtractCueJsonById(string gameplayCueJson, int cueId)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(gameplayCueJson);
                foreach (JsonElement cue in document.RootElement.EnumerateArray())
                {
                    if (cue.TryGetProperty("ID", out JsonElement idElement) &&
                        idElement.TryGetInt32(out int id) &&
                        id == cueId)
                    {
                        return cue.GetRawText();
                    }
                }
            }
            catch (JsonException)
            {
                string idText = $"\"ID\": {cueId}";
                int index = gameplayCueJson.IndexOf(idText, StringComparison.Ordinal);
                if (index >= 0)
                {
                    return gameplayCueJson[index..Math.Min(gameplayCueJson.Length, index + 600)];
                }
            }

            return string.Empty;
        }

        private static string ReadProjectText(string assetPath)
        {
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : string.Empty;
        }

        private static ValidationIssue CreateIssue(string message)
        {
            return new ValidationIssue
            {
                Message = message,
                Severity = MessageType.Error
            };
        }

        private static ValidationIssue CreateWarning(string message)
        {
            return new ValidationIssue
            {
                Message = message,
                Severity = MessageType.Warning
            };
        }

        private static int CountIssuesBySeverity(List<AuditIssue> issues, MessageType severity)
        {
            int count = 0;
            string severityText = severity.ToString();
            for (int i = 0; i < issues.Count; i++)
            {
                if (string.Equals(issues[i].Severity, severityText, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string CreateAuditMessage(int errorCount, int warningCount, int infoCount)
        {
            if (errorCount == 0 && warningCount == 0 && infoCount == 0)
            {
                return "技能职责审计通过。";
            }

            if (errorCount == 0)
            {
                return $"技能职责审计通过，但发现 {warningCount} 个警告、{infoCount} 个提示。";
            }

            return $"技能职责审计发现 {errorCount} 个错误、{warningCount} 个警告、{infoCount} 个提示。";
        }

        private static AuditIssue CreateAuditIssue(
            int formalGasAbilityCode,
            ValidationIssue validationIssue)
        {
            return new AuditIssue
            {
                AbilityPath = $"EX-GAS Ability {formalGasAbilityCode}",
                AbilityName = ResolveFormalGasAbilityName(formalGasAbilityCode),
                AbilityType = "EX-GAS Ability",
                Issue = validationIssue?.Message ?? "未知问题。",
                Severity = validationIssue != null ? validationIssue.Severity.ToString() : MessageType.Error.ToString()
            };
        }

        private static string ResolveFormalGasAbilityName(int formalGasAbilityCode)
        {
            if (FormalGasAbilityIdentityResolver.TryResolveAbilityIdentity(
                    formalGasAbilityCode,
                    out FormalGasAbilityIdentity identity) &&
                !string.IsNullOrWhiteSpace(identity.DisplayName))
            {
                return identity.DisplayName;
            }

            return $"EX-GAS Ability {formalGasAbilityCode}";
        }

    }
}
