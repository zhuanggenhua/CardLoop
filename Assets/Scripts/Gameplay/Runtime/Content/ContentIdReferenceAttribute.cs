using System;
using UnityEngine;

namespace Gameplay.Content
{
	/// <summary>
	/// 标记内容作者字段由编辑器选择内容资产，并把选择结果保存为唯一内容 ID。
	/// 该特性不增加对象引用字段，也不改变运行时序列化协议。
	/// </summary>
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class ContentIdReferenceAttribute : PropertyAttribute
	{
		public Type ContentType { get; }

		public ContentIdReferenceAttribute()
			: this(typeof(ContentAsset))
		{
		}

		public ContentIdReferenceAttribute(Type contentType)
		{
			if (contentType == null || !typeof(ContentAsset).IsAssignableFrom(contentType))
			{
				throw new ArgumentException("内容引用类型必须继承 ContentAsset。", nameof(contentType));
			}
			ContentType = contentType;
		}
	}
}
