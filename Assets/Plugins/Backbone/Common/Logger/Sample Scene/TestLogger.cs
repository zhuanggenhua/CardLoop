using UnityEngine;

namespace Backbone
{
	/// <summary>
	/// Demonstrates a visually appealing test for the Logger system.
	/// Generates a colorful log to showcase categories and levels.
	/// </summary>
	public class TestLogger : MonoBehaviour
	{
		private void Start()
		{
			// === Section 1: Intro Banner ===
			Logger.Log("==========================================", LogLevel.Info, "Gameplay");
			Logger.Log("      LOGGER SYSTEM TEST - VISUAL DEMO     ", LogLevel.Info, "Gameplay");
			Logger.Log("==========================================", LogLevel.Info, "Gameplay");

			// === Section 2: Gameplay Flow ===
			Logger.Log("Player has entered the dungeon", LogLevel.Info, "Gameplay");
			Logger.Log("Enemy spawned near the player", LogLevel.Debug, "Gameplay");
			Logger.Log("Low ammo detected!", LogLevel.Warning, "Gameplay");

			// === Section 3: UI Events ===
			Logger.Log("Main menu loaded", LogLevel.Info, "UI");
			Logger.Log("Settings panel opened", LogLevel.Debug, "UI");

			// === Section 4: AI Behavior ===
			Logger.Log("AI patrol routine started", LogLevel.Info, "AI");
			Logger.Log("AI lost track of the player!", LogLevel.Warning, "AI");
			Logger.Log("Critical AI navigation failure", LogLevel.Critical, "AI");

			// === Section 5: Audio Feedback ===
			Logger.Log("Background music started", LogLevel.Info, "Audio");
			Logger.Log("Sound effect played: Explosion", LogLevel.Debug, "Audio");

			// === Section 6: Network Events ===
			Logger.Log("Connected to server successfully", LogLevel.Info, "Network");
			Logger.Log("High ping detected", LogLevel.Warning, "Network");
			Logger.Log("Network connection lost!", LogLevel.Error, "Network");

			// === Section 8: Category Activation Test ===
			Logger.Log("=== Disabling AI category ===", LogLevel.Info, "Gameplay");
			Logger.SetCategoryActive("AI", false);
			Logger.Log("This AI message should NOT appear", LogLevel.Warning, "AI");

			Logger.Log("=== Re-enabling AI category ===", LogLevel.Info, "Gameplay");
			Logger.SetCategoryActive("AI", true);
			Logger.Log("AI re-enabled and working normally", LogLevel.Info, "AI");

			// === Section 9: Outro Banner ===
			Logger.Log("==========================================", LogLevel.Info, "Gameplay");
			Logger.Log("           END OF LOGGER DEMO             ", LogLevel.Info, "Gameplay");
			Logger.Log("==========================================", LogLevel.Info, "Gameplay");
		}
	}
}
