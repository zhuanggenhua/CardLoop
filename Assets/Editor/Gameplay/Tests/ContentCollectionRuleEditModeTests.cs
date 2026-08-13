using Gameplay.Content;
using Gameplay.Editor.Content;
using NUnit.Framework;
using UnityEditor;
using YooAsset.Editor;

namespace Gameplay.Tests
{
	/// <summary>
	/// 验证 YooAsset 构建期只按内容作者类型收集，并且不为内容定义生成第二资源地址。
	/// </summary>
	public sealed class ContentCollectionRuleEditModeTests
	{
		private const string DefaultPackageName = "DefaultPackage";
		private const string ContentTag = "gameplay-content";
		private const string FoundationCardPath = "Assets/Gameplay/Tests/地基测试卡牌.asset";

		[Test]
		public void DefaultPackage_CollectsContentAssetsByTagWithoutAddress()
		{
			ContentAssetFilterRule filterRule = new ContentAssetFilterRule();
			Assert.That(filterRule.FindAssetType, Is.EqualTo(nameof(ContentAsset)));
			Assert.That(
				filterRule.IsCollectAsset(new AssetFilterRuleData(
					FoundationCardPath,
					"Assets",
					"Gameplay内容定义",
					string.Empty)),
				Is.True);

			CollectResult result = BundleCollectorSettingData.Setting.BeginCollect(
				DefaultPackageName,
				simulateBuild: true,
				useAssetDependencyDB: false);
			bool foundFoundationCard = false;
			int contentAssetCount = 0;
			for (int i = 0; i < result.CollectAssets.Count; i++)
			{
				CollectAssetInfo collected = result.CollectAssets[i];
				if (!collected.AssetTags.Contains(ContentTag))
				{
					continue;
				}

				contentAssetCount++;
				ContentAsset content = AssetDatabase.LoadAssetAtPath<ContentAsset>(
					collected.AssetInfo.AssetPath);
				Assert.That(
					content,
					Is.Not.Null,
					$"带 {ContentTag} 构建标签的资源不是 Gameplay 内容作者资产：{collected.AssetInfo.AssetPath}");
				Assert.That(
					collected.Address,
					Is.Empty,
					$"内容作者资产不应生成并列的 YooAsset 地址：{collected.AssetInfo.AssetPath}");
				foundFoundationCard |= collected.AssetInfo.AssetPath == FoundationCardPath;
			}

			Assert.That(contentAssetCount, Is.GreaterThan(0));
			Assert.That(foundFoundationCard, Is.True, "真实地基内容没有进入 YooAsset 内容收集结果。");
		}
	}
}
