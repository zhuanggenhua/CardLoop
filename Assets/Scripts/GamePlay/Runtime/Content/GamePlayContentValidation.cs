using System;
using System.Collections.Generic;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace GamePlay
{
    /// <summary>
    /// 内容校验问题对资产进入正式运行时索引的影响等级。
    /// </summary>
    public enum GamePlayContentValidationSeverity
    {
        Info = 0,
        Warning = 10,
        Error = 20
    }

    /// <summary>
    /// 一条可定位到 Unity 作者资产的内容校验结果。
    /// </summary>
    public sealed class GamePlayContentValidationIssue
    {
        /// <summary>
        /// 创建校验结果。问题码用于稳定识别规则，消息用于向内容作者解释现实问题。
        /// </summary>
        public GamePlayContentValidationIssue(
            GamePlayContentValidationSeverity severity,
            string code,
            string message,
            UObject sourceObject)
        {
            Severity = severity;
            Code = code;
            Message = message;
            SourceObject = sourceObject;
        }

        /// <summary>该问题是否阻止建立内容索引。</summary>
        public GamePlayContentValidationSeverity Severity { get; }

        /// <summary>供测试、日志筛选和编辑器工具稳定识别的规则码。</summary>
        public string Code { get; }

        /// <summary>面向内容作者的中文问题说明。</summary>
        public string Message { get; }

        /// <summary>产生问题的 Unity 作者资产；无法定位具体资产时可以为空。</summary>
        public UObject SourceObject { get; }

        /// <summary>作为 Unity 日志上下文使用的源资产别名。</summary>
        public UObject Context => SourceObject;
    }

    /// <summary>
    /// 一次内容校验的有序结果集合。警告允许继续建索引，错误会阻止正式初始化。
    /// </summary>
    public sealed class GamePlayContentValidationReport
    {
        private readonly List<GamePlayContentValidationIssue> m_issues = new();

        /// <summary>按发现顺序保存的只读校验问题。</summary>
        public IReadOnlyList<GamePlayContentValidationIssue> Issues => m_issues;

        /// <summary>当前报告是否包含至少一条会阻止建立索引的错误。</summary>
        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < m_issues.Count; i++)
                {
                    if (m_issues[i].Severity == GamePlayContentValidationSeverity.Error)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>追加一条会阻止正式内容索引建立的错误。</summary>
        public void AddError(
            string code,
            string message,
            UObject sourceObject = null)
        {
            Add(GamePlayContentValidationSeverity.Error, code, message, sourceObject);
        }

        /// <summary>追加一条允许继续建立索引、但应由内容作者处理的警告。</summary>
        public void AddWarning(
            string code,
            string message,
            UObject sourceObject = null)
        {
            Add(GamePlayContentValidationSeverity.Warning, code, message, sourceObject);
        }

        /// <summary>按指定等级追加一条校验问题，不自动去重或改写问题码。</summary>
        public void Add(
            GamePlayContentValidationSeverity severity,
            string code,
            string message,
            UObject sourceObject)
        {
            m_issues.Add(new GamePlayContentValidationIssue(severity, code, message, sourceObject));
        }

        /// <summary>
        /// 报告含错误时抛出汇总异常；只有警告或没有问题时正常返回。
        /// </summary>
        public void ThrowIfHasErrors()
        {
            if (!HasErrors)
            {
                return;
            }

            var lines = new List<string>();
            for (int i = 0; i < Issues.Count; i++)
            {
                GamePlayContentValidationIssue issue = Issues[i];
                if (issue.Severity == GamePlayContentValidationSeverity.Error)
                {
                    lines.Add($"{issue.Code}: {issue.Message}");
                }
            }

            throw new InvalidOperationException(
                $"GamePlay 内容校验失败：{Environment.NewLine}{string.Join(Environment.NewLine, lines)}");
        }
    }

    /// <summary>
    /// GamePlay 作者资产进入运行时索引前的统一校验入口。
    /// 它只验证本模块拥有的身份与标签码形状，不替代 EX-GAS 标签表校验。
    /// </summary>
    public static class GamePlayContentValidator
    {
        /// <summary>
        /// 检查空引用、重复资产引用、内容 ID 格式与唯一性，以及 EX-GAS 标签码的基础有效性。
        /// 返回报告而不直接抛出异常，调用方可选择在编辑器中展示或在正式初始化时拒绝启动。
        /// </summary>
        public static GamePlayContentValidationReport ValidateContentAssets(
            IEnumerable<GamePlayContentAsset> contentAssets)
        {
            var report = new GamePlayContentValidationReport();
            var contentIds = new Dictionary<GamePlayContentId, UObject>();
            var seenAssets = new HashSet<GamePlayContentAsset>();

            if (contentAssets == null)
            {
                return report;
            }

            foreach (GamePlayContentAsset contentAsset in contentAssets)
            {
                if (contentAsset == null)
                {
                    report.AddError("CONTENT_ASSET_NULL", "内容资产引用为空。");
                    continue;
                }

                if (!seenAssets.Add(contentAsset))
                {
                    report.AddWarning(
                        "CONTENT_ASSET_DUPLICATE_REFERENCE",
                        $"内容资产 {contentAsset.name} 被重复传入校验。",
                        contentAsset);
                    continue;
                }

                ValidateIdentity(contentAsset, contentIds, report);
                ValidateTags(contentAsset, report);
            }

            ValidateActionDefinitions(seenAssets, contentIds, report);

            return report;
        }

        private static void ValidateIdentity(
            GamePlayContentAsset contentAsset,
            Dictionary<GamePlayContentId, UObject> contentIds,
            GamePlayContentValidationReport report)
        {
            string contentId = contentAsset.ContentId.Value;
            if (!GamePlayContentKeyUtility.IsValidKey(contentId))
            {
                report.AddError(
                    "CONTENT_ID_INVALID",
                    $"内容资产 {contentAsset.name} 的内容 ID 无效：{contentId}。",
                    contentAsset);
                return;
            }

            if (contentIds.TryGetValue(contentAsset.ContentId, out UObject existing))
            {
                report.AddError(
                    "CONTENT_ID_DUPLICATE",
                    $"内容 ID 重复：{contentId}，冲突对象：{existing.name} / {contentAsset.name}。",
                    contentAsset);
                return;
            }

            contentIds.Add(contentAsset.ContentId, contentAsset);
        }

        private static void ValidateTags(
            GamePlayContentAsset contentAsset,
            GamePlayContentValidationReport report)
        {
            var seen = new HashSet<int>();
            for (int i = 0; i < contentAsset.TagCodes.Count; i++)
            {
                int tagCode = contentAsset.TagCodes[i];
                if (tagCode <= 0)
                {
                    report.AddError(
                        "CONTENT_TAG_INVALID",
                        $"{contentAsset.ContentId} 的 EX-GAS 标签码无效：{tagCode}。",
                        contentAsset);
                    continue;
                }

                if (!seen.Add(tagCode))
                {
                    report.AddWarning(
                        "CONTENT_TAG_DUPLICATE",
                        $"{contentAsset.ContentId} 重复声明 EX-GAS 标签码：{tagCode}。",
                        contentAsset);
                }
            }
        }

        private static void ValidateActionDefinitions(
            IEnumerable<GamePlayContentAsset> contentAssets,
            IReadOnlyDictionary<GamePlayContentId, UObject> contentIds,
            GamePlayContentValidationReport report)
        {
            var conditionSignatures = new Dictionary<string, GamePlayActionDefinition>();
            foreach (GamePlayContentAsset contentAsset in contentAssets)
            {
                if (contentAsset is not GamePlayActionDefinition action)
                {
                    continue;
                }

                var slotKeys = new HashSet<string>(StringComparer.Ordinal);
                ValidateActionSlots(action, contentIds, slotKeys, report);
                ValidateActionResultIntents(action, action.ResultIntents, slotKeys, contentIds, report);
                ValidateActionResultBranches(action, slotKeys, contentIds, report);
                WarnIfActionSharesConditionSignature(action, conditionSignatures, report);
            }
        }

        private static void ValidateActionSlots(
            GamePlayActionDefinition action,
            IReadOnlyDictionary<GamePlayContentId, UObject> contentIds,
            HashSet<string> slotKeys,
            GamePlayContentValidationReport report)
        {
            for (int slotIndex = 0; slotIndex < action.ParticipationSlots.Count; slotIndex++)
            {
                GamePlayActionSlotDefinition slot = action.ParticipationSlots[slotIndex];
                if (slot == null)
                {
                    report.AddError(
                        "ACTION_SLOT_NULL",
                        $"行动 {action.ContentId} 的第 {slotIndex + 1} 个参与槽位为空。",
                        action);
                    continue;
                }

                if (!GamePlayContentKeyUtility.IsValidKey(slot.Key))
                {
                    report.AddError(
                        "ACTION_SLOT_KEY_INVALID",
                        $"行动 {action.ContentId} 的参与槽位键无效：{slot.Key}。",
                        action);
                }
                else if (!slotKeys.Add(slot.Key))
                {
                    report.AddError(
                        "ACTION_SLOT_KEY_DUPLICATE",
                        $"行动 {action.ContentId} 重复声明参与槽位键：{slot.Key}。",
                        action);
                }

                if (slot.MinimumParticipants < 0 ||
                    slot.MaximumParticipants < 0 ||
                    (slot.MaximumParticipants > 0 && slot.MaximumParticipants < slot.MinimumParticipants))
                {
                    report.AddError(
                        "ACTION_SLOT_COUNT_INVALID",
                        $"行动 {action.ContentId} 的槽位 {slot.Key} 参与数量范围无效：最少 {slot.MinimumParticipants}，最多 {slot.MaximumParticipants}。",
                        action);
                }

                ValidateContentIdReferences(
                    action,
                    slot.AllowedContentIds,
                    contentIds,
                    "ACTION_SLOT_ALLOWED_CONTENT_INVALID",
                    "ACTION_SLOT_ALLOWED_CONTENT_UNKNOWN",
                    $"行动 {action.ContentId} 的槽位 {slot.Key}",
                    report);
                ValidateTagCodes(
                    action,
                    slot.RequiredAllContentTagCodes,
                    "ACTION_SLOT_CONTENT_TAG_INVALID",
                    $"行动 {action.ContentId} 的槽位 {slot.Key} 内容必须全部具有标签",
                    report);
                ValidateTagCodes(
                    action,
                    slot.RequiredAnyContentTagCodes,
                    "ACTION_SLOT_CONTENT_TAG_INVALID",
                    $"行动 {action.ContentId} 的槽位 {slot.Key} 内容至少具有一个标签",
                    report);
                ValidateTagCodes(
                    action,
                    slot.RequiredNoneContentTagCodes,
                    "ACTION_SLOT_CONTENT_TAG_INVALID",
                    $"行动 {action.ContentId} 的槽位 {slot.Key} 内容不能具有标签",
                    report);
                ValidateTagCodes(
                    action,
                    slot.RequiredAllAbilitySystemTagCodes,
                    "ACTION_SLOT_ABILITY_TAG_INVALID",
                    $"行动 {action.ContentId} 的槽位 {slot.Key} 角色必须全部具有标签",
                    report);
                ValidateTagCodes(
                    action,
                    slot.RequiredAnyAbilitySystemTagCodes,
                    "ACTION_SLOT_ABILITY_TAG_INVALID",
                    $"行动 {action.ContentId} 的槽位 {slot.Key} 角色至少具有一个标签",
                    report);
                ValidateTagCodes(
                    action,
                    slot.RequiredNoneAbilitySystemTagCodes,
                    "ACTION_SLOT_ABILITY_TAG_INVALID",
                    $"行动 {action.ContentId} 的槽位 {slot.Key} 角色不能具有标签",
                    report);
            }
        }

        private static void ValidateActionResultBranches(
            GamePlayActionDefinition action,
            HashSet<string> slotKeys,
            IReadOnlyDictionary<GamePlayContentId, UObject> contentIds,
            GamePlayContentValidationReport report)
        {
            var branchKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int branchIndex = 0; branchIndex < action.ResultBranches.Count; branchIndex++)
            {
                GamePlayActionResultBranch branch = action.ResultBranches[branchIndex];
                if (branch == null)
                {
                    report.AddError(
                        "ACTION_RESULT_BRANCH_NULL",
                        $"行动 {action.ContentId} 的第 {branchIndex + 1} 个随机结果分支为空。",
                        action);
                    continue;
                }

                if (!GamePlayContentKeyUtility.IsValidKey(branch.Key))
                {
                    report.AddError(
                        "ACTION_RESULT_BRANCH_KEY_INVALID",
                        $"行动 {action.ContentId} 的随机结果分支键无效：{branch.Key}。",
                        action);
                }
                else if (!branchKeys.Add(branch.Key))
                {
                    report.AddError(
                        "ACTION_RESULT_BRANCH_KEY_DUPLICATE",
                        $"行动 {action.ContentId} 重复声明随机结果分支键：{branch.Key}。",
                        action);
                }

                if (branch.Weight <= 0)
                {
                    report.AddError(
                        "ACTION_RESULT_BRANCH_WEIGHT_INVALID",
                        $"行动 {action.ContentId} 的随机结果分支 {branch.Key} 权重必须大于 0，当前值为 {branch.Weight}。",
                        action);
                }

                ValidateActionResultIntents(action, branch.ResultIntents, slotKeys, contentIds, report);
            }
        }

        private static void ValidateActionResultIntents(
            GamePlayActionDefinition action,
            IReadOnlyList<GamePlayActionResultIntent> resultIntents,
            HashSet<string> slotKeys,
            IReadOnlyDictionary<GamePlayContentId, UObject> contentIds,
            GamePlayContentValidationReport report)
        {
            for (int intentIndex = 0; intentIndex < resultIntents.Count; intentIndex++)
            {
                GamePlayActionResultIntent intent = resultIntents[intentIndex];
                switch (intent)
                {
                    case TabletopCardRemoveResultIntent removeIntent:
                        ValidateResultSlotKey(
                            action,
                            removeIntent.SlotKey,
                            slotKeys,
                            "ACTION_RESULT_REMOVE_SLOT_UNKNOWN",
                            report);
                        break;
                    case TabletopCardCreateResultIntent createIntent:
                        ValidateCreatedContent(action, createIntent, contentIds, report);
                        ValidateResultSlotKey(
                            action,
                            createIntent.AnchorSlotKey,
                            slotKeys,
                            "ACTION_RESULT_CREATE_ANCHOR_SLOT_UNKNOWN",
                            report);
                        break;
                    case null:
                        report.AddError(
                            "ACTION_RESULT_INTENT_NULL",
                            $"行动 {action.ContentId} 的第 {intentIndex + 1} 个结果意图为空。",
                            action);
                        break;
                }
            }
        }

        private static void ValidateCreatedContent(
            GamePlayActionDefinition action,
            TabletopCardCreateResultIntent intent,
            IReadOnlyDictionary<GamePlayContentId, UObject> contentIds,
            GamePlayContentValidationReport report)
        {
            if (!intent.ContentId.IsValid)
            {
                report.AddError(
                    "ACTION_RESULT_CREATE_CONTENT_INVALID",
                    $"行动 {action.ContentId} 的产物内容 ID 无效：{intent.ContentId}。",
                    action);
            }
            else if (!contentIds.ContainsKey(intent.ContentId))
            {
                report.AddError(
                    "ACTION_RESULT_CREATE_CONTENT_UNKNOWN",
                    $"行动 {action.ContentId} 的产物内容 {intent.ContentId} 没有进入当前内容作者源集合。",
                    action);
            }

            if (intent.Count <= 0)
            {
                report.AddError(
                    "ACTION_RESULT_CREATE_COUNT_INVALID",
                    $"行动 {action.ContentId} 的产物生成数量必须大于 0，当前值为 {intent.Count}。",
                    action);
            }
        }

        private static void ValidateResultSlotKey(
            GamePlayActionDefinition action,
            string slotKey,
            HashSet<string> slotKeys,
            string code,
            GamePlayContentValidationReport report)
        {
            if (!GamePlayContentKeyUtility.IsValidKey(slotKey) || !slotKeys.Contains(slotKey))
            {
                report.AddError(
                    code,
                    $"行动 {action.ContentId} 的结果引用了不存在的参与槽位：{slotKey}。",
                    action);
            }
        }

        private static void ValidateContentIdReferences(
            GamePlayActionDefinition action,
            IReadOnlyList<GamePlayContentId> contentIdReferences,
            IReadOnlyDictionary<GamePlayContentId, UObject> contentIds,
            string invalidCode,
            string unknownCode,
            string messagePrefix,
            GamePlayContentValidationReport report)
        {
            for (int i = 0; i < contentIdReferences.Count; i++)
            {
                GamePlayContentId contentId = contentIdReferences[i];
                if (!contentId.IsValid)
                {
                    report.AddError(
                        invalidCode,
                        $"{messagePrefix} 引用了无效内容 ID：{contentId}。",
                        action);
                }
                else if (!contentIds.ContainsKey(contentId))
                {
                    report.AddError(
                        unknownCode,
                        $"{messagePrefix} 引用了当前内容作者源集合中不存在的内容：{contentId}。",
                        action);
                }
            }
        }

        private static void ValidateTagCodes(
            UObject sourceObject,
            IReadOnlyList<int> tagCodes,
            string code,
            string messagePrefix,
            GamePlayContentValidationReport report)
        {
            for (int i = 0; i < tagCodes.Count; i++)
            {
                int tagCode = tagCodes[i];
                if (tagCode <= 0)
                {
                    report.AddError(
                        code,
                        $"{messagePrefix} 包含无效 EX-GAS 标签码：{tagCode}。",
                        sourceObject);
                }
            }
        }

        private static void WarnIfActionSharesConditionSignature(
            GamePlayActionDefinition action,
            Dictionary<string, GamePlayActionDefinition> conditionSignatures,
            GamePlayContentValidationReport report)
        {
            string signature = BuildConditionSignature(action);
            if (string.IsNullOrEmpty(signature))
            {
                return;
            }

            if (conditionSignatures.TryGetValue(signature, out GamePlayActionDefinition existing))
            {
                report.AddWarning(
                    "ACTION_CONDITION_SIGNATURE_SHARED",
                    $"行动 {action.ContentId} 与行动 {existing.ContentId} 使用相同参与条件。它们会在同一次交互中同时成为候选；若不是有意设计，请拆分条件或合并为随机结果分支。",
                    action);
            }
            else
            {
                conditionSignatures.Add(signature, action);
            }
        }

        private static string BuildConditionSignature(GamePlayActionDefinition action)
        {
            if (action.ParticipationSlots.Count == 0)
            {
                return string.Empty;
            }

            var slotSignatures = new List<string>();
            for (int i = 0; i < action.ParticipationSlots.Count; i++)
            {
                GamePlayActionSlotDefinition slot = action.ParticipationSlots[i];
                if (slot == null)
                {
                    return string.Empty;
                }

                slotSignatures.Add(
                    slot.MinimumParticipants + ":" +
                    slot.MaximumParticipants + "|ids=" +
                    JoinContentIds(slot.AllowedContentIds) + "|ca=" +
                    JoinInts(slot.RequiredAllContentTagCodes) + "|cy=" +
                    JoinInts(slot.RequiredAnyContentTagCodes) + "|cn=" +
                    JoinInts(slot.RequiredNoneContentTagCodes) + "|aa=" +
                    JoinInts(slot.RequiredAllAbilitySystemTagCodes) + "|ay=" +
                    JoinInts(slot.RequiredAnyAbilitySystemTagCodes) + "|an=" +
                    JoinInts(slot.RequiredNoneAbilitySystemTagCodes));
            }

            slotSignatures.Sort(StringComparer.Ordinal);
            return string.Join(";", slotSignatures);
        }

        private static string JoinContentIds(IReadOnlyList<GamePlayContentId> contentIds)
        {
            var values = new List<string>();
            for (int i = 0; i < contentIds.Count; i++)
            {
                values.Add(contentIds[i].Value);
            }

            values.Sort(StringComparer.Ordinal);
            return string.Join(",", values);
        }

        private static string JoinInts(IReadOnlyList<int> values)
        {
            var copy = new List<int>();
            for (int i = 0; i < values.Count; i++)
            {
                copy.Add(values[i]);
            }

            copy.Sort();
            return string.Join(",", copy);
        }
    }
}
