using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
	/// <summary>一个已启用 Mod 在单局开始时实际加载的版本事实。</summary>
	[Serializable]
	public sealed class ModPackageSnapshot
	{
		[SerializeField] private string m_modId;
		[SerializeField] private string m_version;
		[SerializeField] private string m_packageHash;
		[SerializeField] private string m_manifestVersion;

		public string ModId => m_modId;
		public string Version => m_version;
		public string PackageHash => m_packageHash;
		public string ManifestVersion => m_manifestVersion;

		public ModPackageSnapshot(
			string modId,
			string version,
			string packageHash,
			string manifestVersion)
		{
			m_modId = RequireValue(modId, "Mod ID");
			m_version = RequireValue(version, $"Mod {m_modId} 的版本");
			m_packageHash = RequireValue(packageHash, $"Mod {m_modId} 的 YooAsset 包哈希");
			m_manifestVersion = RequireValue(manifestVersion, $"Mod {m_modId} 的 YooAsset 清单版本");
		}

		internal void Validate()
		{
			RequireValue(m_modId, "Mod ID");
			RequireValue(m_version, $"Mod {m_modId} 的版本");
			RequireValue(m_packageHash, $"Mod {m_modId} 的 YooAsset 包哈希");
			RequireValue(m_manifestVersion, $"Mod {m_modId} 的 YooAsset 清单版本");
		}

		private static string RequireValue(string value, string label)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				throw new InvalidOperationException($"{label}不能为空。");
			}
			return value;
		}
	}

	/// <summary>一局游戏冻结的完整启用 Mod 集合；读档要求集合及版本事实严格一致。</summary>
	[Serializable]
	public sealed class ModPackageSetSnapshot
	{
		[SerializeField] private ModPackageSnapshot[] m_packages;

		public IReadOnlyList<ModPackageSnapshot> Packages => m_packages;

		public ModPackageSetSnapshot(IReadOnlyList<ModPackageSnapshot> packages)
		{
			if (packages == null)
			{
				throw new ArgumentNullException(nameof(packages));
			}
			m_packages = new ModPackageSnapshot[packages.Count];
			var ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < packages.Count; i++)
			{
				ModPackageSnapshot package = packages[i] ??
					throw new InvalidOperationException($"Mod 包集合的第 {i + 1} 项为空。");
				package.Validate();
				if (!ids.Add(package.ModId))
				{
					throw new InvalidOperationException($"Mod 包集合重复包含 {package.ModId}。");
				}
				m_packages[i] = package;
			}
			Array.Sort(m_packages, (left, right) => string.CompareOrdinal(left.ModId, right.ModId));
		}

		public void RequireExactMatch(ModPackageSetSnapshot required)
		{
			if (required == null)
			{
				throw new InvalidOperationException("存档缺少 Mod 包集合快照。");
			}
			ValidateSerializedState();
			required.ValidateSerializedState();
			if (m_packages.Length != required.m_packages.Length)
			{
				throw new InvalidOperationException(
					$"当前启用 Mod 数量为 {m_packages.Length}，存档要求 {required.m_packages.Length}，不能恢复单局。");
			}
			for (int i = 0; i < m_packages.Length; i++)
			{
				ModPackageSnapshot current = m_packages[i];
				ModPackageSnapshot saved = required.m_packages[i];
				if (!string.Equals(current.ModId, saved.ModId, StringComparison.Ordinal) ||
					!string.Equals(current.Version, saved.Version, StringComparison.Ordinal) ||
					!string.Equals(current.PackageHash, saved.PackageHash, StringComparison.Ordinal) ||
					!string.Equals(current.ManifestVersion, saved.ManifestVersion, StringComparison.Ordinal))
				{
					throw new InvalidOperationException(
						$"当前 Mod {current.ModId} 的版本事实与存档要求不一致。"
						+ $" 当前：{current.Version}/{current.PackageHash}/{current.ManifestVersion}；"
						+ $"存档：{saved.ModId}/{saved.Version}/{saved.PackageHash}/{saved.ManifestVersion}。");
				}
			}
		}

		private void ValidateSerializedState()
		{
			if (m_packages == null)
			{
				throw new InvalidOperationException("Mod 包集合没有序列化内容。");
			}
			for (int i = 0; i < m_packages.Length; i++)
			{
				if (m_packages[i] == null)
				{
					throw new InvalidOperationException($"Mod 包集合的第 {i + 1} 项为空。");
				}
				m_packages[i].Validate();
				if (i > 0 && string.CompareOrdinal(m_packages[i - 1].ModId, m_packages[i].ModId) >= 0)
				{
					throw new InvalidOperationException("Mod 包集合未按稳定 Mod ID 唯一排序。");
				}
			}
		}
	}
}
