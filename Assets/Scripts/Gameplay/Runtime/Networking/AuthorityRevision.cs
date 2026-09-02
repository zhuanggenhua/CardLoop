using System;
using UnityEngine;

namespace Gameplay.Networking
{
	/// <summary>房主 / 服务器确认后的权威状态版本；客户端命令用它声明自己基于哪个状态发起。</summary>
	[Serializable]
	public struct AuthorityRevision : IEquatable<AuthorityRevision>, IComparable<AuthorityRevision>
	{
		[SerializeField]
		private ulong m_value;

		public ulong Value => m_value;

		public bool IsInitial => m_value == 0uL;

		public AuthorityRevision(ulong value)
		{
			m_value = value;
		}

		public AuthorityRevision Next()
		{
			return new AuthorityRevision(checked(m_value + 1uL));
		}

		public int CompareTo(AuthorityRevision other)
		{
			return m_value.CompareTo(other.m_value);
		}

		public bool Equals(AuthorityRevision other)
		{
			return m_value == other.m_value;
		}

		public override bool Equals(object obj)
		{
			return obj is AuthorityRevision other && Equals(other);
		}

		public override int GetHashCode()
		{
			return m_value.GetHashCode();
		}

		public override string ToString()
		{
			return m_value.ToString();
		}

		public static bool operator ==(AuthorityRevision left, AuthorityRevision right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(AuthorityRevision left, AuthorityRevision right)
		{
			return !left.Equals(right);
		}
	}
}
