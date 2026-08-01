using UnityEngine;

namespace Backbone
{
	/// <summary>
	/// LoggerManager is responsible for applying the settings from a single LoggerConfig
	/// instance at runtime. 
	/// 
	/// Only ONE LoggerConfig should exist in your project at a time, as it defines
	/// all categories, colors, and global log level settings for Backbone Logger.
	/// 
	/// You can use this component for quick setup by attaching it to a GameObject in your
	/// initial scene, or you can call LoggerConfig.ApplyConfig() manually from any other
	/// script if you need more control.
	/// </summary>
	public class LoggerManager : MonoBehaviour
	{
		[Header("Logger Settings")]
		[Tooltip("Assign the LoggerConfig asset to apply these settings at runtime.")]
		public LoggerConfig config;

		private void Awake()
		{
			// Ensure there is a configuration assigned
			if (config != null)
			{
				// Apply the logger configuration globally
				config.ApplyConfig();

				// Log a message to confirm initialization
				Logger.Log("Logger initialized successfully!", LogLevel.Info, "Core");
			}
			else
			{
				// Fallback warning if no config was assigned
				Debug.LogWarning("[LoggerManager] No LoggerConfig assigned. Logging will not be initialized.");
			}
		}
	}
}
