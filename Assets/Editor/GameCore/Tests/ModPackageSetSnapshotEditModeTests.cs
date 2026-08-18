using System;
using NUnit.Framework;

namespace GameCore.Tests
{
	public sealed class ModPackageSetSnapshotEditModeTests
	{
		[Test]
		public void RequireExactMatch_AcceptsSamePackagesRegardlessOfInputOrder()
		{
			ModPackageSetSnapshot saved = CreateSet(
				CreatePackage("author.world", "1.0.0", "hash-world", "manifest-world"),
				CreatePackage("author.core", "2.0.0", "hash-core", "manifest-core"));
			ModPackageSetSnapshot current = CreateSet(
				CreatePackage("author.core", "2.0.0", "hash-core", "manifest-core"),
				CreatePackage("author.world", "1.0.0", "hash-world", "manifest-world"));

			Assert.DoesNotThrow(() => current.RequireExactMatch(saved));
			Assert.That(current.Packages[0].ModId, Is.EqualTo("author.core"));
		}

		[TestCase("version")]
		[TestCase("hash")]
		[TestCase("manifest")]
		[TestCase("missing")]
		[TestCase("extra")]
		public void RequireExactMatch_RejectsAnyDifferentActiveModFact(string difference)
		{
			ModPackageSetSnapshot saved = CreateSet(
				CreatePackage("author.core", "1.0.0", "hash-a", "manifest-a"));
			ModPackageSetSnapshot current = difference switch
			{
				"version" => CreateSet(CreatePackage("author.core", "2.0.0", "hash-a", "manifest-a")),
				"hash" => CreateSet(CreatePackage("author.core", "1.0.0", "hash-b", "manifest-a")),
				"manifest" => CreateSet(CreatePackage("author.core", "1.0.0", "hash-a", "manifest-b")),
				"missing" => CreateSet(),
				"extra" => CreateSet(
					CreatePackage("author.core", "1.0.0", "hash-a", "manifest-a"),
					CreatePackage("author.extra", "1.0.0", "hash-extra", "manifest-extra")),
				_ => throw new ArgumentOutOfRangeException(nameof(difference))
			};

			InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
				() => current.RequireExactMatch(saved));

			StringAssert.Contains("Mod", exception.Message);
		}

		private static ModPackageSetSnapshot CreateSet(params ModPackageSnapshot[] packages) =>
			new ModPackageSetSnapshot(packages);

		private static ModPackageSnapshot CreatePackage(
			string modId,
			string version,
			string packageHash,
			string manifestVersion) =>
			new ModPackageSnapshot(modId, version, packageHash, manifestVersion);
	}
}
