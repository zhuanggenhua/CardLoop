using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace GameCore.Tests
{
	public sealed class ModDependencyResolverEditModeTests
	{
		[Test]
		public void ModLoader_RejectsMissingRequiredCollaboratorsAtConstruction()
		{
			Assert.Throws<ArgumentNullException>(() =>
				new ModLoader(null, new APIValidator(ModAPI.DefaultAPIVersion)));
			Assert.Throws<ArgumentNullException>(() =>
				new ModLoader(new ModConfig(), null));
		}

		[Test]
		public void ApiValidator_WhenConfiguredApiVersionIsInvalid_ThrowsInsteadOfUsingFallbackVersion()
		{
			ArgumentException exception = Assert.Throws<ArgumentException>(
				() => new APIValidator("not-a-version"));

			StringAssert.Contains("Mod API 版本", exception.Message);
			StringAssert.Contains("not-a-version", exception.Message);
		}

		[Test]
		public void LoadAllMods_WhenManifestContainsNull_ThrowsInsteadOfSkippingDirectory()
		{
			string root = Path.Combine(Path.GetTempPath(), "CardLoopModManifestTests", Guid.NewGuid().ToString("N"));
			string modDirectory = Path.Combine(root, "BrokenMod");
			Directory.CreateDirectory(modDirectory);
			try
			{
				string manifestPath = Path.Combine(modDirectory, "broken.cfg");
				File.WriteAllText(manifestPath, "null");
				InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
					ModAPI.LoadModInfo(manifestPath));

				StringAssert.Contains(manifestPath, exception.Message);
				StringAssert.Contains("有效 Mod 清单", exception.Message);
			}
			finally
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, true);
				}
			}
		}

		[Test]
		public void LoadModInfo_WhenJsonIsMalformed_ReportsManifestPath()
		{
			string root = Path.Combine(Path.GetTempPath(), "CardLoopModManifestTests", Guid.NewGuid().ToString("N"));
			string manifestPath = Path.Combine(root, "broken.cfg");
			Directory.CreateDirectory(root);
			File.WriteAllText(manifestPath, "{ invalid json");
			try
			{
				InvalidDataException exception = Assert.Throws<InvalidDataException>(
					() => ModAPI.LoadModInfo(manifestPath));

				StringAssert.Contains(manifestPath, exception.Message);
				StringAssert.Contains("有效 JSON", exception.Message);
			}
			finally
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, true);
				}
			}
		}

		[Test]
		public void LoadAllMods_WhenOneDirectoryContainsMultipleManifests_ThrowsInsteadOfPickingOne()
		{
			string root = Path.Combine(Path.GetTempPath(), "CardLoopModManifestTests", Guid.NewGuid().ToString("N"));
			string modDirectory = Path.Combine(root, "ExampleMod");
			Directory.CreateDirectory(modDirectory);
			try
			{
				const string manifest =
					"{\"modId\":\"author.example\",\"apiVersion\":\"0.1.0\",\"version\":\"1.0.0\",\"packageName\":\"ExamplePackage\"}";
				File.WriteAllText(Path.Combine(modDirectory, "first.cfg"), manifest);
				File.WriteAllText(Path.Combine(modDirectory, "second.cfg"), manifest);
				var config = new ModConfig { LoadingPath = root };
				var loader = new ModLoader(config, new APIValidator(ModAPI.DefaultAPIVersion));

				InvalidDataException exception = Assert.Throws<InvalidDataException>(
					() => loader.LoadAllModsAsync(new List<ModInfo>(), CancellationToken.None).GetAwaiter().GetResult());

				StringAssert.Contains("多个 Mod 清单", exception.Message);
				StringAssert.Contains("first.cfg", exception.Message);
				StringAssert.Contains("second.cfg", exception.Message);
			}
			finally
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, true);
				}
			}
		}

		[Test]
		public void LoadAllMods_WhenManifestIsPlacedAtRoot_ThrowsInsteadOfIgnoringIt()
		{
			string root = Path.Combine(Path.GetTempPath(), "CardLoopModManifestTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			string manifestPath = Path.Combine(root, "loose.cfg");
			File.WriteAllText(manifestPath, "{}");
			try
			{
				var config = new ModConfig { LoadingPath = root };
				var loader = new ModLoader(config, new APIValidator(ModAPI.DefaultAPIVersion));
				InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
					loader.LoadAllModsAsync(new List<ModInfo>(), CancellationToken.None).GetAwaiter().GetResult());

				StringAssert.Contains("独立子目录", exception.Message);
				StringAssert.Contains(manifestPath, exception.Message);
			}
			finally
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, true);
				}
			}
		}

		[Test]
		public void ModConfig_UsesStableModIdAcrossVersionUpgrade()
		{
			var config = new ModConfig();
			ModInfo versionOne = CreateMod("author.survival", "1.0.0", "SurvivalV1");
			ModInfo versionTwo = CreateMod("author.survival", "2.0.0", "SurvivalV2");

			config.SetModEnabled(versionOne, false);

			Assert.That(config.GetModState(versionTwo), Is.EqualTo(ModStatus.Disabled));
			Assert.That(config.States, Has.Count.EqualTo(1));
			Assert.That(config.States[0].modId, Is.EqualTo("author.survival"));
		}

		[Test]
		public void Resolve_OrdersDependenciesBeforeDependentsAndPeersByStableId()
		{
			ModInfo core = CreateMod("author.core", "1.0.0", "Core");
			ModInfo world = CreateMod("author.world", "1.2.0", "World", DependsOn("author.core", "1.0.0"));
			ModInfo addon = CreateMod("author.addon", "2.0.0", "Addon", DependsOn("author.world", "1.0.0", "2.0.0"));
			ModInfo cosmetic = CreateMod("author.cosmetic", "1.0.0", "Cosmetic");

			IReadOnlyList<ModInfo> ordered = ModDependencyResolver.Resolve(
				new[] { world, addon, cosmetic, core },
				new ModConfig());

			Assert.That(ordered, Is.EqualTo(new[] { core, cosmetic, world, addon }));
		}

		[Test]
		public void Resolve_WhenDependencyIsMissing_ThrowsWithBothModIds()
		{
			ModInfo addon = CreateMod("author.addon", "1.0.0", "Addon", DependsOn("author.core"));

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => ModDependencyResolver.Resolve(new[] { addon }, new ModConfig()));

			StringAssert.Contains("author.addon", exception.Message);
			StringAssert.Contains("author.core", exception.Message);
		}

		[Test]
		public void Resolve_WhenDependencyIsDisabled_ThrowsInsteadOfSilentlySkippingDependent()
		{
			ModInfo core = CreateMod("author.core", "1.0.0", "Core");
			ModInfo addon = CreateMod("author.addon", "1.0.0", "Addon", DependsOn("author.core"));
			var config = new ModConfig();
			config.SetModEnabled(core, false);

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => ModDependencyResolver.Resolve(new[] { addon, core }, config));

			StringAssert.Contains("禁用", exception.Message);
			StringAssert.Contains("author.core", exception.Message);
		}

		[Test]
		public void RequireCanDelete_WhenEnabledDependentExists_RejectsBeforeChangingConfig()
		{
			ModInfo core = CreateMod("author.core", "1.0.0", "Core");
			ModInfo addon = CreateMod("author.addon", "1.0.0", "Addon", DependsOn("author.core"));
			var config = new ModConfig();

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
				ModDependencyResolver.RequireCanDelete(core, new[] { core, addon }, config));

			StringAssert.Contains("author.core", exception.Message);
			StringAssert.Contains("author.addon", exception.Message);
			Assert.That(config.GetModState(core), Is.EqualTo(ModStatus.Enabled));
		}

		[Test]
		public void LoadAllMods_WhenEnabledModDependsOnPendingDeletion_RejectsBeforeDeletingEitherDirectory()
		{
			string root = Path.Combine(Path.GetTempPath(), "CardLoopModDeletionTests", Guid.NewGuid().ToString("N"));
			ModInfo core = CreateMod("author.core", "1.0.0", "Core");
			ModInfo addon = CreateMod("author.addon", "1.0.0", "Addon", DependsOn("author.core"));
			WriteManifest(root, "Core", core);
			WriteManifest(root, "Addon", addon);
			var config = new ModConfig { LoadingPath = root };
			config.DeleteMod(core);
			var loader = new ModLoader(config, new APIValidator(ModAPI.DefaultAPIVersion));

			try
			{
				InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
					loader.LoadAllModsAsync(new List<ModInfo>(), CancellationToken.None).GetAwaiter().GetResult());

				StringAssert.Contains("author.core", exception.Message);
				Assert.That(Directory.Exists(Path.Combine(root, "Core")), Is.True);
				Assert.That(Directory.Exists(Path.Combine(root, "Addon")), Is.True);
				Assert.That(config.GetModState(core), Is.EqualTo(ModStatus.Delete));
			}
			finally
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, true);
				}
			}
		}

		[Test]
		public void DeletePendingMods_WhenAnyPathIsOutsideRoot_KeepsEveryDirectoryAndDeleteState()
		{
			string parent = Path.Combine(Path.GetTempPath(), "CardLoopModDeletionTests", Guid.NewGuid().ToString("N"));
			string root = Path.Combine(parent, "Mods");
			string validDirectory = Path.Combine(root, "Valid");
			string outsideDirectory = Path.Combine(parent, "Outside");
			Directory.CreateDirectory(validDirectory);
			Directory.CreateDirectory(outsideDirectory);
			ModInfo valid = CreateMod("author.valid", "1.0.0", "Valid");
			valid.FilePath = validDirectory;
			ModInfo outside = CreateMod("author.outside", "1.0.0", "Outside");
			outside.FilePath = outsideDirectory;
			var config = new ModConfig { LoadingPath = root };
			config.DeleteMod(valid);
			config.DeleteMod(outside);
			var loader = new ModLoader(config, new APIValidator(ModAPI.DefaultAPIVersion));

			try
			{
				Assert.Throws<InvalidDataException>(() => loader.DeletePendingMods(new[] { valid, outside }));

				Assert.That(Directory.Exists(validDirectory), Is.True);
				Assert.That(Directory.Exists(outsideDirectory), Is.True);
				Assert.That(config.GetModState(valid), Is.EqualTo(ModStatus.Delete));
				Assert.That(config.GetModState(outside), Is.EqualTo(ModStatus.Delete));
			}
			finally
			{
				if (Directory.Exists(parent))
				{
					Directory.Delete(parent, true);
				}
			}
		}

		[Test]
		public void DeletePendingMods_AfterDirectoryDeletion_ConsumesDeleteState()
		{
			string root = Path.Combine(Path.GetTempPath(), "CardLoopModDeletionTests", Guid.NewGuid().ToString("N"));
			string directory = Path.Combine(root, "Example");
			Directory.CreateDirectory(directory);
			ModInfo mod = CreateMod("author.example", "1.0.0", "Example");
			mod.FilePath = directory;
			var config = new ModConfig { LoadingPath = root };
			config.DeleteMod(mod);
			var loader = new ModLoader(config, new APIValidator(ModAPI.DefaultAPIVersion));

			try
			{
				loader.DeletePendingMods(new[] { mod });

				Assert.That(Directory.Exists(directory), Is.False);
				Assert.That(config.States, Is.Empty);
			}
			finally
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, true);
				}
			}
		}

		[Test]
		public void LoadAllMods_WhenPreviousDeletionAlreadyRemovedDirectory_ConsumesOnlyDeleteState()
		{
			string root = Path.Combine(Path.GetTempPath(), "CardLoopModDeletionTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			ModInfo deleted = CreateMod("author.deleted", "1.0.0", "Deleted");
			ModInfo disabledMissing = CreateMod("author.disabled", "1.0.0", "Disabled");
			var config = new ModConfig { LoadingPath = root };
			config.DeleteMod(deleted);
			config.SetModEnabled(disabledMissing, false);
			var loader = new ModLoader(config, new APIValidator(ModAPI.DefaultAPIVersion));

			try
			{
				loader.LoadAllModsAsync(new List<ModInfo>(), CancellationToken.None).GetAwaiter().GetResult();

				Assert.That(config.States, Has.Count.EqualTo(1));
				Assert.That(config.GetModState(disabledMissing), Is.EqualTo(ModStatus.Disabled));
				Assert.That(config.States[0].modId, Is.EqualTo("author.disabled"));
			}
			finally
			{
				if (Directory.Exists(root))
				{
					Directory.Delete(root, true);
				}
			}
		}

		[Test]
		public void Resolve_WhenDependencyVersionIsOutsideRange_ThrowsWithRequiredRange()
		{
			ModInfo core = CreateMod("author.core", "2.0.0", "Core");
			ModInfo addon = CreateMod(
				"author.addon",
				"1.0.0",
				"Addon",
				DependsOn("author.core", "1.0.0", "1.9.9"));

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => ModDependencyResolver.Resolve(new[] { addon, core }, new ModConfig()));

			StringAssert.Contains("1.0.0", exception.Message);
			StringAssert.Contains("1.9.9", exception.Message);
		}

		[Test]
		public void Resolve_WhenDependencyCycleExists_ThrowsWithCyclePath()
		{
			ModInfo first = CreateMod("author.first", "1.0.0", "First", DependsOn("author.second"));
			ModInfo second = CreateMod("author.second", "1.0.0", "Second", DependsOn("author.first"));

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => ModDependencyResolver.Resolve(new[] { first, second }, new ModConfig()));

			StringAssert.Contains("author.first", exception.Message);
			StringAssert.Contains("author.second", exception.Message);
			StringAssert.Contains("循环", exception.Message);
		}

		[TestCase(true)]
		[TestCase(false)]
		public void Resolve_WhenStableIdentityOrPackageNameIsDuplicated_Throws(bool duplicateModId)
		{
			ModInfo first = CreateMod("author.first", "1.0.0", "SharedPackage");
			ModInfo second = CreateMod(
				duplicateModId ? "author.first" : "author.second",
				"1.0.0",
				duplicateModId ? "OtherPackage" : "SharedPackage");

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => ModDependencyResolver.Resolve(new[] { first, second }, new ModConfig()));

			StringAssert.Contains(duplicateModId ? "Mod ID" : "资源包名称", exception.Message);
		}

		private static ModInfo CreateMod(
			string modId,
			string version,
			string packageName,
			params ModDependency[] dependencies)
		{
			return new ModInfo
			{
				modId = modId,
				apiVersion = ModAPI.DefaultAPIVersion,
				modName = modId,
				version = version,
				packageName = packageName,
				dependencies = new List<ModDependency>(dependencies)
			};
		}

		private static ModDependency DependsOn(
			string modId,
			string minimumVersion = null,
			string maximumVersion = null)
		{
			return new ModDependency
			{
				modId = modId,
				minimumVersion = minimumVersion,
				maximumVersion = maximumVersion
			};
		}

		private static void WriteManifest(string root, string directoryName, ModInfo mod)
		{
			string directory = Path.Combine(root, directoryName);
			Directory.CreateDirectory(directory);
			string dependencyJson = mod.dependencies.Count == 0
				? string.Empty
				: $"{{\"modId\":\"{mod.dependencies[0].modId}\"}}";
			File.WriteAllText(
				Path.Combine(directory, "manifest.cfg"),
				$"{{\"modId\":\"{mod.modId}\",\"apiVersion\":\"{mod.apiVersion}\"," +
				$"\"modName\":\"{mod.modName}\",\"version\":\"{mod.version}\"," +
				$"\"packageName\":\"{mod.packageName}\",\"dependencies\":[{dependencyJson}]}}");
		}
	}
}
