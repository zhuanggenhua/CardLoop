#if UNITY_EDITOR
using System;
using ContextSteering2D;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace GameCore.EditorTools
{
    /// <summary>
    /// Context Steering 独立调试面板。
    /// </summary>
    public sealed class ContextSteeringDebugWindow : EditorWindow
    {
        private const string WindowTitle = "转向调试";
        private const string MenuPath = "Tools/转向调试面板";
        private const string DefaultProfilePath =
            "Assets/ProjectPlugins/ContextSteering2D/Runtime/Defaults/DefaultContextSteeringProfile2D.asset";

        private static readonly Color BackgroundColor = new(0.16f, 0.16f, 0.17f);
        private static readonly Color PanelColor = new(0.21f, 0.21f, 0.23f);
        private static readonly Color BorderColor = new(0.30f, 0.30f, 0.32f);
        private static readonly Color MutedTextColor = new(0.72f, 0.72f, 0.74f);

        private Label m_playModeValue;
        private Label m_simulationValue;
        private Label m_agentCountValue;
        private Label m_probeCountValue;
        private Label m_snapshotCountValue;
        private Label m_lastRefreshValue;
        private VisualElement m_probeList;
        private double m_lastRefreshTime;

        [MenuItem(MenuPath, priority = 2100)]
        public static void Open()
        {
            ContextSteeringDebugWindow window = GetWindow<ContextSteeringDebugWindow>(false, WindowTitle);
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(780f, 520f);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;
            root.style.backgroundColor = new StyleColor(BackgroundColor);
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;

            root.Add(CreateHeader());
            root.Add(CreateMetricStrip());
            root.Add(CreateControlsPanel());
            root.Add(CreateProbePanel());

            RefreshAll();
        }

        private void OnEditorUpdate()
        {
            if (EditorApplication.timeSinceStartup - m_lastRefreshTime < 0.5d)
            {
                return;
            }

            RefreshAll();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            RefreshAll();
        }

        private VisualElement CreateHeader()
        {
            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 12;

            VisualElement titleBlock = new();
            titleBlock.style.flexGrow = 1;

            Label title = new("转向调试");
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            titleBlock.Add(title);

            Label subtitle = new("Context Steering 运行状态、探针列表和 SceneView 显示开关。");
            subtitle.style.marginTop = 3;
            subtitle.style.color = new StyleColor(MutedTextColor);
            titleBlock.Add(subtitle);
            header.Add(titleBlock);

            Button refreshButton = CreateButton("刷新", RefreshAll);
            refreshButton.style.width = 86;
            header.Add(refreshButton);
            return header;
        }

        private VisualElement CreateMetricStrip()
        {
            VisualElement strip = new();
            strip.style.flexDirection = FlexDirection.Row;
            strip.style.flexWrap = Wrap.Wrap;
            strip.style.marginBottom = 10;

            m_playModeValue = CreateMetricCard(strip, "运行状态", "Unity PlayMode");
            m_simulationValue = CreateMetricCard(strip, "世界模拟", "Simulation");
            m_agentCountValue = CreateMetricCard(strip, "Agent 数量", "已注册角色");
            m_probeCountValue = CreateMetricCard(strip, "探针数量", "DebugProbe");
            m_snapshotCountValue = CreateMetricCard(strip, "有快照", "调试数据");
            return strip;
        }

        private Label CreateMetricCard(VisualElement parent, string title, string hint)
        {
            VisualElement card = CreateCard();
            card.style.width = 136;
            card.style.height = 78;
            card.style.marginRight = 8;
            card.style.marginBottom = 8;

            Label titleLabel = new(title);
            titleLabel.style.color = new StyleColor(MutedTextColor);
            titleLabel.style.fontSize = 11;
            card.Add(titleLabel);

            Label valueLabel = new("-");
            valueLabel.style.marginTop = 7;
            valueLabel.style.fontSize = 20;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = Color.white;
            card.Add(valueLabel);

            Label hintLabel = new(hint);
            hintLabel.style.marginTop = 3;
            hintLabel.style.fontSize = 10;
            hintLabel.style.color = new StyleColor(new Color(0.55f, 0.55f, 0.58f));
            card.Add(hintLabel);

            parent.Add(card);
            return valueLabel;
        }

        private VisualElement CreateControlsPanel()
        {
            VisualElement panel = CreateSection("SceneView 显示", "控制所有转向调试探针在场景视图里的可视化状态。");

            VisualElement displayRow = CreateButtonRow();
            displayRow.Add(CreateButton("关闭全部", () => ApplyDisplayMode(false, false)));
            displayRow.Add(CreateButton("仅选中", () => ApplyDisplayMode(true, false)));
            displayRow.Add(CreateButton("显示全部", () => ApplyDisplayMode(true, true)));
            panel.Add(displayRow);

            VisualElement toolRow = CreateButtonRow();
            toolRow.Add(CreateButton("打开默认配置", OpenDefaultProfile));
            toolRow.Add(CreateButton("重置推挤记录", ResetProbeHistory));
            toolRow.Add(CreateButton("重绘 SceneView", SceneView.RepaintAll));
            panel.Add(toolRow);

            m_lastRefreshValue = new Label();
            m_lastRefreshValue.style.marginTop = 4;
            m_lastRefreshValue.style.color = new StyleColor(MutedTextColor);
            panel.Add(m_lastRefreshValue);
            return panel;
        }

        private VisualElement CreateProbePanel()
        {
            VisualElement panel = CreateSection("调试探针", "查看当前场景中的转向调试对象，并快速选中或聚焦。");
            panel.style.flexGrow = 1;

            ScrollView scrollView = new();
            scrollView.style.flexGrow = 1;
            scrollView.style.marginTop = 4;

            m_probeList = new VisualElement();
            m_probeList.style.flexDirection = FlexDirection.Column;
            scrollView.Add(m_probeList);
            panel.Add(scrollView);
            return panel;
        }

        private VisualElement CreateSection(string title, string subtitle)
        {
            VisualElement panel = CreateCard();
            panel.style.marginBottom = 10;

            Label titleLabel = new(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            panel.Add(titleLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                Label subtitleLabel = new(subtitle);
                subtitleLabel.style.marginTop = 3;
                subtitleLabel.style.marginBottom = 8;
                subtitleLabel.style.color = new StyleColor(MutedTextColor);
                panel.Add(subtitleLabel);
            }

            return panel;
        }

        private VisualElement CreateCard()
        {
            VisualElement card = new();
            card.style.backgroundColor = new StyleColor(PanelColor);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new StyleColor(BorderColor);
            card.style.borderBottomColor = new StyleColor(BorderColor);
            card.style.borderLeftColor = new StyleColor(BorderColor);
            card.style.borderRightColor = new StyleColor(BorderColor);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.paddingLeft = 12;
            card.style.paddingRight = 12;
            card.style.paddingTop = 10;
            card.style.paddingBottom = 10;
            return card;
        }

        private VisualElement CreateButtonRow()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 8;
            return row;
        }

        private Button CreateButton(string text, Action action)
        {
            Button button = new(action) { text = text };
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            button.style.minWidth = 92;
            button.style.height = 28;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            return button;
        }

        private void RefreshAll()
        {
            m_lastRefreshTime = EditorApplication.timeSinceStartup;

            ContextSteeringSimulation2D simulation = ContextSteeringSimulation2D.Current;
            ContextSteeringDebugProbe2D[] probes = FindProbes();
            int snapshotCount = 0;
            for (int i = 0; i < probes.Length; i++)
            {
                if (probes[i] != null && probes[i].HasSnapshot)
                {
                    snapshotCount++;
                }
            }

            SetLabel(m_playModeValue, EditorApplication.isPlaying ? "运行中" : "未运行");
            SetLabel(m_simulationValue, simulation != null ? "已存在" : "未找到");
            SetLabel(m_agentCountValue, simulation != null ? simulation.AgentCount.ToString() : "0");
            SetLabel(m_probeCountValue, probes.Length.ToString());
            SetLabel(m_snapshotCountValue, snapshotCount.ToString());
            SetLabel(m_lastRefreshValue, $"最后刷新：{DateTime.Now:HH:mm:ss}");

            RebuildProbeList(probes);
        }

        private void RebuildProbeList(ContextSteeringDebugProbe2D[] probes)
        {
            if (m_probeList == null)
            {
                return;
            }

            m_probeList.Clear();

            if (probes.Length == 0)
            {
                Label empty = new("当前场景没有转向调试探针。运行带 ContextSteeringDebugProbe2D 的 NPC 后会显示在这里。");
                empty.style.color = new StyleColor(MutedTextColor);
                empty.style.whiteSpace = WhiteSpace.Normal;
                m_probeList.Add(empty);
                return;
            }

            for (int i = 0; i < probes.Length; i++)
            {
                ContextSteeringDebugProbe2D probe = probes[i];
                if (probe == null)
                {
                    continue;
                }

                m_probeList.Add(CreateProbeRow(probe));
            }
        }

        private VisualElement CreateProbeRow(ContextSteeringDebugProbe2D probe)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 8;
            row.style.paddingBottom = 8;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(BorderColor);

            VisualElement info = new();
            info.style.flexGrow = 1;
            info.style.flexDirection = FlexDirection.Column;

            Label title = new(probe.gameObject.name);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            info.Add(title);

            Label detail = new(BuildProbeDetail(probe));
            detail.style.color = new StyleColor(MutedTextColor);
            detail.style.whiteSpace = WhiteSpace.Normal;
            info.Add(detail);
            row.Add(info);

            row.Add(CreateButton("选中", () => SelectProbe(probe)));
            row.Add(CreateButton("聚焦", () => FocusProbe(probe)));
            return row;
        }

        private string BuildProbeDetail(ContextSteeringDebugProbe2D probe)
        {
            string drawState = probe.DrawSceneView
                ? probe.DrawWhenNotSelected ? "显示全部" : "仅选中"
                : "已关闭";

            if (!probe.HasSnapshot)
            {
                return $"显示：{drawState} | 快照：暂无";
            }

            SteeringDebugSnapshot2D snapshot = probe.Snapshot;
            float pushPeak = Mathf.Sqrt(Mathf.Max(0.0f, probe.MaximumObservedPushCorrectionSqrMagnitude));
            return
                $"显示：{drawState} | 快照：有 | 行为组：{snapshot.BehaviourGroupId} | 邻居：{snapshot.Neighbours.Length} | " +
                $"碰撞体：{snapshot.DetectedColliderCount} | 目标：{FormatNullableVector(snapshot.TargetPosition)} | " +
                $"安全速度：{FormatVector(snapshot.Result.SafeVelocity)} | 推挤峰值：{pushPeak:0.000}";
        }

        private void ApplyDisplayMode(bool drawSceneView, bool drawWhenNotSelected)
        {
            ContextSteeringDebugProbe2D[] probes = FindProbes();
            for (int i = 0; i < probes.Length; i++)
            {
                ContextSteeringDebugProbe2D probe = probes[i];
                if (probe == null)
                {
                    continue;
                }

                Undo.RecordObject(probe, "设置转向调试显示");
                probe.DrawSceneView = drawSceneView;
                probe.DrawWhenNotSelected = drawWhenNotSelected;
                EditorUtility.SetDirty(probe);
            }

            SceneView.RepaintAll();
            RefreshAll();
        }

        private void ResetProbeHistory()
        {
            ContextSteeringDebugProbe2D[] probes = FindProbes();
            for (int i = 0; i < probes.Length; i++)
            {
                ContextSteeringDebugProbe2D probe = probes[i];
                if (probe == null)
                {
                    continue;
                }

                Undo.RecordObject(probe, "重置转向调试历史");
                probe.ResetHistory();
                EditorUtility.SetDirty(probe);
            }

            RefreshAll();
        }

        private static void OpenDefaultProfile()
        {
            UnityEngine.Object profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(DefaultProfilePath);
            if (profile == null)
            {
                Debug.LogWarning($"没有找到默认转向配置：{DefaultProfilePath}");
                return;
            }

            Selection.activeObject = profile;
            EditorGUIUtility.PingObject(profile);
        }

        private static void SelectProbe(ContextSteeringDebugProbe2D probe)
        {
            if (probe == null)
            {
                return;
            }

            Selection.activeGameObject = probe.gameObject;
            EditorGUIUtility.PingObject(probe.gameObject);
            SceneView.RepaintAll();
        }

        private static void FocusProbe(ContextSteeringDebugProbe2D probe)
        {
            SelectProbe(probe);

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            sceneView.FrameSelected();
            sceneView.Repaint();
        }

        private static ContextSteeringDebugProbe2D[] FindProbes()
        {
            return UnityEngine.Object.FindObjectsByType<ContextSteeringDebugProbe2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        private static void SetLabel(Label label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static string FormatNullableVector(Vector2? value)
        {
            return value.HasValue ? FormatVector(value.Value) : "无";
        }

        private static string FormatVector(Vector2 value)
        {
            return $"({value.x:0.00}, {value.y:0.00})";
        }
    }
}
#endif
