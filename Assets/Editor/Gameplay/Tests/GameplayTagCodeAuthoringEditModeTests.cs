using System;
using System.Reflection;
using GAS.Runtime;
using Gameplay.Actions;
using Gameplay.Content;
using Gameplay.Editor.Content;
using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证 EX-GAS 标签作者字段必须由选择器维护，不能退回为手填整数码。
	/// </summary>
	public sealed class GameplayTagCodeAuthoringEditModeTests
	{
		private const string ExGasTagDropdownSource = "@GAS.General.GeneralGasChoiceHelper.Tags()";

		[Test]
		public void ContentAndActionTagFields_UseOfficialExGasTagDropdown()
		{
			AssertFieldUsesOfficialDropdown(typeof(ContentAsset), "m_tagCodes");
			AssertFieldUsesOfficialDropdown(typeof(ActionSlotDefinition), "m_requiredAllContentTagCodes");
			AssertFieldUsesOfficialDropdown(typeof(ActionSlotDefinition), "m_requiredAnyContentTagCodes");
			AssertFieldUsesOfficialDropdown(typeof(ActionSlotDefinition), "m_requiredNoneContentTagCodes");
			AssertFieldUsesOfficialDropdown(typeof(ActionSlotDefinition), "m_requiredAllAbilitySystemTagCodes");
			AssertFieldUsesOfficialDropdown(typeof(ActionSlotDefinition), "m_requiredAnyAbilitySystemTagCodes");
			AssertFieldUsesOfficialDropdown(typeof(ActionSlotDefinition), "m_requiredNoneAbilitySystemTagCodes");
		}

		[Test]
		public void ValidateContentAssetsForEditor_RejectsUnknownStaticTagFromOfficialGasChoices()
		{
			CardDefinition knownTagContent = CreateCardDefinition("test.tag.known", XTag.Faction);
			CardDefinition unknownTagContent = CreateCardDefinition("test.tag.unknown", int.MaxValue);
			try
			{
				ContentValidationReport report = ContentValidationMenu.ValidateContentAssetsForEditor(
					new ContentAsset[] { knownTagContent, unknownTagContent });

				Assert.That(HasIssue(report, "CONTENT_TAG_UNKNOWN", unknownTagContent), Is.True);
				Assert.That(HasIssue(report, "CONTENT_TAG_UNKNOWN", knownTagContent), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(knownTagContent);
				Object.DestroyImmediate(unknownTagContent);
			}
		}

		private static void AssertFieldUsesOfficialDropdown(
			Type declaringType,
			string fieldName)
		{
			FieldInfo field = declaringType.GetField(
				fieldName,
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, $"{declaringType.Name}.{fieldName} 不存在。");
			ValueDropdownAttribute dropdown = field.GetCustomAttribute<ValueDropdownAttribute>();
			Assert.That(
				dropdown,
				Is.Not.Null,
				$"{declaringType.Name}.{fieldName} 仍允许作者手填 EX-GAS 标签码。");
			Assert.That(dropdown.MemberName, Is.EqualTo(ExGasTagDropdownSource));
			Assert.That(dropdown.IsUniqueList, Is.True);
			Assert.That(dropdown.HideChildProperties, Is.True);
		}

		private static CardDefinition CreateCardDefinition(string contentId, int tagCode)
		{
			CardDefinition content = ScriptableObject.CreateInstance<CardDefinition>();
			JsonUtility.FromJsonOverwrite(
				$"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}},\"m_tagCodes\":[{tagCode}]}}",
				content);
			return content;
		}

		private static bool HasIssue(
			ContentValidationReport report,
			string code,
			Object sourceObject)
		{
			for (int i = 0; i < report.Issues.Count; i++)
			{
				ContentValidationIssue issue = report.Issues[i];
				if (issue.Code == code && issue.SourceObject == sourceObject)
				{
					return true;
				}
			}
			return false;
		}
	}
}
