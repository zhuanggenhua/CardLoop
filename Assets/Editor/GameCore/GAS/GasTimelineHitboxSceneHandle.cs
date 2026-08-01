using System.Collections.Generic;
using System;
using System.Linq;
using System.Reflection;
using FantasyWord.GameCore;
using GAS.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FantasyWord.GameCore.Editor
{
    [InitializeOnLoad]
    internal static class GasTimelineHitboxSceneHandle
    {
        private const float MinimumSize = 0.01f;
        private const BindingFlags InstanceBindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticBindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static GasTimelineHitboxSceneHandle()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!TryGetSelectedApplyEffects(out TimelineWindowContext context, out XParamApplyEffects applyEffects))
            {
                return;
            }

            if (applyEffects.Param is XParamCatchAreaPolygon2D polygonParameter)
            {
                DrawPolygonHandles(sceneView, context, context.PreviewObject, polygonParameter);
                return;
            }

            if (applyEffects.Param is not XParamCatchAreaBox2D parameter)
            {
                return;
            }

            GameObject previewObject = context.PreviewObject;
            if (!TryResolvePose(previewObject, parameter, out Vector3 center, out float angle))
            {
                return;
            }

            DrawBox(center, parameter.size, angle);

            EditorGUI.BeginChangeCheck();
            Vector3 movedCenter = Handles.PositionHandle(center, Quaternion.Euler(0f, 0f, angle));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(context.Window, "编辑 GAS 命中框偏移");
                parameter.SetOffset(parameter.isWorldSpace
                    ? (Vector2)movedCenter
                    : WorldToLocalOffset(previewObject, parameter, movedCenter));
                context.RefreshInspector();
                sceneView.Repaint();
            }

            Vector3 right = Rotate(Vector3.right, angle);
            Vector3 up = Rotate(Vector3.up, angle);
            float handleSize = HandleUtility.GetHandleSize(center) * 0.08f;

            DrawSizeHandle(sceneView, context, previewObject, parameter, center, right, parameter.size.x, true, handleSize);
            DrawSizeHandle(sceneView, context, previewObject, parameter, center, -right, parameter.size.x, true, handleSize);
            DrawSizeHandle(sceneView, context, previewObject, parameter, center, up, parameter.size.y, false, handleSize);
            DrawSizeHandle(sceneView, context, previewObject, parameter, center, -up, parameter.size.y, false, handleSize);

            EditorGUI.BeginChangeCheck();
            Quaternion rotation = Handles.RotationHandle(Quaternion.Euler(0f, 0f, angle), center);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(context.Window, "编辑 GAS 命中框旋转");
                float worldAngle = NormalizeAngle(rotation.eulerAngles.z);
                parameter.SetRotation(parameter.isWorldSpace
                    ? worldAngle
                    : WorldToLocalRotation(previewObject, worldAngle));
                context.RefreshInspector();
                sceneView.Repaint();
            }
        }

        private static bool TryGetSelectedApplyEffects(out TimelineWindowContext context, out XParamApplyEffects parameter)
        {
            context = default;
            parameter = null;

            Type windowType = ResolveAbilityTimelineEditorWindowType();
            if (windowType == null)
            {
                return false;
            }

            if (windowType.GetProperty("Instance", StaticBindingFlags)?.GetValue(null) is not EditorWindow window)
            {
                return false;
            }

            object currentInspectorObject = windowType.GetProperty("CurrentInspectorObject", InstanceBindingFlags)?.GetValue(window);
            object timelineInspector = windowType.GetProperty("TimelineInspector", InstanceBindingFlags)?.GetValue(window);
            if (currentInspectorObject == null && timelineInspector != null)
            {
                currentInspectorObject = timelineInspector.GetType()
                    .GetProperty("CurrentInspectorObject", InstanceBindingFlags)?
                    .GetValue(timelineInspector);
            }

            object taskClipData = GetInstanceMemberValue(currentInspectorObject, "TaskClipData");
            object parameterSource = taskClipData ?? currentInspectorObject;
            if (GetInstanceMemberValue(parameterSource, "Parameter") is not XParamApplyEffects applyEffects)
            {
                return false;
            }

            GameObject previewObject = ResolvePreviewObject(windowType, window);
            context = new TimelineWindowContext(window, previewObject, timelineInspector);
            parameter = applyEffects;
            return true;
        }

        private static object GetInstanceMemberValue(object source, string memberName)
        {
            if (source == null)
            {
                return null;
            }

            Type type = source.GetType();
            return type.GetProperty(memberName, InstanceBindingFlags)?.GetValue(source)
                   ?? type.GetField(memberName, InstanceBindingFlags)?.GetValue(source);
        }

        private static GameObject ResolvePreviewObject(Type windowType, EditorWindow window)
        {
            try
            {
                if (windowType.GetProperty("PreviewObject", InstanceBindingFlags)?.GetValue(window) is GameObject previewObject)
                {
                    return previewObject;
                }
            }
            catch (TargetInvocationException)
            {
                // EX-GAS 原属性在窗口未完全初始化时会抛异常；继续从原 ObjectField 读取。
            }

            object previewObjectField = windowType.GetField("_previewObjectField", InstanceBindingFlags)?.GetValue(window)
                ?? window.rootVisualElement.Query<UnityEditor.UIElements.ObjectField>("PreviewInstance").First();
            object fieldValue = previewObjectField?.GetType().GetProperty("value", InstanceBindingFlags)?.GetValue(previewObjectField);
            return fieldValue as GameObject;
        }

        private static Type ResolveAbilityTimelineEditorWindowType()
        {
            Type windowType = Type.GetType("GAS.Editor.AbilityTimelineEditorWindow, com.exhard.exgas.editor");
            if (windowType != null)
            {
                return windowType;
            }

            windowType = Type.GetType("GAS.Editor.AbilityTimelineEditorWindow, GAS.Editor");
            if (windowType != null)
            {
                return windowType;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("GAS.Editor.AbilityTimelineEditorWindow"))
                .FirstOrDefault(type => type != null);
        }

        private static void DrawPolygonHandles(
            SceneView sceneView,
            TimelineWindowContext context,
            GameObject previewObject,
            XParamCatchAreaPolygon2D parameter)
        {
            if (!TryResolvePolygonPose(previewObject, parameter, out Vector2 origin, out float facingAngle))
            {
                return;
            }

            int pointCount = parameter.Points.Count;
            if (pointCount < 3)
            {
                return;
            }

            Vector3[] worldPoints = new Vector3[pointCount + 1];
            for (int i = 0; i < pointCount; i++)
            {
                worldPoints[i] = LocalPolygonPointToWorld(parameter, origin, facingAngle, parameter.Points[i]);
            }

            worldPoints[pointCount] = worldPoints[0];
            using (new Handles.DrawingScope(Color.green))
            {
                Handles.DrawAAPolyLine(3f, worldPoints);
            }

            Vector2 worldCenter = Vector2.zero;
            for (int i = 0; i < pointCount; i++)
            {
                worldCenter += (Vector2)worldPoints[i];
            }

            worldCenter /= pointCount;

            EditorGUI.BeginChangeCheck();
            Vector3 movedCenter = Handles.PositionHandle(worldCenter, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Vector2 delta = movedCenter - (Vector3)worldCenter;
                List<Vector2> movedPoints = new(pointCount);
                for (int i = 0; i < pointCount; i++)
                {
                    Vector2 movedWorldPoint = (Vector2)worldPoints[i] + delta;
                    movedPoints.Add(WorldPolygonPointToLocal(previewObject, parameter, movedWorldPoint));
                }

                Undo.RecordObject(context.Window, "移动 GAS 多边形命中框");
                parameter.SetPoints(movedPoints);
                context.RefreshInspector();
                sceneView.Repaint();
            }

            float handleSize = HandleUtility.GetHandleSize(worldCenter) * 0.06f;
            for (int i = 0; i < pointCount; i++)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 movedPoint = Handles.FreeMoveHandle(worldPoints[i], handleSize, Vector3.zero, Handles.DotHandleCap);
                if (!EditorGUI.EndChangeCheck())
                {
                    continue;
                }

                Undo.RecordObject(context.Window, "编辑 GAS 多边形顶点");
                parameter.MovePoint(i, WorldPolygonPointToLocal(previewObject, parameter, movedPoint));
                context.RefreshInspector();
                sceneView.Repaint();
            }

            Event evt = Event.current;
            if (evt == null || evt.type != EventType.MouseDown || evt.button != 1)
            {
                return;
            }

            Vector2 mousePosition = evt.mousePosition;
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("插入顶点"), false, () =>
            {
                int insertIndex = FindNearestEdgeIndex(worldPoints, mousePosition, pointCount);
                Vector2 insertWorldPoint = GetNearestEdgePoint(worldPoints, mousePosition, pointCount);
                Undo.RecordObject(context.Window, "插入 GAS 多边形顶点");
                parameter.InsertPoint(insertIndex + 1, WorldPolygonPointToLocal(previewObject, parameter, insertWorldPoint));
                context.RefreshInspector();
                sceneView.Repaint();
            });

            int nearestPoint = FindNearestPointIndex(worldPoints, mousePosition, pointCount);
            if (parameter.Points.Count > 3 && nearestPoint >= 0)
            {
                menu.AddItem(new GUIContent("删除最近顶点"), false, () =>
                {
                    Undo.RecordObject(context.Window, "删除 GAS 多边形顶点");
                    parameter.RemovePoint(nearestPoint);
                    context.RefreshInspector();
                    sceneView.Repaint();
                });
            }
            else
            {
                menu.AddDisabledItem(new GUIContent("删除最近顶点"));
            }

            menu.ShowAsContext();
            evt.Use();
        }

        private static void DrawSizeHandle(
            SceneView sceneView,
            TimelineWindowContext context,
            GameObject previewObject,
            XParamCatchAreaBox2D parameter,
            Vector3 center,
            Vector3 outward,
            float axisSize,
            bool editWidth,
            float handleSize)
        {
            Vector3 oldHandle = center + outward * (axisSize * 0.5f);
            EditorGUI.BeginChangeCheck();
            Vector3 newHandle = Handles.Slider(oldHandle, outward, handleSize, Handles.CubeHandleCap, 0f);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            float rawDelta = Vector3.Dot(newHandle - oldHandle, outward);
            float newAxisSize = Mathf.Max(MinimumSize, axisSize + rawDelta);
            float appliedDelta = newAxisSize - axisSize;
            Vector3 newCenter = center + outward * (appliedDelta * 0.5f);
            Vector2 newSize = editWidth
                ? new Vector2(newAxisSize, parameter.size.y)
                : new Vector2(parameter.size.x, newAxisSize);

            Undo.RecordObject(context.Window, "编辑 GAS 命中框大小");
            parameter.SetSize(newSize);
            parameter.SetOffset(parameter.isWorldSpace
                ? (Vector2)newCenter
                : WorldToLocalOffset(previewObject, parameter, newCenter));
            context.RefreshInspector();
            sceneView.Repaint();
        }

        private static bool TryResolvePose(GameObject previewObject, XParamCatchAreaBox2D parameter, out Vector3 center, out float angle)
        {
            center = default;
            angle = default;

            if (parameter.isWorldSpace)
            {
                center = parameter.offset;
                angle = parameter.rotation;
                return true;
            }

            if (previewObject == null)
            {
                return false;
            }

            Transform sourceTransform = previewObject.transform;
            Movable movable = previewObject.GetComponent<Movable>();
            if (movable != null && movable.TryGetGas2DFacingDirection(out Vector2 direction))
            {
                float facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                angle = facingAngle + parameter.rotation;
                center = (Vector2)sourceTransform.position + Rotate(parameter.offset, facingAngle);
                return true;
            }

            center = sourceTransform.TransformPoint(parameter.offset);
            angle = sourceTransform.eulerAngles.z + parameter.rotation;
            return true;
        }

        private static Vector2 WorldToLocalOffset(GameObject previewObject, XParamCatchAreaBox2D parameter, Vector3 worldCenter)
        {
            Transform sourceTransform = previewObject.transform;
            Movable movable = previewObject.GetComponent<Movable>();
            if (movable != null && movable.TryGetGas2DFacingDirection(out Vector2 direction))
            {
                float facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                return Rotate((Vector2)(worldCenter - sourceTransform.position), -facingAngle);
            }

            return sourceTransform.InverseTransformPoint(worldCenter);
        }

        private static float WorldToLocalRotation(GameObject previewObject, float worldAngle)
        {
            Movable movable = previewObject.GetComponent<Movable>();
            if (movable != null && movable.TryGetGas2DFacingDirection(out Vector2 direction))
            {
                float facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                return NormalizeAngle(worldAngle - facingAngle);
            }

            return NormalizeAngle(worldAngle - previewObject.transform.eulerAngles.z);
        }

        private static bool TryResolvePolygonPose(GameObject previewObject, XParamCatchAreaPolygon2D parameter, out Vector2 origin, out float facingAngle)
        {
            origin = Vector2.zero;
            facingAngle = 0.0f;

            if (parameter.isWorldSpace)
            {
                return true;
            }

            if (previewObject == null)
            {
                return false;
            }

            origin = previewObject.transform.position;
            Movable movable = previewObject.GetComponent<Movable>();
            if (movable != null && movable.TryGetGas2DFacingDirection(out Vector2 direction))
            {
                facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                return true;
            }

            facingAngle = previewObject.transform.eulerAngles.z;
            return true;
        }

        private static Vector2 LocalPolygonPointToWorld(XParamCatchAreaPolygon2D parameter, Vector2 origin, float facingAngle, Vector2 localPoint)
        {
            return parameter.isWorldSpace ? localPoint : origin + Rotate(localPoint, facingAngle);
        }

        private static Vector2 WorldPolygonPointToLocal(GameObject previewObject, XParamCatchAreaPolygon2D parameter, Vector2 worldPoint)
        {
            if (parameter.isWorldSpace)
            {
                return worldPoint;
            }

            if (previewObject == null)
            {
                return worldPoint;
            }

            Movable movable = previewObject.GetComponent<Movable>();
            if (movable != null && movable.TryGetGas2DFacingDirection(out Vector2 direction))
            {
                float facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                return Rotate(worldPoint - (Vector2)previewObject.transform.position, -facingAngle);
            }

            return previewObject.transform.InverseTransformPoint(worldPoint);
        }

        private static int FindNearestPointIndex(Vector3[] worldPoints, Vector2 mousePosition, int pointCount)
        {
            int nearestIndex = -1;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < pointCount; i++)
            {
                float distance = Vector2.Distance(HandleUtility.WorldToGUIPoint(worldPoints[i]), mousePosition);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestDistance <= 24.0f ? nearestIndex : -1;
        }

        private static int FindNearestEdgeIndex(Vector3[] worldPoints, Vector2 mousePosition, int pointCount)
        {
            int nearestIndex = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < pointCount; i++)
            {
                Vector2 a = HandleUtility.WorldToGUIPoint(worldPoints[i]);
                Vector2 b = HandleUtility.WorldToGUIPoint(worldPoints[(i + 1) % pointCount]);
                float distance = DistancePointToSegment(mousePosition, a, b);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private static Vector2 GetNearestEdgePoint(Vector3[] worldPoints, Vector2 mousePosition, int pointCount)
        {
            int edgeIndex = FindNearestEdgeIndex(worldPoints, mousePosition, pointCount);
            Vector3 a = worldPoints[edgeIndex];
            Vector3 b = worldPoints[(edgeIndex + 1) % pointCount];
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane plane = new(Vector3.forward, a);
            if (!plane.Raycast(ray, out float enter))
            {
                return (a + b) * 0.5f;
            }

            Vector3 mouseWorld = ray.GetPoint(enter);
            Vector3 edge = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(mouseWorld - a, edge) / edge.sqrMagnitude);
            return a + edge * t;
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
            {
                return Vector2.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            return Vector2.Distance(point, a + segment * t);
        }

        private static void DrawBox(Vector3 center, Vector2 size, float angle)
        {
            Vector3 right = Rotate(Vector3.right, angle) * (size.x * 0.5f);
            Vector3 up = Rotate(Vector3.up, angle) * (size.y * 0.5f);

            Vector3 p0 = center - right - up;
            Vector3 p1 = center + right - up;
            Vector3 p2 = center + right + up;
            Vector3 p3 = center - right + up;

            using (new Handles.DrawingScope(Color.green))
            {
                Handles.DrawAAPolyLine(3f, p0, p1, p2, p3, p0);
            }
        }

        private static Vector3 Rotate(Vector3 value, float degrees)
        {
            return Quaternion.Euler(0f, 0f, degrees) * value;
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(
                value.x * cos - value.y * sin,
                value.x * sin + value.y * cos);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f)
            {
                angle -= 360f;
            }
            else if (angle < -180f)
            {
                angle += 360f;
            }

            return angle;
        }

        private readonly struct TimelineWindowContext
        {
            public TimelineWindowContext(EditorWindow window, GameObject previewObject, object timelineInspector)
            {
                Window = window;
                PreviewObject = previewObject;
                _timelineInspector = timelineInspector;
            }

            public readonly EditorWindow Window;
            public readonly GameObject PreviewObject;
            private readonly object _timelineInspector;

            public void RefreshInspector()
            {
                _timelineInspector?.GetType().GetMethod("RefreshInspector", InstanceBindingFlags)?.Invoke(_timelineInspector, null);
            }
        }
    }
}
