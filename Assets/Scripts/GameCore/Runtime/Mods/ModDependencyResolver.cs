using System;
using System.Collections.Generic;

namespace GameCore
{
	/// <summary>验证一组 Mod 清单，并生成依赖优先、结果确定的加载顺序。</summary>
	public static class ModDependencyResolver
	{
		/// <summary>删除请求提交前，拒绝仍被启用 Mod 依赖的目标。</summary>
		public static void RequireCanDelete(
			ModInfo target,
			IReadOnlyList<ModInfo> discoveredMods,
			ModConfig config)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}
			if (discoveredMods == null)
			{
				throw new ArgumentNullException(nameof(discoveredMods));
			}
			if (config == null)
			{
				throw new ArgumentNullException(nameof(config));
			}
			if (string.IsNullOrWhiteSpace(target.modId))
			{
				throw new InvalidOperationException("待删除 Mod 缺少稳定 Mod ID。");
			}

			for (int i = 0; i < discoveredMods.Count; i++)
			{
				ModInfo candidate = discoveredMods[i] ??
					throw new InvalidOperationException($"发现的第 {i + 1} 个 Mod 清单为空。");
				if (string.Equals(candidate.modId, target.modId, StringComparison.Ordinal) ||
					config.GetModState(candidate) != ModStatus.Enabled)
				{
					continue;
				}

				IReadOnlyList<ModDependency> dependencies = candidate.dependencies != null
					? candidate.dependencies
					: Array.Empty<ModDependency>();
				for (int dependencyIndex = 0; dependencyIndex < dependencies.Count; dependencyIndex++)
				{
					if (string.Equals(dependencies[dependencyIndex]?.modId, target.modId, StringComparison.Ordinal))
					{
						throw new InvalidOperationException(
							$"不能删除 Mod {target.modId}：启用的 Mod {candidate.modId} 仍依赖它。");
					}
				}
			}
		}

		public static IReadOnlyList<ModInfo> Resolve(
			IReadOnlyList<ModInfo> discoveredMods,
			ModConfig config)
		{
			if (discoveredMods == null)
			{
				throw new ArgumentNullException(nameof(discoveredMods));
			}
			if (config == null)
			{
				throw new ArgumentNullException(nameof(config));
			}

			var modsById = new Dictionary<string, ModInfo>(StringComparer.Ordinal);
			var packageOwners = new Dictionary<string, string>(StringComparer.Ordinal);
			for (int i = 0; i < discoveredMods.Count; i++)
			{
				ModInfo mod = discoveredMods[i] ??
					throw new InvalidOperationException($"发现的第 {i + 1} 个 Mod 清单为空。");
				ValidateManifest(mod);
				if (!modsById.TryAdd(mod.modId, mod))
				{
					throw new InvalidOperationException($"发现重复 Mod ID：{mod.modId}。");
				}
				if (!packageOwners.TryAdd(mod.packageName, mod.modId))
				{
					throw new InvalidOperationException(
						$"Mod {mod.modId} 与 {packageOwners[mod.packageName]} 使用了重复资源包名称 {mod.packageName}。");
				}
			}

			var enabled = new Dictionary<string, ModInfo>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, ModInfo> pair in modsById)
			{
				if (config.GetModState(pair.Value) == ModStatus.Enabled)
				{
					enabled.Add(pair.Key, pair.Value);
				}
			}

			foreach (ModInfo mod in enabled.Values)
			{
				IReadOnlyList<ModDependency> dependencies = mod.dependencies;
				for (int i = 0; i < dependencies.Count; i++)
				{
					ModDependency dependency = dependencies[i];
					if (!modsById.TryGetValue(dependency.modId, out ModInfo installed))
					{
						throw new InvalidOperationException($"Mod {mod.modId} 缺少依赖 {dependency.modId}。");
					}
					if (!enabled.ContainsKey(dependency.modId))
					{
						throw new InvalidOperationException($"Mod {mod.modId} 的依赖 {dependency.modId} 已被禁用。");
					}
					RequireCompatibleVersion(mod, dependency, installed);
				}
			}

			var dependencyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
			var dependents = new Dictionary<string, List<string>>(StringComparer.Ordinal);
			foreach (string modId in enabled.Keys)
			{
				dependencyCounts.Add(modId, enabled[modId].dependencies.Count);
				dependents.Add(modId, new List<string>());
			}
			foreach (ModInfo mod in enabled.Values)
			{
				for (int i = 0; i < mod.dependencies.Count; i++)
				{
					dependents[mod.dependencies[i].modId].Add(mod.modId);
				}
			}

			var available = new SortedSet<string>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, int> pair in dependencyCounts)
			{
				if (pair.Value == 0)
				{
					available.Add(pair.Key);
				}
			}

			var ordered = new List<ModInfo>(enabled.Count);
			while (available.Count > 0)
			{
				string modId = available.Min;
				available.Remove(modId);
				ordered.Add(enabled[modId]);
				List<string> directDependents = dependents[modId];
				directDependents.Sort(StringComparer.Ordinal);
				for (int i = 0; i < directDependents.Count; i++)
				{
					string dependentId = directDependents[i];
					dependencyCounts[dependentId]--;
					if (dependencyCounts[dependentId] == 0)
					{
						available.Add(dependentId);
					}
				}
			}

			if (ordered.Count != enabled.Count)
			{
				var cycleMembers = new List<string>();
				foreach (KeyValuePair<string, int> pair in dependencyCounts)
				{
					if (pair.Value > 0)
					{
						cycleMembers.Add(pair.Key);
					}
				}
				cycleMembers.Sort(StringComparer.Ordinal);
				throw new InvalidOperationException(
					$"Mod 依赖存在循环，涉及：{string.Join("、", cycleMembers)}。");
			}
			return ordered;
		}

		private static void ValidateManifest(ModInfo mod)
		{
			if (string.IsNullOrWhiteSpace(mod.modId))
			{
				throw new InvalidOperationException("Mod 清单缺少稳定 Mod ID。");
			}
			if (string.IsNullOrWhiteSpace(mod.packageName))
			{
				throw new InvalidOperationException($"Mod {mod.modId} 缺少 YooAsset 资源包名称。");
			}
			ParseVersion(mod.version, $"Mod {mod.modId} 的版本");
			mod.dependencies ??= new List<ModDependency>();
			var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < mod.dependencies.Count; i++)
			{
				ModDependency dependency = mod.dependencies[i] ??
					throw new InvalidOperationException($"Mod {mod.modId} 的第 {i + 1} 个依赖为空。");
				if (string.IsNullOrWhiteSpace(dependency.modId))
				{
					throw new InvalidOperationException($"Mod {mod.modId} 的第 {i + 1} 个依赖缺少 Mod ID。");
				}
				if (!dependencyIds.Add(dependency.modId))
				{
					throw new InvalidOperationException($"Mod {mod.modId} 重复声明依赖 {dependency.modId}。");
				}
				if (string.Equals(dependency.modId, mod.modId, StringComparison.Ordinal))
				{
					throw new InvalidOperationException($"Mod {mod.modId} 不能依赖自身。");
				}
				Version minimum = ParseOptionalVersion(dependency.minimumVersion, $"依赖 {dependency.modId} 的最低版本");
				Version maximum = ParseOptionalVersion(dependency.maximumVersion, $"依赖 {dependency.modId} 的最高版本");
				if (minimum != null && maximum != null && minimum > maximum)
				{
					throw new InvalidOperationException($"Mod {mod.modId} 对依赖 {dependency.modId} 的版本范围上下限颠倒。");
				}
			}
		}

		private static void RequireCompatibleVersion(
			ModInfo owner,
			ModDependency dependency,
			ModInfo installed)
		{
			Version actual = ParseVersion(installed.version, $"Mod {installed.modId} 的版本");
			Version minimum = ParseOptionalVersion(dependency.minimumVersion, $"依赖 {dependency.modId} 的最低版本");
			Version maximum = ParseOptionalVersion(dependency.maximumVersion, $"依赖 {dependency.modId} 的最高版本");
			if ((minimum != null && actual < minimum) || (maximum != null && actual > maximum))
			{
				string range = $"[{dependency.minimumVersion ?? "不限"}, {dependency.maximumVersion ?? "不限"}]";
				throw new InvalidOperationException(
					$"Mod {owner.modId} 要求依赖 {dependency.modId} 版本位于 {range}，当前为 {installed.version}。");
			}
		}

		private static Version ParseOptionalVersion(string value, string label) =>
			string.IsNullOrWhiteSpace(value) ? null : ParseVersion(value, label);

		private static Version ParseVersion(string value, string label)
		{
			if (!Version.TryParse(value, out Version version))
			{
				throw new InvalidOperationException($"{label}不是有效版本号：{value ?? "<null>"}。");
			}
			return version;
		}
	}
}
