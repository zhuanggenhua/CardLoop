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

            ContentValidationReport report =
                ContentValidator.ValidateContentAssets(contentAssets);
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
    }
}

