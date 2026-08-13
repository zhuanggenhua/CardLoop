using System;
using UnityEngine;

namespace Gameplay.Actions
{
	/// <summary>
	/// 标记字符串字段引用所属行动的参与槽位；编辑器保存槽位已有的内部稳定键。
	/// 该特性不新增运行时身份，也不改变单槽位行动的自动推导规则。
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class ActionSlotReferenceAttribute : PropertyAttribute
	{
	}
}
