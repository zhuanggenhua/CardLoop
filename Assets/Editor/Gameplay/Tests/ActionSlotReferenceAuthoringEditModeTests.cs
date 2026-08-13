using System;
using System.Reflection;
using Gameplay.Actions;
using Gameplay.Tabletop.Actions;
using NUnit.Framework;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证行动结果引用通过当前行动的槽位选择器维护，而不是要求作者手填内部键。
	/// </summary>
	public sealed class ActionSlotReferenceAuthoringEditModeTests
	{
		[Test]
		public void ResultSlotFields_DeclareVisibleActionSlotSelector()
		{
			Type referenceAttributeType = Type.GetType(
				"Gameplay.Actions.ActionSlotReferenceAttribute, Gameplay.Runtime");
			Assert.That(
				referenceAttributeType,
				Is.Not.Null,
				"缺少行动槽位引用作者入口，结果字段仍只能依赖隐藏字符串。\n");

			AssertSlotReferenceField<RemoveCardsResultIntent>(
				"m_slotKey",
				"移除槽位",
				referenceAttributeType);
			AssertSlotReferenceField<CreateCardsResultIntent>(
				"m_anchorSlotKey",
				"生成位置",
				referenceAttributeType);
		}

		[Test]
		public void SlotSelector_UsesExistingKeysAndOnlyAllowsSingleSlotInference()
		{
			ActionDefinition multiSlotAction = CreateActionDefinition(
				"test.action.multi-slot",
				"[{\"m_key\":\"actor\",\"m_displayName\":\"行动者\"},{\"m_key\":\"target\",\"m_displayName\":\"目标\"}]");
			ActionDefinition singleSlotAction = CreateActionDefinition(
				"test.action.single-slot",
				"[{\"m_key\":\"participant\",\"m_displayName\":\"参与者\"}]");
			try
			{
				SerializedObject multiSerialized = CreateActionWithRemoveIntent(multiSlotAction);
				SerializedProperty multiSlotKey = FindRemoveSlotKey(multiSerialized);
				InvokeAssignReference(
					multiSlotKey,
					multiSlotAction,
					multiSlotAction.ParticipationSlots[1].Key);
				Assert.That(multiSlotKey.stringValue, Is.EqualTo("target"));

				TargetInvocationException multiSlotException = Assert.Throws<TargetInvocationException>(() =>
					InvokeAssignReference(multiSlotKey, multiSlotAction, string.Empty));
				Assert.That(multiSlotException.InnerException, Is.TypeOf<InvalidOperationException>());
				Assert.That(multiSlotKey.stringValue, Is.EqualTo("target"));

				SerializedObject singleSerialized = CreateActionWithRemoveIntent(singleSlotAction);
				SerializedProperty singleSlotKey = FindRemoveSlotKey(singleSerialized);
				InvokeAssignReference(singleSlotKey, singleSlotAction, string.Empty);
				Assert.That(singleSlotKey.stringValue, Is.Empty);
			}
			finally
			{
				Object.DestroyImmediate(multiSlotAction);
				Object.DestroyImmediate(singleSlotAction);
			}
		}

		private static void AssertSlotReferenceField<TDeclaring>(
			string fieldName,
			string expectedLabel,
			Type referenceAttributeType)
		{
			FieldInfo field = typeof(TDeclaring).GetField(
				fieldName,
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, $"{typeof(TDeclaring).Name}.{fieldName} 不存在。");
			Assert.That(
				field.GetCustomAttribute(typeof(HideInInspector)),
				Is.Null,
				$"{typeof(TDeclaring).Name}.{fieldName} 仍被隐藏，作者无法选择槽位。");
			Assert.That(
				field.GetCustomAttribute(referenceAttributeType),
				Is.Not.Null,
				$"{typeof(TDeclaring).Name}.{fieldName} 没有使用行动槽位选择入口。");

			LabelTextAttribute label = field.GetCustomAttribute<LabelTextAttribute>();
			Assert.That(label, Is.Not.Null, $"{typeof(TDeclaring).Name}.{fieldName} 缺少作者可读标签。");
			Assert.That(label.Text, Is.EqualTo(expectedLabel));
		}

		private static ActionDefinition CreateActionDefinition(string contentId, string slotsJson)
		{
			ActionDefinition action = ScriptableObject.CreateInstance<ActionDefinition>();
			JsonUtility.FromJsonOverwrite(
				$"{{\"m_contentId\":{{\"m_value\":\"{contentId}\"}},\"m_participationSlots\":{slotsJson}}}",
				action);
			return action;
		}

		private static SerializedObject CreateActionWithRemoveIntent(ActionDefinition action)
		{
			SerializedObject serializedAction = new SerializedObject(action);
			SerializedProperty intents = serializedAction.FindProperty("m_resultIntents");
			intents.arraySize = 1;
			intents.GetArrayElementAtIndex(0).managedReferenceValue = new RemoveCardsResultIntent();
			serializedAction.ApplyModifiedPropertiesWithoutUndo();
			serializedAction.Update();
			return serializedAction;
		}

		private static SerializedProperty FindRemoveSlotKey(SerializedObject serializedAction)
		{
			return serializedAction
				.FindProperty("m_resultIntents")
				.GetArrayElementAtIndex(0)
				.FindPropertyRelative("m_slotKey");
		}

		private static void InvokeAssignReference(
			SerializedProperty slotKeyProperty,
			ActionDefinition action,
			string selectedKey)
		{
			Type drawerType = Type.GetType(
				"Gameplay.Editor.Actions.ActionSlotReferenceDrawer, Gameplay.Editor");
			Assert.That(drawerType, Is.Not.Null, "缺少行动槽位引用的 Inspector 选择器。");
			MethodInfo assignReference = drawerType.GetMethod(
				"AssignReference",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(assignReference, Is.Not.Null, "槽位选择器没有统一的序列化写入口。");
			assignReference.Invoke(null, new object[] { slotKeyProperty, action, selectedKey });
		}
	}
}
