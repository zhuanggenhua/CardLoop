using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using YokiFrame;

namespace GameCore.Tests
{
	/// <summary>验证 GameCore 文件层只承载 SaveKit 模块容器，不抢占各领域快照职责。</summary>
	public sealed class SaveSystemModuleStorageEditModeTests
	{
		private string m_saveDirectory;

		[SetUp]
		public void SetUp()
		{
			m_saveDirectory = Path.Combine(
				Path.GetTempPath(),
				"CardLoop-SaveSystem-" + Guid.NewGuid().ToString("N"));
			SaveSystem.ConfigureSaveKit(m_saveDirectory);
		}

		[TearDown]
		public void TearDown()
		{
			SaveSystem.ResetSaveKitConfigurationForTests();
			SaveKit.Reset();
			if (Directory.Exists(m_saveDirectory))
			{
				Directory.Delete(m_saveDirectory, recursive: true);
			}
		}

		[Test]
		public void SaveContainer_RoundTripsIndependentModulesAndSlotMetadata()
		{
			SaveData container = SaveSystem.CreateSaveContainer();
			container.RegisterModule(new SaveDataBlock { header = "旧世界块" });
			container.RegisterModule(new ProbeRunSnapshot
			{
				ScenarioId = "test.scenario",
				ConfirmedTurn = 7
			});

			Assert.That(
				SaveSystem.StoreSaveDataToFile(2, container, "荒岛 · 第 4 天"),
				Is.True);

			SaveData restored = SaveSystem.ExtractSaveContainerFromFile(2);
			SaveMeta metadata = SaveSystem.GetSaveMetadata(2);

			Assert.That(restored, Is.Not.Null);
			Assert.That(restored.GetModule<SaveDataBlock>().header, Is.EqualTo("旧世界块"));
			Assert.That(restored.GetModule<ProbeRunSnapshot>().ScenarioId, Is.EqualTo("test.scenario"));
			Assert.That(restored.GetModule<ProbeRunSnapshot>().ConfirmedTurn, Is.EqualTo(7));
			Assert.That(metadata.SlotId, Is.EqualTo(2));
			Assert.That(metadata.DisplayName, Is.EqualTo("荒岛 · 第 4 天"));
			Assert.That(metadata.LastSavedTimestamp, Is.GreaterThan(0));
		}

		[Test]
		public void SaveSlots_AreEnumeratedInSlotOrderAndDeletedThroughOneFileOwner()
		{
			SaveData container = SaveSystem.CreateSaveContainer();
			container.RegisterModule(new ProbeRunSnapshot { ScenarioId = "test.slots" });
			Assert.That(SaveSystem.StoreSaveDataToFile(9, container, "九号"), Is.True);
			Assert.That(SaveSystem.StoreSaveDataToFile(2, container, "二号"), Is.True);
			Assert.That(SaveSystem.StoreSaveDataToFile(5, container, "五号"), Is.True);

			Assert.That(
				SaveSystem.GetAllSaveMetadata().Select(metadata => metadata.SlotId),
				Is.EqualTo(new[] { 2, 5, 9 }));
			Assert.That(SaveSystem.DeleteSaveData(5), Is.True);
			Assert.That(SaveSystem.ExtractSaveContainerFromFile(5), Is.Null);
			Assert.That(SaveSystem.DeleteSaveData(5), Is.False,
				"删除不存在的槽位不能向 UI 返回假成功。");
			Assert.That(
				SaveSystem.GetAllSaveMetadata().Select(metadata => metadata.SlotId),
				Is.EqualTo(new[] { 2, 9 }));

			Assert.That(SaveSystem.DeleteAllSaveData(), Is.EqualTo(2));
			Assert.That(SaveSystem.GetAllSaveMetadata(), Is.Empty);
		}

		[Serializable]
		private sealed class ProbeRunSnapshot
		{
			public string ScenarioId;
			public int ConfirmedTurn;
		}
	}
}
