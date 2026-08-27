using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameCore
{
    public static class UIPointerUtility
    {
        private const string DefaultIgnoredObjectName = "Mask";

        private static readonly List<RaycastResult> sRaycastResults = new(16);
        private static PointerEventData sPointerEventData;

        public static bool IsPositionOverUI(Vector2 screenPosition, string ignoredObjectName = DefaultIgnoredObjectName)
        {
            bool result = false;
            if (TryRaycast(screenPosition, out _, ignoredObjectName))
            {
                for (int i = 0; i < sRaycastResults.Count; i++)
                {
                    if (IsBlockingUIRaycastResult(sRaycastResults[i], ignoredObjectName))
                    {
                        result = true;
                        break;
                    }
                }
            }

            sRaycastResults.Clear();
            return result;
        }

        public static bool TrySelectPointerSelectable(Vector2 screenPosition)
        {
            if (!TryRaycast(screenPosition, out var eventSystem, ignoredObjectName: null))
            {
                return false;
            }

            foreach (var result in sRaycastResults)
            {
                if (!IsBlockingUIRaycastResult(result, ignoredObjectName: null))
                {
                    continue;
                }

                var selectable = result.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.isActiveAndEnabled && (!selectable.targetGraphic || selectable.targetGraphic.raycastTarget))
                {
                    if (selectable.gameObject != eventSystem.currentSelectedGameObject)
                    {
                        eventSystem.SetSelectedGameObject(selectable.gameObject);
                    }

                    sRaycastResults.Clear();
                    return true;
                }

                var graphic = result.gameObject.GetComponent<Graphic>();
                if (graphic != null && graphic.raycastTarget)
                {
                    break;
                }
            }

            sRaycastResults.Clear();
            return false;
        }

        private static bool IsBlockingUIRaycastResult(RaycastResult result, string ignoredObjectName)
        {
            if (result.module is not GraphicRaycaster)
            {
                return false;
            }

            GameObject hitObject = result.gameObject;
            if (hitObject == null || hitObject.name == ignoredObjectName)
            {
                return false;
            }

            // 有些表现层 UI 只负责显示，例如过场黑幕和受击闪屏。它们可能保留 Graphic.raycastTarget，
            // 但父级 CanvasGroup 已明确 blocksRaycasts=false，点地移动不能被这类显示层误判为点到 UI。
            CanvasGroup[] canvasGroups = hitObject.GetComponentsInParent<CanvasGroup>(includeInactive: false);
            for (int i = 0; i < canvasGroups.Length; i++)
            {
                if (!canvasGroups[i].blocksRaycasts)
                {
                    return false;
                }

                if (canvasGroups[i].ignoreParentGroups)
                {
                    break;
                }
            }

            Graphic graphic = hitObject.GetComponent<Graphic>();
            return graphic == null || graphic.raycastTarget;
        }

        private static bool TryRaycast(Vector2 screenPosition, out EventSystem eventSystem, string ignoredObjectName)
        {
            // 指针射线只认 GameManager 暴露的正式输入入口，避免项目侧各处自行直连 Unity 当前选中节点。
            eventSystem = GameManager.EventSystem;
            if (eventSystem == null)
            {
                return false;
            }

            sPointerEventData ??= new PointerEventData(eventSystem);
            sPointerEventData.Reset();
            sPointerEventData.position = screenPosition;

            sRaycastResults.Clear();
            eventSystem.RaycastAll(sPointerEventData, sRaycastResults);

            return sRaycastResults.Count > 0;
        }
    }
}
