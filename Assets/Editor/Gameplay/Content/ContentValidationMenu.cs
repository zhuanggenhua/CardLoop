using System.Collections.Generic;
using GAS.General;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Scenarios;
using Gameplay.Tabletop;

namespace Gameplay.Editor.Content
{
    /// <summary>
    /// 在 Unity 编辑器中扫描并展示全部 Gameplay 作者资产的校验结果。
    /// 该工具只读取和报告问题，不自动改写内容 ID、标签码或资源引用。
    /// </summary>
    public static class ContentValidationMenu
    {
        /// <summary>
        /// 按当前 EX-GAS 作者表校验内容资产。此入口只读取生成后的官方标签选择数据，
        /// 不建立 Gameplay 自己的标签表，也不承担运行时内容集合的加载职责。
        /// </summary>
        public static ContentValidationReport ValidateContentAssetsForEditor(
            IEnumerable<ContentAsset> contentAssets)
        {
            List<ContentAsset> assets = new();
            if (contentAssets != null)
            {
                foreach (ContentAsset contentAsset in contentAssets)
                {
                    assets.Add(contentAsset);
                }
            }

            ContentValidationReport report = ContentValidator.ValidateContentAssets(assets);
            AppendUnknownStaticTagIssues(report, assets);
            return report;
        }

        /// <summary>
        /// 扫描当前 AssetDatabase 中的 Gameplay 内容资产，并把问题定位到对应 Unity 资产。
        /// 错误会阻止运行时索引建立，警告只提示作者处理。
        /// </summary>
        [MenuItem("Gameplay/内容/校验内容资产")]
        public static void ValidateAllContentAssets()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            var contentAssets = new System.Collections.Generic.List<ContentAsset>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ScriptableObject asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset is ContentAsset contentAsset)
                {
                    contentAssets.Add(contentAsset);
                }
            }

            ContentValidationReport report = ValidateContentAssetsForEditor(contentAssets);
            foreach (ContentValidationIssue issue in report.Issues)
            {
                switch (issue.Severity)
                {
                    case ContentValidationSeverity.Error:
                        Debug.LogError($"[{issue.Code}] {issue.Message}", issue.Context);
                        break;
                    case ContentValidationSeverity.Warning:
                        Debug.LogWarning($"[{issue.Code}] {issue.Message}", issue.Context);
                        break;
                    default:
                        Debug.Log($"[{issue.Code}] {issue.Message}", issue.Context);
                        break;
                }
            }

            if (report.Issues.Count == 0)
            {
                Debug.Log($"Gameplay 内容校验通过：共 {contentAssets.Count} 个内容资产。");
            }
            else if (!report.HasErrors)
            {
                Debug.Log($"Gameplay 内容校验完成：{contentAssets.Count} 个内容资产，{report.Issues.Count} 个警告/提示，无错误。");
            }
        }

        private static void AppendUnknownStaticTagIssues(
            ContentValidationReport report,
            IReadOnlyList<ContentAsset> contentAssets)
        {
            HashSet<int> declaredTagCodes = new();
            for (int assetIndex = 0; assetIndex < contentAssets.Count; assetIndex++)
            {
                ContentAsset contentAsset = contentAssets[assetIndex];
                if (contentAsset == null)
                {
                    continue;
                }

                for (int tagIndex = 0; tagIndex < contentAsset.TagCodes.Count; tagIndex++)
                {
                    int tagCode = contentAsset.TagCodes[tagIndex];
                    if (tagCode > 0)
                    {
                        declaredTagCodes.Add(tagCode);
                    }
                }
            }

            if (declaredTagCodes.Count == 0)
            {
                return;
            }

            GeneralGasChoiceHelper.LoadCache();
            List<ValueDropdownItem> officialChoices = GeneralGasChoiceHelper.Tags();
            HashSet<int> officialTagCodes = new();
            for (int choiceIndex = 0; choiceIndex < officialChoices.Count; choiceIndex++)
            {
                if (officialChoices[choiceIndex].Value is int tagCode)
                {
                    officialTagCodes.Add(tagCode);
                }
            }

            if (officialTagCodes.Count == 0)
            {
                report.AddError(
                    "CONTENT_TAG_AUTHORING_SOURCE_EMPTY",
                    "EX-GAS 没有提供可用的 GameplayTag 作者数据。请先检查 GAS 表生成和标签缓存。");
                return;
            }

            for (int assetIndex = 0; assetIndex < contentAssets.Count; assetIndex++)
            {
                ContentAsset contentAsset = contentAssets[assetIndex];
                if (contentAsset == null)
                {
                    continue;
                }

                for (int tagIndex = 0; tagIndex < contentAsset.TagCodes.Count; tagIndex++)
                {
                    int tagCode = contentAsset.TagCodes[tagIndex];
                    if (tagCode > 0 && !officialTagCodes.Contains(tagCode))
                    {
                        report.AddError(
                            "CONTENT_TAG_UNKNOWN",
                            $"内容资产 {contentAsset.name} 引用了 EX-GAS 作者表中不存在的静态标签码：{tagCode}。",
                            contentAsset);
                    }
                }
            }
        }
    }
}
