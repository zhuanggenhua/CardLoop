using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Content
{
	/// <summary>
	/// 内容校验问题的严重级别。
	/// </summary>
	public enum ContentValidationSeverity
	{
		Info = 0,
		Warning = 10,
		Error = 20
	}

	/// <summary>
	/// 一条可定位到 Unity 作者资产的内容校验结果。
	/// </summary>
	public sealed class ContentValidationIssue
	{
		public ContentValidationSeverity Severity { get; }

		public string Code { get; }

		public string Message { get; }

		public Object SourceObject { get; }

		public Object Context => SourceObject;

		public ContentValidationIssue(
			ContentValidationSeverity severity,
			string code,
			string message,
			Object sourceObject)
		{
			Severity = severity;
			Code = code;
			Message = message;
			SourceObject = sourceObject;
		}
	}

	/// <summary>
	/// 汇总内容校验问题，并在存在错误时阻止索引建立。
	/// </summary>
	public sealed class ContentValidationReport
	{
		private readonly List<ContentValidationIssue> m_issues = new();
		private readonly ReadOnlyCollection<ContentValidationIssue> m_readOnlyIssues;

		public IReadOnlyList<ContentValidationIssue> Issues => m_readOnlyIssues;

		public ContentValidationReport()
		{
			m_readOnlyIssues = m_issues.AsReadOnly();
		}

		public bool HasErrors
		{
			get
			{
				for (int i = 0; i < m_issues.Count; i++)
				{
					if (m_issues[i].Severity == ContentValidationSeverity.Error)
					{
						return true;
					}
				}
				return false;
			}
		}

		public void AddError(string code, string message, Object sourceObject = null)
		{
			Add(ContentValidationSeverity.Error, code, message, sourceObject);
		}

		public void AddWarning(string code, string message, Object sourceObject = null)
		{
			Add(ContentValidationSeverity.Warning, code, message, sourceObject);
		}

		public void Add(
			ContentValidationSeverity severity,
			string code,
			string message,
			Object sourceObject)
		{
			m_issues.Add(new ContentValidationIssue(severity, code, message, sourceObject));
		}

		public void ThrowIfHasErrors()
		{
			if (!HasErrors)
			{
				return;
			}

			List<string> lines = new();
			for (int i = 0; i < Issues.Count; i++)
			{
				ContentValidationIssue issue = Issues[i];
				if (issue.Severity == ContentValidationSeverity.Error)
				{
					lines.Add(issue.Code + ": " + issue.Message);
				}
			}
			throw new InvalidOperationException(
				"Gameplay 内容校验失败：" + Environment.NewLine +
				string.Join(Environment.NewLine, lines));
		}
	}

	/// <summary>
	/// 一次内容集合校验的只读上下文，供内容派生类型检查跨资产引用。
	/// </summary>
	public sealed class ContentValidationContext
	{
		private readonly IReadOnlyDictionary<ContentId, ContentAsset> m_assetsById;
		private readonly ContentValidationReport m_report;

		public IReadOnlyList<ContentAsset> Assets { get; }

		internal ContentValidationContext(
			IReadOnlyList<ContentAsset> assets,
			IReadOnlyDictionary<ContentId, ContentAsset> assetsById,
			ContentValidationReport report)
		{
			Assets = assets ?? throw new ArgumentNullException(nameof(assets));
			m_assetsById = assetsById ?? throw new ArgumentNullException(nameof(assetsById));
			m_report = report ?? throw new ArgumentNullException(nameof(report));
		}

		public bool TryGet(ContentId contentId, out ContentAsset contentAsset)
		{
			return m_assetsById.TryGetValue(contentId, out contentAsset);
		}

		public bool TryGet<TAsset>(ContentId contentId, out TAsset contentAsset)
			where TAsset : ContentAsset
		{
			if (TryGet(contentId, out ContentAsset found) && found is TAsset typed)
			{
				contentAsset = typed;
				return true;
			}

			contentAsset = null;
			return false;
		}

		public void AddError(string code, string message, Object sourceObject)
		{
			m_report.AddError(code, message, sourceObject);
		}

		public void AddWarning(string code, string message, Object sourceObject)
		{
			m_report.AddWarning(code, message, sourceObject);
		}
	}

	/// <summary>
	/// 校验所有 Gameplay 内容共有的身份和标签，再把领域规则交回具体内容对象。
	/// </summary>
	public static class ContentValidator
	{
		public static ContentValidationReport ValidateContentAssets(
			IEnumerable<ContentAsset> contentAssets)
		{
			ContentValidationReport report = new();
			List<ContentAsset> assets = new();
			Dictionary<ContentId, ContentAsset> assetsById = new();
			HashSet<ContentAsset> seenAssets = new();

			if (contentAssets != null)
			{
				foreach (ContentAsset contentAsset in contentAssets)
				{
					if (contentAsset == null)
					{
						report.AddError("CONTENT_ASSET_NULL", "内容资产引用为空。");
						continue;
					}

					if (!seenAssets.Add(contentAsset))
					{
						report.AddError(
							"CONTENT_ASSET_DUPLICATE_REFERENCE",
							"内容资产 " + contentAsset.name + " 被重复传入校验。",
							contentAsset);
						continue;
					}

					assets.Add(contentAsset);
					ValidateIdentity(contentAsset, assetsById, report);
					ValidateTags(contentAsset, report);
				}
			}

			ContentValidationContext context = new(assets.AsReadOnly(), assetsById, report);
			for (int i = 0; i < assets.Count; i++)
			{
				assets[i].ValidateContentAsset(context);
			}
			return report;
		}

		private static void ValidateIdentity(
			ContentAsset contentAsset,
			Dictionary<ContentId, ContentAsset> assetsById,
			ContentValidationReport report)
		{
			string contentId = contentAsset.ContentId.Value;
			if (!ContentIdRules.IsValidKey(contentId))
			{
				report.AddError(
					"CONTENT_ID_INVALID",
					"内容资产 " + contentAsset.name + " 的内容 ID 无效：" + contentId + "。",
					contentAsset);
				return;
			}

			if (assetsById.TryGetValue(contentAsset.ContentId, out ContentAsset existing))
			{
				report.AddError(
					"CONTENT_ID_DUPLICATE",
					"内容 ID 重复：" + contentId + "，冲突对象：" +
					existing.name + " / " + contentAsset.name + "。",
					contentAsset);
				return;
			}

			assetsById.Add(contentAsset.ContentId, contentAsset);
		}

		private static void ValidateTags(
			ContentAsset contentAsset,
			ContentValidationReport report)
		{
			HashSet<int> seen = new();
			for (int i = 0; i < contentAsset.TagCodes.Count; i++)
			{
				int tagCode = contentAsset.TagCodes[i];
				if (tagCode <= 0)
				{
					report.AddError(
						"CONTENT_TAG_INVALID",
						$"{contentAsset.ContentId} 的 EX-GAS 标签码无效：{tagCode}。",
						contentAsset);
				}
				else if (!seen.Add(tagCode))
				{
					report.AddWarning(
						"CONTENT_TAG_DUPLICATE",
						$"{contentAsset.ContentId} 重复声明 EX-GAS 标签码：{tagCode}。",
						contentAsset);
				}
			}
		}
	}
}
