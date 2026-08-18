using System.Collections;
using GameCore;
using Gameplay.Content;
using Sirenix.OdinInspector;
using UnityEngine;
using YokiFrame;

namespace Gameplay.Scenarios
{
	/// <summary>
	/// 标题场景的玩家入口。场景只配置一次默认剧本内容引用，
	/// 新局、读档和后续场景组合仍由剧本导演与 UIKit 正式入口负责。
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ScenarioTitleScreen : MonoBehaviour
	{
		[SerializeField]
		[ContentIdReference(typeof(ScenarioDefinition))]
		[LabelText("默认剧本")]
		[Tooltip("玩家点击新游戏时开始的剧本。这里只保存唯一内容 ID，不复制剧本名称、场景或规则。")]
		private ContentId m_defaultScenarioId;

		private IEnumerator Start()
		{
			while (GameManager.StartupState is
				GameManagerStartupState.NotStarted or GameManagerStartupState.Initializing)
			{
				yield return null;
			}

			if (GameManager.StartupState != GameManagerStartupState.Ready)
			{
				throw new System.InvalidOperationException(
					$"标题入口无法启动：GameManager 状态为 {GameManager.StartupState}。",
					GameManager.StartupException);
			}
			if (!m_defaultScenarioId.IsValid)
			{
				throw new System.InvalidOperationException("标题入口没有配置有效的默认剧本内容 ID。");
			}

			ScenarioDirector director = GameManager.GetSystem<ScenarioDirector>();
			UIKit.OpenPanelAsync<ScenarioTitlePanel>(
				callback: panel =>
				{
					if (panel == null)
					{
						throw new System.InvalidOperationException("UIKit 没有加载标题面板。");
					}
				},
				level: UILevel.Common,
				data: new ScenarioTitlePanelData(director, m_defaultScenarioId));
		}

		private void OnDestroy()
		{
			UIKit.ClosePanel<ScenarioTitlePanel>();
		}
	}
}
