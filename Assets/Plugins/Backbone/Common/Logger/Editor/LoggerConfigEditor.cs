using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Backbone
{
    /// \class LoggerConfigEditor
    /// \brief Custom inspector for LoggerConfig.
    /// \details
    /// Provides:
    ///  - Global log level and color mode controls.
    ///  - Category list with per-item toggle, name, color, and delete.
    ///  - Optional grouping by category prefix (e.g., "Menu.UI" -> group "Menu").
    ///  - Delete-whole-group with confirmation.
    ///  - Import/Merge categories from another LoggerConfig (Keep or Overwrite).
    ///  - Toggle for the ENABLE_LOGGING scripting define.
    /// \note Applies serialized changes exactly once at the end of OnInspectorGUI to keep IMGUI stable,
    ///       except when confirming destructive operations where an early Apply is required before ExitGUI.
    [CustomEditor(typeof(LoggerConfig))]
    public class LoggerConfigEditor : Editor
    {
        /// \brief Serialized reference to LoggerConfig.globalLevel.
        private SerializedProperty globalLevelProp;
        /// \brief Serialized reference to LoggerConfig.categories.
        private SerializedProperty categoriesProp;
        /// \brief Serialized reference to LoggerConfig.colorMode.
        private SerializedProperty colorModeProp;
        /// \brief Serialized reference to LoggerConfig.groupByPrefix.
        private SerializedProperty groupByPrefixProp;

        /// \brief Temporary buffer for the “add category” text field.
        private string newCategoryName = "";

        /// \brief Scripting define used to include logger calls in builds.
        private const string SYMBOL = "ENABLE_LOGGING";

        /// \brief Source asset used to import/merge categories (editor-only, not serialized).
        private LoggerConfig importSource = null;

        /// \brief Unity callback to cache SerializedProperty references.
        /// \details Finds and stores references to:
        ///  - globalLevel
        ///  - categories
        ///  - colorMode
        ///  - groupByPrefix
        private void OnEnable()
        {
            globalLevelProp = serializedObject.FindProperty("globalLevel");
            categoriesProp = serializedObject.FindProperty("categories");
            colorModeProp = serializedObject.FindProperty("colorMode");
            groupByPrefixProp = serializedObject.FindProperty("groupByPrefix");
        }

        /// \brief Renders the custom inspector UI and applies serialized changes once.
        /// \details
        /// - Draws ENABLE_LOGGING define toggle and forces domain reload on change.
        /// - Draws global level and color mode fields.
        /// - Draws grouping toggle and either a grouped or flat category list.
        /// - Handles add, delete, and restore-defaults actions.
        /// - Provides Import/Merge from another LoggerConfig (Keep or Overwrite).
        /// \note Uses UpdateIfRequiredOrScript()/ApplyModifiedProperties() once to keep IMGUI consistent.
        public override void OnInspectorGUI()
        {
            // Begin read/write of serialized data
            serializedObject.UpdateIfRequiredOrScript();

            var config = (LoggerConfig)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Logger Configuration", EditorStyles.boldLabel);

            // Production warning
            EditorGUILayout.HelpBox(
                "Remember to disable this checkbox before production builds to completely strip all Logger.Log code.",
                MessageType.Warning
            );

            // ENABLE_LOGGING define toggle (not serialized; affects PlayerSettings)
            bool isEnabled = IsSymbolDefined(SYMBOL);
            bool newValue = EditorGUILayout.Toggle("Enable Logging", isEnabled);
            if (newValue != isEnabled)
            {
                if (newValue) AddSymbol(SYMBOL);
                else RemoveSymbol(SYMBOL);

                // Force domain reload to recompile with new define
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.Space();

            // Global level
            EditorGUILayout.PropertyField(globalLevelProp, new GUIContent("Global Log Level"));

            EditorGUILayout.Space();

            // Color mode
            EditorGUILayout.PropertyField(colorModeProp, new GUIContent("Color Mode"));

            EditorGUILayout.Space();

            // Grouping toggle
            EditorGUILayout.PropertyField(groupByPrefixProp, new GUIContent("Group Categories by Prefix"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Categories", EditorStyles.boldLabel);

            // Draw categories
            if (config.groupByPrefix)
            {
                DrawCategoriesGroupedByPrefix();
            }
            else
            {
                for (int i = 0; i < categoriesProp.arraySize; i++)
                    DrawCategoryRow(categoriesProp.GetArrayElementAtIndex(i), i);
            }

            EditorGUILayout.Space();

            // Add new category
            EditorGUILayout.BeginHorizontal();
            newCategoryName = EditorGUILayout.TextField(newCategoryName);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newCategoryName)))
            {
                if (GUILayout.Button("Add", GUILayout.Width(50)))
                {
                    bool exists = false;
                    for (int i = 0; i < categoriesProp.arraySize; i++)
                    {
                        if (categoriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == newCategoryName)
                        { exists = true; break; }
                    }

                    if (!exists)
                    {
                        int index = categoriesProp.arraySize;
                        categoriesProp.InsertArrayElementAtIndex(index);

                        var e = categoriesProp.GetArrayElementAtIndex(index);
                        e.FindPropertyRelative("name").stringValue = newCategoryName;
                        e.FindPropertyRelative("active").boolValue = true;
                        e.FindPropertyRelative("color").colorValue = Color.white;
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Duplicate Category", $"The category '{newCategoryName}' already exists.", "OK");
                    }

                    newCategoryName = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Enable Logging: When unchecked, the compiler strips all Logger.Log code completely.\n" +
                "Color Mode: Choose whether only the header or the full message is colored.\n" +
                "Group Categories by Prefix: Organize categories visually using 'Prefix.CategoryName' format.\n" +
                "Use checkboxes to enable/disable categories and assign colors to each one.",
                MessageType.Info
            );

            // Restore default categories
            if (GUILayout.Button("Restore Default Categories"))
            {
                Undo.RecordObject(config, "Restore Default Categories");
                config.EnsureDefaultCategories();
                EditorUtility.SetDirty(config);
            }

            // --- Import / Merge ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Import / Merge", EditorStyles.boldLabel);

            // Source asset picker (project asset only)
            importSource = (LoggerConfig)EditorGUILayout.ObjectField(
                new GUIContent("Source LoggerConfig"),
                importSource,
                typeof(LoggerConfig),
                false
            );

            using (new EditorGUI.DisabledScope(importSource == null || importSource == (LoggerConfig)target))
            {
                if (GUILayout.Button("Merge Categories...", GUILayout.Height(22)))
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Merge Categories",
                        "Choose merge behavior for same-name categories:",
                        "Keep existing",        // 0
                        "Cancel",               // 1
                        "Overwrite existing"    // 2
                    );

                    if (choice == 0) ImportCategoriesFrom(importSource, overwriteExisting: false);
                    else if (choice == 2) ImportCategoriesFrom(importSource, overwriteExisting: true);
                }
            }

            // APPLY EXACTLY ONCE at the end
            serializedObject.ApplyModifiedProperties();
        }

        #region Category Drawing Helpers

        /// \brief Draws categories grouped by the segment before the first '.' in their name.
        /// \details
        /// - Builds a map of prefix -> indices in the categories array.
        /// - Shows a tri-state group toggle (mixed when some but not all are active).
        /// - Clicking the group toggle assigns the same active state to all children.
        /// - Provides a Delete Group button with confirmation, removing all children safely.
        /// - Renders each child row via DrawCategoryRow; no mid-frame Apply calls.
        /// \note No ApplyModifiedProperties() here; it is deferred to OnInspectorGUI, except when confirming deletion.
        private void DrawCategoriesGroupedByPrefix()
        {
            // Build group map
            var grouped = new Dictionary<string, List<int>>();
            for (int i = 0; i < categoriesProp.arraySize; i++)
            {
                var nameProp = categoriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("name");
                string fullName = nameProp.stringValue;

                string prefix = fullName.Contains(".") ? fullName.Split('.')[0] : "Other";

                if (!grouped.ContainsKey(prefix))
                    grouped[prefix] = new List<int>();

                grouped[prefix].Add(i);
            }

            // Stable key order helps IMGUI diffing
            var keys = new List<string>(grouped.Keys);
            keys.Sort();

            foreach (var key in keys)
            {
                var idxs = grouped[key];

                // Compute group state
                bool allActive = true;
                bool anyActive = false;
                foreach (var index in idxs)
                {
                    var activeProp = categoriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("active");
                    if (activeProp.boolValue) anyActive = true;
                    else allActive = false;
                }
                bool mixedState = anyActive && !allActive;

                // Header row: tri-state toggle + label + delete group button
                EditorGUILayout.BeginHorizontal();

                EditorGUI.showMixedValue = mixedState;
                bool newGroupState = EditorGUILayout.Toggle(allActive, GUILayout.Width(20));
                EditorGUI.showMixedValue = false;

                EditorGUILayout.LabelField(key, EditorStyles.boldLabel);

                // Delete whole group with confirmation
                if (GUILayout.Button("Delete Group", GUILayout.Width(100)))
                {
                    int count = idxs.Count;
                    if (EditorUtility.DisplayDialog(
                            "Delete Category Group",
                            $"Delete group '{key}' with {count} categories?",
                            "Delete", "Cancel"))
                    {
                        // Remove children in descending order to avoid index shifts
                        for (int n = idxs.Count - 1; n >= 0; n--)
                            categoriesProp.DeleteArrayElementAtIndex(idxs[n]);

                        // IMPORTANT: apply before exiting the IMGUI pass
                        serializedObject.ApplyModifiedProperties();
                        // Exit current IMGUI pass to avoid iterating a mutated array
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.EndHorizontal();

                // If user toggled the group, set all children
                if (newGroupState != allActive)
                {
                    foreach (var index in idxs)
                    {
                        var activeProp = categoriesProp.GetArrayElementAtIndex(index).FindPropertyRelative("active");
                        activeProp.boolValue = newGroupState;
                    }
                    // No Apply here; OnInspectorGUI will apply once at the end
                }

                // Children rows
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var index in idxs)
                        DrawCategoryRow(categoriesProp.GetArrayElementAtIndex(index), index);
                }

                EditorGUILayout.Space();
            }
        }

        /// \brief Draws a single category row.
        /// \param element SerializedProperty pointing to the category struct/object.
        /// \param index Index of the category in the categories array (used for deletion).
        /// \details
        /// Row layout:
        ///  - Toggle (bound to 'active' via PropertyField for IMGUI stability)
        ///  - Editable name (TextField)
        ///  - Color picker (PropertyField)
        ///  - Delete button (removes array element and exits GUI)
        /// \note No ApplyModifiedProperties() is called here.
        private void DrawCategoryRow(SerializedProperty element, int index)
        {
            var nameProp = element.FindPropertyRelative("name");
            var activeProp = element.FindPropertyRelative("active");
            var colorProp = element.FindPropertyRelative("color");

            EditorGUILayout.BeginHorizontal();

            // Toggle bound directly to SerializedProperty to avoid IMGUI glitches
            var rect = GUILayoutUtility.GetRect(20, EditorGUIUtility.singleLineHeight, GUILayout.Width(20));
            EditorGUI.PropertyField(rect, activeProp, GUIContent.none);

            // Name
            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(nameProp.stringValue);
            if (EditorGUI.EndChangeCheck())
                nameProp.stringValue = newName;

            // Color bound to SerializedProperty
            rect = GUILayoutUtility.GetRect(50, EditorGUIUtility.singleLineHeight, GUILayout.Width(50));
            EditorGUI.PropertyField(rect, colorProp, GUIContent.none);

            // Delete
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                categoriesProp.DeleteArrayElementAtIndex(index);
                // Cut current IMGUI pass to avoid iterating a mutated array
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
            // No Apply here
        }

        #endregion

        #region Import / Merge Helpers

        /// \brief Merge categories from another LoggerConfig without clearing current ones.
        /// \param src Source LoggerConfig asset.
        /// \param overwriteExisting If true, overwrite 'active' and 'color' for same-name entries; otherwise keep current.
        /// \details
        /// Reads the source via SerializedObject to avoid requiring public access on the ScriptableObject.
        /// New categories are appended; existing names are kept or overwritten based on the flag.
        private void ImportCategoriesFrom(LoggerConfig src, bool overwriteExisting)
        {
            if (src == null) return;

            var dst = (LoggerConfig)target;
            Undo.RecordObject(dst, "Merge Categories");

            // Read source categories via SerializedObject
            var so = new SerializedObject(src);
            var sp = so.FindProperty("categories");
            if (sp == null || !sp.isArray)
            {
                EditorUtility.DisplayDialog("Import Error", "Source has no 'categories' array.", "OK");
                return;
            }

            // Build name -> index map for destination
            var nameToIndex = new Dictionary<string, int>();
            for (int i = 0; i < categoriesProp.arraySize; i++)
            {
                string n = categoriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (!nameToIndex.ContainsKey(n)) nameToIndex.Add(n, i);
            }

            // Iterate source array
            for (int i = 0; i < sp.arraySize; i++)
            {
                var srcElem = sp.GetArrayElementAtIndex(i);
                string sName = srcElem.FindPropertyRelative("name").stringValue;
                bool sAct = srcElem.FindPropertyRelative("active").boolValue;
                Color sCol = srcElem.FindPropertyRelative("color").colorValue;

                if (!nameToIndex.TryGetValue(sName, out int dstIdx))
                {
                    // Append new category
                    int newIdx = categoriesProp.arraySize;
                    categoriesProp.InsertArrayElementAtIndex(newIdx);
                    var e = categoriesProp.GetArrayElementAtIndex(newIdx);
                    e.FindPropertyRelative("name").stringValue = sName;
                    e.FindPropertyRelative("active").boolValue = sAct;
                    e.FindPropertyRelative("color").colorValue = sCol;
                    nameToIndex[sName] = newIdx;
                }
                else if (overwriteExisting)
                {
                    // Overwrite existing fields
                    var e = categoriesProp.GetArrayElementAtIndex(dstIdx);
                    e.FindPropertyRelative("active").boolValue = sAct;
                    e.FindPropertyRelative("color").colorValue = sCol;
                }
                // else keep existing
            }

            EditorUtility.SetDirty(dst);
            // Do NOT call ApplyModifiedProperties() here; OnInspectorGUI applies once at the end.
        }

        #endregion

        #region Compilation Symbol Helpers

        /// \brief Checks whether a scripting define symbol is present for the current build target group.
        /// \param symbol The define symbol to check (e.g., "ENABLE_LOGGING").
        /// \return True if present, false otherwise.
        private static bool IsSymbolDefined(string symbol)
        {
            string symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return symbols.Contains(symbol);
        }

        /// \brief Adds a scripting define symbol to the current build target group if missing.
        /// \param symbol The define symbol to add.
        private static void AddSymbol(string symbol)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            if (!defines.Contains(symbol))
            {
                defines = string.IsNullOrEmpty(defines) ? symbol : $"{defines};{symbol}";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
            }
        }

        /// \brief Removes a scripting define symbol from the current build target group if present.
        /// \param symbol The define symbol to remove.
        /// \details Also cleans up duplicate separators and trims trailing semicolons.
        private static void RemoveSymbol(string symbol)
        {
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);

            if (defines.Contains(symbol))
            {
                defines = defines.Replace(symbol, "");
                defines = defines.Replace(";;", ";").Trim(';');
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, defines);
            }
        }

        #endregion
    }
}
