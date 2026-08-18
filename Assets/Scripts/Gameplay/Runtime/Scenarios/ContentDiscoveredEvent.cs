using Gameplay.Content;

namespace Gameplay.Scenarios
{
	/// <summary>剧本单局已把一个内容提交到发现集合。</summary>
	public readonly struct ContentDiscoveredEvent
	{
		public ContentId ScenarioId { get; }

		public ContentId ContentId { get; }

		public ContentDiscoveredEvent(ContentId scenarioId, ContentId contentId)
		{
			ScenarioId = scenarioId;
			ContentId = contentId;
		}
	}
}
