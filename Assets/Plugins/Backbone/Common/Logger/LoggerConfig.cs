using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Backbone
{
	/// <summary>
	/// ScriptableObject configuration for the Logger.
	/// Allows setting default categories, their colors, and global log level directly from the Unity Inspector.
	/// </summary>
	[CreateAssetMenu(fileName = "LoggerConfig", menuName = "Backbone/Logging/LoggerConfig")]
	public class LoggerConfig : ScriptableObject
	{        
        /// <summary>
        /// Represents a single log category with activation state and color.
        /// </summary>
        [System.Serializable]
		public class CategoryEntry
		{
			/// <summary>
			/// The name of the category.
			/// </summary>
			public string name;

			/// <summary>
			/// Whether the category is active by default.
			/// </summary>
			public bool active = true;

			/// <summary>
			/// Display color for this category in the Unity console.
			/// </summary>
			public Color color = Color.white;
		}

        /// <summary>
        /// Controls if categories must be displayer grouped by prefix.
        /// </summary>

        public bool groupByPrefix = false;


        /// <summary>
        /// Controls which parts of the log will be colored in the console.
        /// </summary>
        public LoggerColorMode colorMode = LoggerColorMode.CategoryOnly;

		/// <summary>
		/// Global minimum log level for filtering messages.
		/// </summary>
		public LogLevel globalLevel = LogLevel.Debug;

		/// <summary>
		/// List of categories that can be managed from the Unity Inspector.
		/// </summary>
		public List<CategoryEntry> categories = new List<CategoryEntry>();

		/// <summary>
		/// Default categories with their associated colors.
		/// </summary>
		private static readonly (string name, Color color)[] DefaultCategories = new (string, Color)[]
		{
			("Gameplay", new Color(0.3f, 0.8f, 1f)),   // Light blue
			("UI", new Color(0.5f, 0.5f, 1f)),         // Blue
			("AI", new Color(1f, 0.6f, 0.2f)),         // Orange
			("Audio", new Color(0.6f, 1f, 0.6f)),      // Light green
			("Network", new Color(1f, 0.3f, 0.3f))     // Red
		};

		/// <summary>
		/// Ensures that default categories are present in the configuration.
		/// </summary>
		public void EnsureDefaultCategories()
		{
			foreach (var def in DefaultCategories)
			{
				if (!categories.Any(c => c.name == def.name))
				{
					categories.Add(new CategoryEntry
					{
						name = def.name,
						active = true,
						color = def.color
					});
				}
			}
		}

		/// <summary>
		/// Called automatically when the ScriptableObject is loaded or created.
		/// Guarantees default categories are added the very first time.
		/// </summary>
		private void OnEnable()
		{
			if (categories.Count == 0)
			{
				EnsureDefaultCategories();
#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(this);
#endif
			}
		}

		/// <summary>
		/// Automatically populate defaults when the asset is first created.
		/// This triggers right after creation in Unity.
		/// </summary>
		private void OnValidate()
		{
			// Only populate if the list is completely empty
			if (categories.Count == 0)
			{
				EnsureDefaultCategories();
#if UNITY_EDITOR
				UnityEditor.EditorUtility.SetDirty(this);
#endif
			}
		}

        /// <summary>
        /// Called automatically when Unity starts playing.
        /// Logs a message depending on whether ENABLE_LOGGING is active.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CheckLoggerStatusOnStart()
        {
#if ENABLE_LOGGING
            Debug.LogWarning("<color=orange>[Backbone Logger]</color> ENABLE_LOGGING is ACTIVE. " +
                             "Remember to disable it before production builds to strip all logs.");
#else
            Debug.Log("<color=#00CFFF>[Backbone Logger]</color> Logger is currently DISABLED.\n" +
                      "No logs will be recorded at runtime. To enable logging, open your <b>LoggerConfig</b> settings in the Unity Inspector " +
                      "and check the <b>'Enable Logging'</b> option.");
#endif
        }

        /// <summary>
        /// Applies the configuration to the global Logger system.
        /// </summary>
        public void ApplyConfig()
		{
			Logger.GlobalLevel = globalLevel;
			Logger.ColorMode = colorMode;

			// Ensure default categories are included
			EnsureDefaultCategories();

			// Register categories
			foreach (var entry in categories)
			{
				Logger.AddCategory(entry.name, entry.active, entry.color);
			}
		}

		/// <summary>
		/// Returns the color assigned to a specific category.
		/// </summary>
		/// <param name="category">The category name.</param>
		/// <returns>The configured color or white if the category does not exist.</returns>
		public Color GetCategoryColor(string category)
		{
			var entry = categories.FirstOrDefault(c => c.name == category);
			return entry != null ? entry.color : Color.white;
		}
	}
}
