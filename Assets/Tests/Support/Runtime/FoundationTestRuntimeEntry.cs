using System;
using GameCore;
using UnityEngine;

namespace Gameplay.Tests.Support
{
    /// <summary>
    /// 允许统一地基场景既作为剧本内容被加载，也能被测试单独打开。
    /// 进程根的系统配置只存在于同一预制体；已有进程根时不会创建第二个实例。
    /// </summary>
    [DefaultExecutionOrder(-9999)]
    [DisallowMultipleComponent]
    public sealed class FoundationTestRuntimeEntry : MonoBehaviour
    {
        [SerializeField, Tooltip("仅在单独打开测试场景时实例化的唯一测试进程根预制体。")]
        private GameObject m_runtimeRootPrefab;

        private void Awake()
        {
            if (GameManager.Exists())
            {
                return;
            }
            if (m_runtimeRootPrefab == null)
            {
                throw new InvalidOperationException("地基测试场景没有配置测试进程根预制体。");
            }

            GameObject runtimeRoot = Instantiate(m_runtimeRootPrefab);
            if (!runtimeRoot.TryGetComponent(out GameManager _))
            {
                throw new InvalidOperationException("地基测试进程根预制体缺少 GameManager 组件。");
            }
        }
    }
}
