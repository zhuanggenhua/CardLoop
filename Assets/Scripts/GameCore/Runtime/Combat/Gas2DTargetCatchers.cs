using System;
using System.Collections.Generic;
using GAS.General;
using GAS.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FantasyWord.GameCore
{
    /// <summary>
    /// EX-GAS 2D 命中范围的运行时调试绘制工具。
    /// 只在编辑器或开发构建中生成短生命周期线框，不参与正式目标筛选结果。
    /// </summary>
    internal static class Gas2DTargetCatcherRuntimeDebug
    {
        private static readonly Color HitRangeColor = new(0.0f, 1.0f, 0.15f, 0.85f);
        private static Material s_lineMaterial;
        private const float HitRangeDuration = 0.25f;
        private const float HitRangeWidth = 0.035f;

        public static void DrawBox(Vector2 center, Vector2 size, float angle)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Vector2 halfSize = size * 0.5f;
            Vector2[] corners =
            {
                Rotate(new Vector2(-halfSize.x, -halfSize.y), angle) + center,
                Rotate(new Vector2(-halfSize.x, halfSize.y), angle) + center,
                Rotate(new Vector2(halfSize.x, halfSize.y), angle) + center,
                Rotate(new Vector2(halfSize.x, -halfSize.y), angle) + center
            };
            DrawPolygon(corners, corners.Length);
#endif
        }

        public static void DrawCircle(Vector2 center, float radius)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            const int segmentCount = 48;
            Vector2[] points = new Vector2[segmentCount];
            for (int i = 0; i < segmentCount; i++)
            {
                float radians = (Mathf.PI * 2.0f * i) / segmentCount;
                points[i] = center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
            }

            DrawPolygon(points, segmentCount);
#endif
        }

        public static void DrawPolygon(Vector2[] points, int pointCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (points == null || pointCount < 3)
            {
                return;
            }

            GameObject lineObject = new("GAS Attack Hit Range");
            lineObject.hideFlags = HideFlags.HideAndDontSave;
            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.positionCount = pointCount;
            lineRenderer.startColor = HitRangeColor;
            lineRenderer.endColor = HitRangeColor;
            lineRenderer.startWidth = HitRangeWidth;
            lineRenderer.endWidth = HitRangeWidth;
            lineRenderer.numCapVertices = 3;
            lineRenderer.numCornerVertices = 3;
            lineRenderer.sharedMaterial = GetLineMaterial();
            lineRenderer.textureMode = LineTextureMode.Stretch;
            lineRenderer.sortingOrder = 32762;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            for (int i = 0; i < pointCount; i++)
            {
                Vector2 point = points[i];
                lineRenderer.SetPosition(i, new Vector3(point.x, point.y, 0.0f));
            }

            DestroyLineObject(lineObject);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void DestroyLineObject(GameObject lineObject)
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(lineObject, HitRangeDuration);
                return;
            }

#if UNITY_EDITOR
            UnityEngine.Object.DestroyImmediate(lineObject);
#else
            UnityEngine.Object.Destroy(lineObject);
