using System;
using Sirenix.OdinInspector;
using UnityEngine;
using System.Globalization;
using System.Text;

namespace Gameplay.Content
{
	/// <summary>
	/// Gameplay 内容在存档、联机、Mod 和作者引用中的唯一身份值。
	/// </summary>
	[Serializable]
	public struct ContentId : IEquatable<ContentId>
	{
		[SerializeField]
		[LabelText("内容 ID")]
		[Tooltip("Gameplay 内容的唯一作者 ID。不要填写 Unity GUID、YooAsset 地址、文件路径或运行时实例号。")]
		private string m_value;

		public string Value => m_value ?? string.Empty;

		public bool IsValid => ContentIdRules.IsValidKey(Value);

		public ContentId(string value)
		{
			m_value = value;
		}

		public bool Equals(ContentId other)
		{
			return string.Equals(Value, other.Value, StringComparison.Ordinal);
		}

		public override bool Equals(object obj)
		{
			return obj is ContentId other && Equals(other);
		}

		public override int GetHashCode()
		{
			return StringComparer.Ordinal.GetHashCode(Value);
		}

		public override string ToString()
		{
			return Value;
		}

		public static implicit operator string(ContentId id)
		{
			return id.Value;
		}

		public static implicit operator ContentId(string value)
		{
			return new ContentId(value);
		}
	}

	/// <summary>
	/// 统一校验和生成内容 ID；Unity GUID 仅可作为首次生成时的稳定种子。
	/// </summary>
	public static class ContentIdRules
	{
		public static bool IsValidKey(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
			{
				return false;
			}
			foreach (char c in value)
			{
				if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
				{
					return false;
				}
			}
			return true;
		}

		public static string CreateGeneratedContentId(string fileNameWithoutExtension, string stableSeed)
		{
			string readableSegment = SanitizeKeySegment(fileNameWithoutExtension, "content");
			string hashSegment = CreateStableHash(stableSeed, 10);
			return readableSegment + "." + hashSegment;
		}

		private static string SanitizeKeySegment(string value, string fallback)
		{
			StringBuilder builder = new StringBuilder();
			bool wroteSeparator = false;
			string source = (string.IsNullOrWhiteSpace(value) ? fallback : value.Trim());
			foreach (char c in source)
			{
				if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
				{
					builder.Append(c);
					wroteSeparator = false;
				}
				else if (!wroteSeparator && builder.Length > 0)
				{
					builder.Append('-');
					wroteSeparator = true;
				}
			}
			while (builder.Length > 0)
			{
				if (builder[builder.Length - 1] != '-')
				{
					break;
				}
				builder.Length--;
			}
			return (builder.Length == 0) ? fallback : builder.ToString();
		}

		private static string CreateStableHash(string value, int length)
		{
			ulong hash = 14695981039346656037uL;
			string source = (string.IsNullOrEmpty(value) ? "content" : value);
			for (int i = 0; i < source.Length; i++)
			{
				hash ^= source[i];
				hash *= 1099511628211L;
			}
			string hex = hash.ToString("x16", CultureInfo.InvariantCulture);
			return hex[..Math.Max(1, Math.Min(length, hex.Length))];
		}
	}
}
