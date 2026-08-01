using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public sealed class ContentBrowserWindow : EditorWindow
    {
        enum ContentTab
        {
            Items,
            EquipmentItems,
            EquipmentVisuals,
            WorkbenchCatalogs
        }

        sealed class TabDefinition
        {
            public ContentTab Tab;
            public string Label;
            public Type AssetType;
            public string EmptyMessage;
        }

        const string EquipmentRenderDataTypeName = "EquipmentRenderData";
        const string EquipmentWorkbenchCatalogTypeName = "EquipmentWorkbenchCatalog";

        static readonly TabDefinition[] Tabs =
        {
            new TabDefinition
            {
                Tab = ContentTab.Items,
                Label = "物品",
                AssetType = typeof(Item),
                EmptyMessage = "当前没有物品资产。"
            },
            new TabDefinition
            {
                Tab = ContentTab.EquipmentItems,
                Label = "装备物品",
                AssetType = typeof(Equipment),
                EmptyMessage = "当前没有装备物品资产。"
            },
            new TabDefinition
            {
                Tab = ContentTab.EquipmentVisuals,
                Label = "装备表现",
                AssetType = FindScriptableObjectType(EquipmentRenderDataTypeName),
                EmptyMessage = "当前没有装备表现资产。"
            },
            new TabDefinition
            {
                Tab = ContentTab.WorkbenchCatalogs,
                Label = "换装目录",
                AssetType = FindScriptableObjectType(EquipmentWorkbenchCatalogTypeName),
                EmptyMessage = "当前没有换装目录资产。"
            }
        };

        int _selectedTabIndex;
        int _selectedAssetIndex = -1;
        string _searchText = string.Empty;
        Vector2 _listScroll;
        Vector2 _detailScroll;
        UnityEditor.Editor _cachedEditor;
        ScriptableObject _selectedObject;

        static ContentBrowserWindow()
        {
            FormalDataAssetCache.RegisterKnownTypes(Tabs
                .Select(tab => tab.AssetType)
                .Where(type => type != null));
        }

        [MenuItem("Window/GameCore/Content Browser")]
        public static void ShowWindow()
        {
            ContentBrowserWindow window = GetWindow<ContentBrowserWindow>();
            window.titleContent = new GUIContent("GameCore Content Browser");
            window.minSize = new Vector2(1040f, 620f);
            window.Show();
        }

        void OnDisable()
        {
            if (_cachedEditor != null)
            {
                DestroyImmediate(_cachedEditor);
                _cachedEditor = null;
            }
        }

        void OnGUI()
        {
            IReadOnlyList<ScriptableObject> visibleAssets = GetVisibleAssets();

            EditorGUILayout.BeginHorizontal();
            DrawSidebar(visibleAssets);
            DrawDetailsPanel(visibleAssets);
            EditorGUILayout.EndHorizontal();
        }

        IReadOnlyList<ScriptableObject> GetVisibleAssets()
        {
            TabDefinition tab = Tabs[_selectedTabIndex];
            if (tab.AssetType == null)
                return Array.Empty<ScriptableObject>();

            return FormalDataAssetCache.CreateAssignableAssetSnapshot(tab.AssetType)
                .Where(asset => asset != null)
                .Where(asset => string.IsNullOrWhiteSpace(_searchText)
                    || asset.name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(asset => asset.name)
                .ToArray();
        }

        void DrawSidebar(IReadOnlyList<ScriptableObject> visibleAssets)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(320f));

            string[] tabLabels = Tabs.Select(tab => tab.Label).ToArray();
            int newTabIndex = GUILayout.Toolbar(_selectedTabIndex, tabLabels);
            if (newTabIndex != _selectedTabIndex)
            {
                _selectedTabIndex = newTabIndex;
                _selectedAssetIndex = -1;
                SetSelectedObject(null);
            }

            string newSearch = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
            if (!string.Equals(newSearch, _searchText, StringComparison.Ordinal))
            {
                _searchText = newSearch;
                _selectedAssetIndex = -1;
                SetSelectedObject(null);
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"结果：{visibleAssets.Count}", EditorStyles.miniBoldLabel);

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            if (visibleAssets.Count == 0)
            {
                EditorGUILayout.HelpBox(Tabs[_selectedTabIndex].EmptyMessage, MessageType.Info);
            }
            else
            {
                for (int i = 0; i < visibleAssets.Count; i++)
                {
                    DrawAssetListItem(visibleAssets[i], i);
                }

                if (_selectedAssetIndex >= visibleAssets.Count)
                {
                    _selectedAssetIndex = -1;
                    SetSelectedObject(null);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawAssetListItem(ScriptableObject asset, int index)
        {
            bool isSelected = _selectedAssetIndex == index && _selectedObject == asset;
            GUIStyle style = isSelected ? EditorStyles.helpBox : EditorStyles.objectField;

            EditorGUILayout.BeginHorizontal(style);

            Texture icon = AssetPreview.GetMiniThumbnail(GetPreviewObject(asset));
            GUILayout.Label(icon, GUILayout.Width(20f), GUILayout.Height(20f));

            if (GUILayout.Button(asset.name, EditorStyles.label, GUILayout.Height(22f)))
            {
                _selectedAssetIndex = index;
                SetSelectedObject(asset);
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawDetailsPanel(IReadOnlyList<ScriptableObject> visibleAssets)
        {
            ScriptableObject active = ResolveSelectedObject(visibleAssets);

            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);

            if (active == null)
            {
                EditorGUILayout.HelpBox("左侧选择一个正式数据资产。", MessageType.Info);
            }
            else
            {
                DrawAssetHeader(active);
                EditorGUILayout.Space(10f);
                DrawSummary(active);
                EditorGUILayout.Space(10f);
                DrawInspector(active);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        ScriptableObject ResolveSelectedObject(IReadOnlyList<ScriptableObject> visibleAssets)
        {
            if (_selectedObject != null && visibleAssets.Contains(_selectedObject))
                return _selectedObject;

            if (_selectedAssetIndex >= 0 && _selectedAssetIndex < visibleAssets.Count)
            {
                SetSelectedObject(visibleAssets[_selectedAssetIndex]);
                return _selectedObject;
            }

            return null;
        }

        void SetSelectedObject(ScriptableObject asset)
        {
            _selectedObject = asset;
            Selection.activeObject = asset;

            if (asset == null)
            {
                if (_cachedEditor != null)
                {
                    DestroyImmediate(_cachedEditor);
                    _cachedEditor = null;
                }
                return;
            }

            UnityEditor.Editor.CreateCachedEditor(asset, null, ref _cachedEditor);
        }

        void DrawAssetHeader(ScriptableObject asset)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            Texture preview = AssetPreview.GetAssetPreview(GetPreviewObject(asset))
                ?? AssetPreview.GetMiniThumbnail(GetPreviewObject(asset));
            GUILayout.Label(preview, GUILayout.Width(96f), GUILayout.Height(96f));

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(asset.name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(asset.GetType().Name, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(asset), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("定位资源", GUILayout.Width(90f)))
                EditorGUIUtility.PingObject(asset);
            if (GUILayout.Button("Project 选中", GUILayout.Width(100f)))
                Selection.activeObject = asset;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        void DrawSummary(ScriptableObject asset)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("摘要", EditorStyles.boldLabel);

            if (IsType(asset, EquipmentRenderDataTypeName))
            {
                SerializedObject serializedObject = new SerializedObject(asset);
                SerializedProperty typeProperty = serializedObject.FindProperty("type");
                SerializedProperty spriteProperty = serializedObject.FindProperty("spriteSE");
                SerializedProperty weaponSlotProperty = serializedObject.FindProperty("weaponSlotType");

                DrawSummaryRow("装备类型", GetEnumDisplayName(typeProperty));
                DrawSummaryRow("渲染模式", GetEquipmentRenderModeName(typeProperty));
                DrawSummaryRow("基础贴图", spriteProperty?.objectReferenceValue != null
                    ? spriteProperty.objectReferenceValue.name
                    : "未配置");
                DrawSummaryRow("武器槽位", GetEnumDisplayName(weaponSlotProperty));
            }
            else if (IsType(asset, EquipmentWorkbenchCatalogTypeName))
            {
                DrawSummaryRow("角色数量", GetSerializedArraySize(asset, "characters").ToString());
                DrawSummaryRow("装备数量", GetSerializedArraySize(asset, "equipments").ToString());
                DrawSummaryRow("类型数量", GetWorkbenchCategoryCount(asset).ToString());
            }
            else
            {
                switch (asset)
                {
                    case Equipment equipment:
                        DrawSummaryRow("装备槽位", equipment.type.ToString());
                        DrawSummaryRow("物品分类", equipment.category.ToString());
                        DrawSummaryRow("价格", equipment.price.ToString());
                        DrawSummaryRow("装备表现", equipment.visual != null ? equipment.visual.name : "未配置");
                        DrawSummaryRow("附加能力", equipment.bonusAbilityCount.ToString());
                        break;

                    case Item item:
                        DrawSummaryRow("物品分类", item.category.ToString());
                        DrawSummaryRow("显示名", item.displayName);
                        DrawSummaryRow("价格", item.price.ToString());
                        DrawSummaryRow("可出售", item.sellable ? "是" : "否");
                        break;

                    default:
                        DrawSummaryRow("类型", asset.GetType().Name);
                        break;
                }
            }

            EditorGUILayout.EndVertical();
        }

        static bool IsType(ScriptableObject asset, string typeName)
        {
            return asset != null && string.Equals(asset.GetType().Name, typeName, StringComparison.Ordinal);
        }

        static Type FindScriptableObjectType(string typeName)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    return type;
            }

            return null;
        }

        static string GetEnumDisplayName(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                return "未配置";

            return property.enumDisplayNames != null
                && property.enumValueIndex >= 0
                && property.enumValueIndex < property.enumDisplayNames.Length
                    ? property.enumDisplayNames[property.enumValueIndex]
                    : property.enumValueIndex.ToString();
        }

        static string GetEquipmentRenderModeName(SerializedProperty typeProperty)
        {
            if (typeProperty == null || typeProperty.propertyType != SerializedPropertyType.Enum)
                return "Unknown";

            string typeName = typeProperty.enumNames != null
                && typeProperty.enumValueIndex >= 0
                && typeProperty.enumValueIndex < typeProperty.enumNames.Length
                    ? typeProperty.enumNames[typeProperty.enumValueIndex]
                    : string.Empty;

            switch (typeName)
            {
                case "Gloves":
                case "Shoes":
                    return "Color";
                case "Weapon":
                case "Shield":
                case "Bag":
                    return "Weapon";
                case "Clothing":
                case "Cloak":
                case "Pants":
                case "Helmet":
                case "Hat":
                case "Mask":
                    return "Sprite";
                default:
                    return "Unknown";
            }
        }

        static int GetWorkbenchCategoryCount(ScriptableObject asset)
        {
            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty equipments = serializedObject.FindProperty("equipments");
            if (equipments == null || !equipments.isArray)
                return 0;

            HashSet<string> categories = new HashSet<string>();
            for (int i = 0; i < equipments.arraySize; i++)
            {
                SerializedProperty element = equipments.GetArrayElementAtIndex(i);
                SerializedProperty visual = element.FindPropertyRelative("visual");
                if (visual?.objectReferenceValue == null)
                    continue;

                SerializedObject visualObject = new SerializedObject(visual.objectReferenceValue);
                SerializedProperty typeProperty = visualObject.FindProperty("type");
                if (typeProperty != null && typeProperty.propertyType == SerializedPropertyType.Enum)
                    categories.Add(typeProperty.enumValueIndex.ToString());
            }

            return categories.Count;
        }

        void DrawSummaryRow(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(88f));
            EditorGUILayout.SelectableLabel(value ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        void DrawInspector(ScriptableObject asset)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Inspector", EditorStyles.boldLabel);
            UnityEditor.Editor.CreateCachedEditor(asset, null, ref _cachedEditor);
            _cachedEditor?.OnInspectorGUI();
            EditorGUILayout.EndVertical();
        }

        static int GetSerializedArraySize(ScriptableObject asset, string propertyName)
        {
            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null && property.isArray ? property.arraySize : 0;
        }

        static UnityEngine.Object GetPreviewObject(ScriptableObject asset)
        {
            switch (asset)
            {
                case Item item when item.icon != null:
                    return item.icon;
                default:
                    if (IsType(asset, EquipmentRenderDataTypeName))
                    {
                        SerializedObject serializedObject = new SerializedObject(asset);
                        SerializedProperty spriteProperty = serializedObject.FindProperty("spriteSE");
                        if (spriteProperty?.objectReferenceValue != null)
                            return spriteProperty.objectReferenceValue;
                    }

                    return asset;
            }
        }
    }
}