#endif
        }

        private static Material GetLineMaterial()
        {
            if (s_lineMaterial != null)
            {
                return s_lineMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                shader = Shader.Find("Hidden/Internal-Colored");
            }

            if (shader == null)
            {
                return null;
            }

            s_lineMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return s_lineMaterial;
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
#endif
    }

    /// <summary>
    /// 从施法者解析 2D 命中方向的统一入口。
    /// 运行时必须读取 Movable 的命中帧朝向；编辑器预览才允许退回 Transform 方向。
    /// </summary>
    internal static class Gas2DTargetCatcherDirectionResolver
    {
        private const float DirectionEpsilon = 0.0001f;

        public static bool TryResolveRuntime(
            GameObject source,
            out Vector2 direction)
        {
            direction = default;
            if (source == null)
            {
                Debug.LogError(
                    "EX-GAS 2D 目标捕获缺少施法者对象，无法读取命中帧姿态。");
                return false;
            }

            Movable movable = source.GetComponent<Movable>();
            if (movable == null)
            {
                Debug.LogError(
                    "EX-GAS 2D 目标捕获要求施法者挂载 Movable，以读取命中帧的当前朝向。",
                    source);
                return false;
            }

            if (!movable.TryGetGas2DFacingDirection(out direction))
            {
                Debug.LogError(
                    "EX-GAS 2D 目标捕获无法取得施法者在命中帧的当前朝向。",
                    source);
                return false;
            }

            return true;
        }

        public static bool TryResolvePreview(GameObject source, out Vector2 direction)
        {
            direction = default;
            if (source == null)
            {
                return false;
            }

            Movable movable = source.GetComponent<Movable>();
            if (movable != null && movable.TryGetGas2DFacingDirection(out direction))
            {
                return true;
            }

            Vector3 transformRight = source.transform.right;
            direction = new Vector2(transformRight.x, transformRight.y);
            if (direction.sqrMagnitude <= DirectionEpsilon)
            {
                return false;
            }

            direction.Normalize();
            return true;
        }
    }

    /// <summary>
    /// EX-GAS 的 2D 矩形目标捕获器。
    /// 支持世界坐标和相对施法者朝向两种模式，命中结果统一返回 AbilitySystemCell。
    /// </summary>
    public sealed class CatchAreaBox2D : TargetCatcherBase<XParamCatchAreaBox2D>
    {
        private static readonly Collider2D[] Colliders = new Collider2D[64];
        private static readonly HashSet<AbilitySystemCell> UniqueTargets = new();

        protected override void CatchTargetsNonAlloc(AbilitySystemCell mainTarget, List<AbilitySystemCell> results)
        {
            if (Parameter == null || results == null)
            {
                return;
            }

            int count;
            Vector2 debugCenter;
            float debugAngle;
            if (Parameter.isWorldSpace)
            {
                debugCenter = Parameter.offset;
                debugAngle = Parameter.rotation;
                count = Physics2D.OverlapBoxNonAlloc(
                    debugCenter,
                    Parameter.size,
                    debugAngle,
                    Colliders,
                    Parameter.layer.value);
            }
            else
            {
                if (Owner?.GameObject == null)
                {
                    return;
                }

                if (!TryResolveRelativePose(
                        Owner.GameObject,
                        Parameter.offset,
                        Parameter.rotation,
                        true,
                        out Vector2 center,
                        out float angle))
                {
                    return;
                }

                debugCenter = center;
                debugAngle = angle;
                count = Physics2D.OverlapBoxNonAlloc(
                    debugCenter,
                    Parameter.size,
                    debugAngle,
                    Colliders,
                    Parameter.layer.value);
            }

            Gas2DTargetCatcherRuntimeDebug.DrawBox(debugCenter, Parameter.size, debugAngle);

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = Colliders[i];
                if (collider == null)
                {
                    continue;
                }

                AbilitySystemComponent asc = collider.GetComponentInParent<AbilitySystemComponent>();
                AbilitySystemCell cell = asc != null ? asc.Cell : null;
                if (cell == null || cell == Owner || !UniqueTargets.Add(cell))
                {
                    continue;
                }

                results.Add(cell);
            }

            ClearColliderCache(count);
            UniqueTargets.Clear();
        }

        public override void OnEditorPreview(GameObject obj)
        {
#if UNITY_EDITOR
            if (Parameter == null)
            {
                return;
            }

            Vector2 center;
            float angle;
            if (Parameter.isWorldSpace)
            {
                center = Parameter.offset;
                angle = Parameter.rotation;
            }
            else
            {
                if (obj == null)
                {
                    return;
                }

                if (!TryResolveRelativePose(
                        obj,
                        Parameter.offset,
                        Parameter.rotation,
                        false,
                        out center,
                        out angle))
                {
                    return;
                }
            }

            DebugExtension.DebugBox(center, Parameter.size, angle, Color.green, 0.1f);
#endif
        }

        private bool TryResolveRelativePose(
            GameObject source,
            Vector2 offset,
            float localRotation,
            bool useRuntimePose,
            out Vector2 center,
            out float angle)
        {
            Transform sourceTransform = source.transform;
            Vector2 direction;
            bool hasDirection = useRuntimePose
                ? Gas2DTargetCatcherDirectionResolver.TryResolveRuntime(source, out direction)
                : Gas2DTargetCatcherDirectionResolver.TryResolvePreview(source, out direction);
            if (!hasDirection)
            {
                center = default;
                angle = 0.0f;
                return false;
            }

            float facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            angle = facingAngle + localRotation;
            center = (Vector2)sourceTransform.position + Rotate(offset, facingAngle);
            return true;
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

        private static void ClearColliderCache(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Colliders[i] = null;
            }
        }
    }

    /// <summary>
    /// EX-GAS 的 2D 圆形目标捕获器。
    /// 用于范围技能和以施法者朝向偏移的圆形命中区。
    /// </summary>
    public sealed class CatchAreaCircle2D : TargetCatcherBase<XParamCatchAreaCircle2D>
    {
        private static readonly Collider2D[] Colliders = new Collider2D[64];
        private static readonly HashSet<AbilitySystemCell> UniqueTargets = new();

        protected override void CatchTargetsNonAlloc(AbilitySystemCell mainTarget, List<AbilitySystemCell> results)
        {
            if (Parameter == null || results == null)
            {
                return;
            }

            int count;
            Vector2 debugCenter;
            if (Parameter.isWorldSpace)
            {
                debugCenter = Parameter.offset;
                count = Physics2D.OverlapCircleNonAlloc(
                    debugCenter,
                    Parameter.radius,
                    Colliders,
                    Parameter.layer.value);
            }
            else
            {
                if (Owner?.GameObject == null)
                {
                    return;
                }

                if (!TryResolveRelativeCenter(
                        Owner.GameObject,
                        Parameter.offset,
                        true,
                        out Vector2 center))
                {
                    return;
                }

                debugCenter = center;
                count = Physics2D.OverlapCircleNonAlloc(
                    debugCenter,
                    Parameter.radius,
                    Colliders,
                    Parameter.layer.value);
            }

            Gas2DTargetCatcherRuntimeDebug.DrawCircle(debugCenter, Parameter.radius);

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = Colliders[i];
                if (collider == null)
                {
                    continue;
                }

                AbilitySystemComponent asc = collider.GetComponentInParent<AbilitySystemComponent>();
                AbilitySystemCell cell = asc != null ? asc.Cell : null;
                if (cell == null || cell == Owner || !UniqueTargets.Add(cell))
                {
                    continue;
                }

                results.Add(cell);
            }

            ClearColliderCache(count);
            UniqueTargets.Clear();
        }

        public override void OnEditorPreview(GameObject obj)
        {
#if UNITY_EDITOR
            if (Parameter == null)
            {
                return;
            }

            Vector2 center;
            if (Parameter.isWorldSpace)
            {
                center = Parameter.offset;
            }
            else
            {
                if (obj == null)
                {
                    return;
                }

                if (!TryResolveRelativeCenter(obj, Parameter.offset, false, out center))
                {
                    return;
                }
            }

            DebugExtension.DebugDrawCircle(center, Parameter.radius, Color.green, 0.1f);
#endif
        }

        private bool TryResolveRelativeCenter(
            GameObject source,
            Vector2 offset,
            bool useRuntimePose,
            out Vector2 center)
        {
            Transform sourceTransform = source.transform;
            Vector2 direction;
            bool hasDirection = useRuntimePose
                ? Gas2DTargetCatcherDirectionResolver.TryResolveRuntime(source, out direction)
                : Gas2DTargetCatcherDirectionResolver.TryResolvePreview(source, out direction);
            if (!hasDirection)
            {
                center = default;
                return false;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            center = (Vector2)sourceTransform.position + Rotate(offset, angle);
            return true;
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

        private static void ClearColliderCache(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Colliders[i] = null;
            }
        }
    }

    /// <summary>
    /// EX-GAS 的 2D 多边形目标捕获器。
    /// 用于火焰、扇形或不规则命中区，运行时先用包围盒粗筛再做多边形相交判断。
    /// </summary>
    public sealed class CatchAreaPolygon2D : TargetCatcherBase<XParamCatchAreaPolygon2D>
    {
        private static readonly Collider2D[] Colliders = new Collider2D[64];
        private static readonly HashSet<AbilitySystemCell> UniqueTargets = new();
        private static readonly Vector2[] WorldPoints = new Vector2[XParamCatchAreaPolygon2D.MaxPointCount];
        private static readonly Vector2[] BoxCorners = new Vector2[4];
        private static readonly Vector2[] BoundsCorners = new Vector2[4];

        protected override void CatchTargetsNonAlloc(AbilitySystemCell mainTarget, List<AbilitySystemCell> results)
        {
            if (Parameter == null || results == null || Parameter.Points.Count < 3)
            {
                return;
            }

            if (!TryBuildWorldPolygon(mainTarget, out int pointCount, out Bounds bounds))
            {
                return;
            }

            int count = Physics2D.OverlapBoxNonAlloc(
                bounds.center,
                bounds.size,
                0.0f,
                Colliders,
                Parameter.layer.value);

            Gas2DTargetCatcherRuntimeDebug.DrawPolygon(WorldPoints, pointCount);

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = Colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!ColliderIntersectsPolygon(collider, WorldPoints, pointCount))
                {
                    continue;
                }

                AbilitySystemComponent asc = collider.GetComponentInParent<AbilitySystemComponent>();
                AbilitySystemCell cell = asc != null ? asc.Cell : null;
                if (cell == null || cell == Owner || !UniqueTargets.Add(cell))
                {
                    continue;
                }

                results.Add(cell);
            }

            ClearColliderCache(count);
            UniqueTargets.Clear();
        }

        public override void OnEditorPreview(GameObject obj)
        {
#if UNITY_EDITOR
            if (Parameter == null || Parameter.Points.Count < 3)
            {
                return;
            }

            if (!TryBuildWorldPolygon(obj, out int pointCount, out _))
            {
                return;
            }

            for (int i = 0; i < pointCount; i++)
            {
                DebugDrawTool.DrawLine(WorldPoints[i], WorldPoints[(i + 1) % pointCount], Color.green, 1.0f, false);
            }
#endif
        }

        private bool TryBuildWorldPolygon(AbilitySystemCell mainTarget, out int pointCount, out Bounds bounds)
        {
            if (Parameter.isWorldSpace)
            {
                return BuildWorldPolygon(null, false, out pointCount, out bounds);
            }

            if (Owner?.GameObject == null)
            {
                pointCount = 0;
                bounds = default;
                return false;
            }

            return BuildWorldPolygon(Owner.GameObject, true, out pointCount, out bounds);
        }

        private bool TryBuildWorldPolygon(GameObject previewObject, out int pointCount, out Bounds bounds)
        {
            if (Parameter.isWorldSpace)
            {
                return BuildWorldPolygon(null, false, out pointCount, out bounds);
            }

            if (previewObject == null)
            {
                pointCount = 0;
                bounds = default;
                return false;
            }

            return BuildWorldPolygon(previewObject, false, out pointCount, out bounds);
        }

        private bool BuildWorldPolygon(
            GameObject source,
            bool useRuntimePose,
            out int pointCount,
            out Bounds bounds)
        {
            pointCount = Mathf.Min(Parameter.Points.Count, XParamCatchAreaPolygon2D.MaxPointCount);
            if (pointCount < 3)
            {
                bounds = default;
                return false;
            }

            float facingAngle = 0.0f;
            Vector2 origin = Vector2.zero;
            Transform sourceTransform = source != null ? source.transform : null;

            if (!Parameter.isWorldSpace && sourceTransform != null)
            {
                origin = sourceTransform.position;
                Vector2 direction;
                bool hasDirection = useRuntimePose
                    ? Gas2DTargetCatcherDirectionResolver.TryResolveRuntime(source, out direction)
                    : Gas2DTargetCatcherDirectionResolver.TryResolvePreview(source, out direction);
                if (!hasDirection)
                {
                    bounds = default;
                    return false;
                }

                facingAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            }

            for (int i = 0; i < pointCount; i++)
            {
                Vector2 local = Parameter.Points[i];
                WorldPoints[i] = Parameter.isWorldSpace
                    ? local
                    : origin + Rotate(local, facingAngle);
            }

            Vector2 min = WorldPoints[0];
            Vector2 max = WorldPoints[0];
            for (int i = 1; i < pointCount; i++)
            {
                min = Vector2.Min(min, WorldPoints[i]);
                max = Vector2.Max(max, WorldPoints[i]);
            }

            Vector2 size = max - min;
            bounds = new Bounds((min + max) * 0.5f, new Vector3(Mathf.Max(size.x, 0.01f), Mathf.Max(size.y, 0.01f), 0.01f));
            return true;
        }

        private static bool ColliderIntersectsPolygon(Collider2D collider, Vector2[] polygon, int pointCount)
        {
            if (collider is BoxCollider2D boxCollider)
            {
                return BoxColliderIntersectsPolygon(boxCollider, polygon, pointCount);
            }

            if (collider is CircleCollider2D circleCollider)
            {
                return CircleColliderIntersectsPolygon(circleCollider, polygon, pointCount);
            }

            return ColliderBoundsIntersectsPolygon(collider, polygon, pointCount);
        }

        private static bool BoxColliderIntersectsPolygon(BoxCollider2D collider, Vector2[] polygon, int pointCount)
        {
            Vector2 halfSize = collider.size * 0.5f;
            Vector2 offset = collider.offset;
            Transform transform = collider.transform;

            BoxCorners[0] = transform.TransformPoint(offset + new Vector2(-halfSize.x, -halfSize.y));
            BoxCorners[1] = transform.TransformPoint(offset + new Vector2(-halfSize.x, halfSize.y));
            BoxCorners[2] = transform.TransformPoint(offset + new Vector2(halfSize.x, halfSize.y));
            BoxCorners[3] = transform.TransformPoint(offset + new Vector2(halfSize.x, -halfSize.y));

            return ShapePointsIntersectPolygon(BoxCorners, BoxCorners.Length, polygon, pointCount, collider);
        }

        private static bool CircleColliderIntersectsPolygon(CircleCollider2D collider, Vector2[] polygon, int pointCount)
        {
            Transform transform = collider.transform;
            Vector2 center = transform.TransformPoint(collider.offset);
            Vector3 scale = transform.lossyScale;
            float radius = collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            float radiusSquared = radius * radius;

            if (IsPointInPolygon(center, polygon, pointCount))
            {
                return true;
            }

            for (int i = 0; i < pointCount; i++)
            {
                if ((polygon[i] - center).sqrMagnitude <= radiusSquared)
                {
                    return true;
                }
            }

            for (int i = 0; i < pointCount; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % pointCount];
                if (SegmentDistanceSquared(center, a, b) <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ColliderBoundsIntersectsPolygon(Collider2D collider, Vector2[] polygon, int pointCount)
        {
            Bounds bounds = collider.bounds;
            Vector2 min = bounds.min;
            Vector2 max = bounds.max;

            BoundsCorners[0] = new Vector2(min.x, min.y);
            BoundsCorners[1] = new Vector2(min.x, max.y);
            BoundsCorners[2] = new Vector2(max.x, max.y);
            BoundsCorners[3] = new Vector2(max.x, min.y);

            return ShapePointsIntersectPolygon(BoundsCorners, BoundsCorners.Length, polygon, pointCount, collider);
        }

        private static bool ShapePointsIntersectPolygon(
            Vector2[] shapePoints,
            int shapePointCount,
            Vector2[] polygon,
            int pointCount,
            Collider2D collider)
        {
            for (int i = 0; i < shapePointCount; i++)
            {
                if (IsPointInPolygon(shapePoints[i], polygon, pointCount))
                {
                    return true;
                }
            }

            for (int i = 0; i < pointCount; i++)
            {
                if (collider.OverlapPoint(polygon[i]))
                {
                    return true;
                }
            }

            for (int i = 0; i < pointCount; i++)
            {
                Vector2 polygonA = polygon[i];
                Vector2 polygonB = polygon[(i + 1) % pointCount];
                for (int j = 0; j < shapePointCount; j++)
                {
                    Vector2 shapeA = shapePoints[j];
                    Vector2 shapeB = shapePoints[(j + 1) % shapePointCount];
                    if (SegmentsIntersect(polygonA, polygonB, shapeA, shapeB))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon, int pointCount)
        {
            bool inside = false;
            for (int i = 0, j = pointCount - 1; i < pointCount; j = i++)
            {
                Vector2 pi = polygon[i];
                Vector2 pj = polygon[j];
                if (((pi.y > point.y) != (pj.y > point.y)) &&
                    point.x < (pj.x - pi.x) * (point.y - pi.y) / (pj.y - pi.y) + pi.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float direction1 = Cross(d - c, a - c);
            float direction2 = Cross(d - c, b - c);
            float direction3 = Cross(b - a, c - a);
            float direction4 = Cross(b - a, d - a);

            if (((direction1 > 0.0f && direction2 < 0.0f) || (direction1 < 0.0f && direction2 > 0.0f)) &&
                ((direction3 > 0.0f && direction4 < 0.0f) || (direction3 < 0.0f && direction4 > 0.0f)))
            {
                return true;
            }

            const float epsilon = 0.0001f;
            return Mathf.Abs(direction1) <= epsilon && IsPointOnSegment(a, c, d) ||
                   Mathf.Abs(direction2) <= epsilon && IsPointOnSegment(b, c, d) ||
                   Mathf.Abs(direction3) <= epsilon && IsPointOnSegment(c, a, b) ||
                   Mathf.Abs(direction4) <= epsilon && IsPointOnSegment(d, a, b);
        }

        private static bool IsPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            return point.x >= Mathf.Min(a.x, b.x) - 0.0001f &&
                   point.x <= Mathf.Max(a.x, b.x) + 0.0001f &&
                   point.y >= Mathf.Min(a.y, b.y) - 0.0001f &&
                   point.y <= Mathf.Max(a.y, b.y) + 0.0001f;
        }

        private static float SegmentDistanceSquared(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
            {
                return (point - a).sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSquared);
            Vector2 closest = a + segment * t;
            return (point - closest).sqrMagnitude;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
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

        private static void ClearColliderCache(int count)
        {
            for (int i = 0; i < count; i++)
            {
                Colliders[i] = null;
            }
        }
    }

    /// <summary>
    /// 矩形 2D 目标捕获参数。
    /// 字段通过 EX-GAS BeanField 暴露给配置表和编辑器，不直接由项目代码任意改写。
    /// </summary>
    [Serializable]
    public sealed class XParamCatchAreaBox2D : XParam
    {
        [ShowInInspector]
        [LabelText("是否是世界空间坐标系")]
        [BeanField(nameof(SetIsWorldSpace), Order = 1)]
        public bool isWorldSpace { get; private set; }

        [ShowInInspector]
        [LabelText("偏移")]
        [BeanField(nameof(SetOffset), Order = 2)]
        public Vector2 offset { get; private set; }

        [ShowInInspector]
        [LabelText("大小")]
        [BeanField(nameof(SetSize), Order = 3)]
        public Vector2 size { get; private set; } = Vector2.one;

        [ShowInInspector]
        [LabelText("旋转")]
        [BeanField(nameof(SetRotation), Order = 4)]
        public float rotation { get; private set; }

        [ShowInInspector]
        [LabelText("监测层级")]
        [BeanField(nameof(SetLayer), LubanType = "int", Order = 5)]
        public LayerMask layer { get; private set; } = ~0;

        public void SetIsWorldSpace(bool value) => isWorldSpace = value;
        public void SetOffset(Vector2 value) => offset = value;
        public void SetSize(Vector2 value) => size = new Vector2(Mathf.Max(0.01f, value.x), Mathf.Max(0.01f, value.y));
        public void SetRotation(float value) => rotation = value;
        public void SetLayer(int value) => layer = value;

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData.Count > 0 && bool.TryParse(paramData[0] as string, out bool parsedBool))
            {
                SetIsWorldSpace(parsedBool);
            }

            if (paramData.Count > 1 && TryParseVector2(paramData[1] as string, out Vector2 parsedOffset))
            {
                SetOffset(parsedOffset);
            }

            if (paramData.Count > 2 && TryParseVector2(paramData[2] as string, out Vector2 parsedSize))
            {
                SetSize(parsedSize);
            }

            if (paramData.Count > 3 && float.TryParse(paramData[3] as string, out float parsedRotation))
            {
                SetRotation(parsedRotation);
            }

            if (paramData.Count > 4 && int.TryParse(paramData[4] as string, out int parsedLayer))
            {
                SetLayer(parsedLayer);
            }
        }

        public List<object> EncodeExcelData()
        {
            return new List<object>
            {
                isWorldSpace.ToString(),
                $"{offset.x},{offset.y}",
                $"{size.x},{size.y}",
                rotation.ToString(),
                layer.value.ToString()
            };
        }
#endif
        internal static bool TryParseVector2(string value, out Vector2 result)
        {
            result = default;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split(',');
            return parts.Length == 2 &&
                   float.TryParse(parts[0], out result.x) &&
                   float.TryParse(parts[1], out result.y);
        }
    }

    /// <summary>
    /// 圆形 2D 目标捕获参数。
    /// </summary>
    [Serializable]
    public sealed class XParamCatchAreaCircle2D : XParam
    {
        [ShowInInspector]
        [LabelText("是否是世界空间坐标系")]
        [BeanField(nameof(SetIsWorldSpace), Order = 1)]
        public bool isWorldSpace { get; private set; }

        [ShowInInspector]
        [LabelText("偏移")]
        [BeanField(nameof(SetOffset), Order = 2)]
        public Vector2 offset { get; private set; }

        [ShowInInspector]
        [LabelText("半径")]
        [BeanField(nameof(SetRadius), Order = 3)]
        public float radius { get; private set; } = 0.5f;

        [ShowInInspector]
        [LabelText("监测层级")]
        [BeanField(nameof(SetLayer), LubanType = "int", Order = 4)]
        public LayerMask layer { get; private set; } = ~0;

        public void SetIsWorldSpace(bool value) => isWorldSpace = value;
        public void SetOffset(Vector2 value) => offset = value;
        public void SetRadius(float value) => radius = Mathf.Max(0.01f, value);
        public void SetLayer(int value) => layer = value;

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData.Count > 0 && bool.TryParse(paramData[0] as string, out bool parsedBool))
            {
                SetIsWorldSpace(parsedBool);
            }

            if (paramData.Count > 1 && XParamCatchAreaBox2D.TryParseVector2(paramData[1] as string, out Vector2 parsedOffset))
            {
                SetOffset(parsedOffset);
            }

            if (paramData.Count > 2 && float.TryParse(paramData[2] as string, out float parsedRadius))
            {
                SetRadius(parsedRadius);
            }

            if (paramData.Count > 3 && int.TryParse(paramData[3] as string, out int parsedLayer))
            {
                SetLayer(parsedLayer);
            }
        }

        public List<object> EncodeExcelData()
        {
            return new List<object>
            {
                isWorldSpace.ToString(),
                $"{offset.x},{offset.y}",
                radius.ToString(),
                layer.value.ToString()
            };
        }
#endif
    }

    /// <summary>
    /// 多边形 2D 目标捕获参数。
    /// points 是 Luban/Excel 的保存格式，编辑器侧会维护同步的顶点列表。
    /// </summary>
    [Serializable]
    public sealed class XParamCatchAreaPolygon2D : XParam
    {
        public const int MaxPointCount = 16;

        [ShowInInspector]
        [LabelText("是否是世界空间坐标系")]
        [BeanField(nameof(SetIsWorldSpace), Order = 1)]
        public bool isWorldSpace { get; private set; }

        [HideInInspector]
        [BeanField(nameof(SetPoints), LubanType = "string", Order = 2)]
        public string points { get; private set; } = "0.2,-0.35;0.95,-0.2;0.95,0.35;0.2,0.45";

        private List<Vector2> _points = new()
        {
            new Vector2(0.2f, -0.35f),
            new Vector2(0.95f, -0.2f),
            new Vector2(0.95f, 0.35f),
            new Vector2(0.2f, 0.45f)
        };

#if UNITY_EDITOR
        [OnInspectorGUI]
        private void DrawEditorPoints()
        {
            _points ??= new List<Vector2>();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("顶点", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这里用 Unity 原生数值字段编辑多边形顶点；points 只作为 Excel/Luban 保存格式。选中时间轴命中任务后，也可以在 SceneView 里拖动顶点。", MessageType.None);

            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                for (int i = 0; i < _points.Count; i++)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"#{i}", GUILayout.Width(28));

                        EditorGUI.BeginChangeCheck();
                        float x = EditorGUILayout.FloatField("X", _points[i].x);
                        float y = EditorGUILayout.FloatField("Y", _points[i].y);
                        if (EditorGUI.EndChangeCheck())
                        {
                            MovePoint(i, new Vector2(x, y));
                            SceneView.RepaintAll();
                        }

                        if (GUILayout.Button("+", GUILayout.Width(24)))
                        {
                            InsertPoint(i + 1, _points[i] + new Vector2(0.1f, 0.0f));
                            SceneView.RepaintAll();
                            GUI.changed = true;
                            break;
                        }

                        using (new EditorGUI.DisabledScope(_points.Count <= 3))
                        {
                            if (GUILayout.Button("-", GUILayout.Width(24)))
                            {
                                RemovePoint(i);
                                SceneView.RepaintAll();
                                GUI.changed = true;
                                break;
                            }
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(_points.Count >= MaxPointCount))
                {
                    if (GUILayout.Button("添加顶点"))
                    {
                        Vector2 newPoint = _points.Count == 0
                            ? Vector2.zero
                            : _points[_points.Count - 1] + new Vector2(0.1f, 0.0f);
                        InsertPoint(_points.Count, newPoint);
                        SceneView.RepaintAll();
                    }
                }
            }
        }
#endif

        public IReadOnlyList<Vector2> Points => _points;

        [ShowInInspector]
        [LabelText("监测层级")]
        [BeanField(nameof(SetLayer), LubanType = "int", Order = 3)]
        public LayerMask layer { get; private set; } = ~0;

        public void SetIsWorldSpace(bool value) => isWorldSpace = value;

        public void SetPoints(string value)
        {
            if (TryParsePoints(value, out List<Vector2> parsedPoints))
            {
                SetPoints(parsedPoints);
            }
        }

        public void SetPoints(List<Vector2> value)
        {
            _points = value != null ? new List<Vector2>(value) : new List<Vector2>();
            if (_points.Count > MaxPointCount)
            {
                _points.RemoveRange(MaxPointCount, _points.Count - MaxPointCount);
            }

            points = EncodePoints(_points);
        }

#if UNITY_EDITOR
#endif

        public void SetLayer(int value) => layer = value;

        public void MovePoint(int index, Vector2 value)
        {
            if (index < 0 || index >= _points.Count)
            {
                return;
            }

            _points[index] = value;
            points = EncodePoints(_points);
        }

        public void InsertPoint(int index, Vector2 value)
        {
            if (_points.Count >= MaxPointCount)
            {
                return;
            }

            _points.Insert(Mathf.Clamp(index, 0, _points.Count), value);
            points = EncodePoints(_points);
        }

        public void RemovePoint(int index)
        {
            if (_points.Count <= 3 || index < 0 || index >= _points.Count)
            {
                return;
            }

            _points.RemoveAt(index);
            points = EncodePoints(_points);
        }

#if UNITY_EDITOR
        public void DecodeExcelData(List<object> paramData)
        {
            if (paramData.Count > 0 && bool.TryParse(paramData[0] as string, out bool parsedBool))
            {
                SetIsWorldSpace(parsedBool);
            }

            if (paramData.Count > 1)
            {
                SetPoints(paramData[1]?.ToString());
            }

            if (paramData.Count > 2 && int.TryParse(paramData[2] as string, out int parsedLayer))
            {
                SetLayer(parsedLayer);
            }
        }

        public List<object> EncodeExcelData()
        {
            return new List<object>
            {
                isWorldSpace.ToString(),
                points,
                layer.value.ToString()
            };
        }
#endif

        private static string EncodePoints(IReadOnlyList<Vector2> source)
        {
            if (source == null || source.Count == 0)
            {
                return string.Empty;
            }

            List<string> encoded = new(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                encoded.Add($"{source[i].x},{source[i].y}");
            }

            return string.Join(";", encoded);
        }

        private static bool TryParsePoints(string value, out List<Vector2> result)
        {
            result = new List<Vector2>();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] pointValues = value.Split(';');
            for (int i = 0; i < pointValues.Length && result.Count < MaxPointCount; i++)
            {
                if (XParamCatchAreaBox2D.TryParseVector2(pointValues[i], out Vector2 point))
                {
                    result.Add(point);
                }
            }

            return result.Count >= 3;
        }
    }
}
