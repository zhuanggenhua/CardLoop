using System.Collections.Generic;
using UnityEngine;
using YokiFrame;

namespace GameCore
{
    /// <summary>
    /// 浮动文字的排队播放请求。队列存在的原因是同一帧大量战斗事件需要按最小间隔错开发出。
    /// </summary>
    public struct FloatingTextAnimation
    {
        public string text;
        public Vector2 position;
        public Color color;
        public string animationTrigger;
    }

    /// <summary>
    /// 战斗浮字入口。这里只负责节流和排队，实例生命周期统一交给 YokiFrame 对象池管理。
    /// </summary>
    public class FloatingTextPool : MonoBehaviour
    {
        // Inspector Settings
        [Header("References")]
        [Tooltip("浮动文字预制体，必须带有 FloatingText 组件。")]
        [SerializeField] private GameObject m_floatingTextPrefab = null;

        [Header("Settings")]
        [Tooltip("预热数量和最大同时存活数量。容量不足时新浮字会继续留在队列中等待。")]
        [SerializeField] private int m_poolSize = 3;
        [Tooltip("两条浮动文字之间的最小播放间隔，用于避免同一帧伤害数字堆叠。")]
        [SerializeField] private float m_minimumDelayBetweenTexts = 0.15f;

        // Private Members
        private float m_cooldown = 0.0f;
        private float m_poolExhaustedWarningCooldown = 0.0f;
        private readonly Queue<FloatingTextAnimation> m_queue = new();

        private void Awake()
        {
            ConfigureFloatingTextPool();
        }

        private void Update()
        {
            if (m_queue.Count > 0 && m_cooldown <= 0.0f)
            {
                FloatingTextAnimation animation = m_queue.Peek();

                FloatingText floatingText = RentFloatingText();

                if (floatingText)
                {
                    floatingText.Play(animation.text, animation.position, animation.color, animation.animationTrigger);
                    m_queue.Dequeue();
                    m_cooldown = m_minimumDelayBetweenTexts;
                }
                else
                {
                    if (m_poolExhaustedWarningCooldown <= 0.0f)
                    {
                        Debug.LogWarning(
                            "No floating text available. The queued text will wait for a pooled instance.");
                        m_poolExhaustedWarningCooldown = 1.0f;
                    }
                }
            }

            m_cooldown = Mathf.Max(0.0f, m_cooldown - Time.deltaTime);
            m_poolExhaustedWarningCooldown = Mathf.Max(
                0.0f,
                m_poolExhaustedWarningCooldown - Time.deltaTime);
        }

        private FloatingText RentFloatingText()
        {
            if (m_floatingTextPrefab == null)
            {
                return null;
            }

            GameObject instance = GameObjectPoolService.Rent(m_floatingTextPrefab, transform);
            if (instance == null)
            {
                return null;
            }

            if (instance.TryGetComponent(out FloatingText floatingText))
            {
                return floatingText;
            }

            Debug.LogError("FloatingText Prefab invalid. Make sure the prefab has a FloatingText component", instance);
            GameObjectPoolService.Return(instance);
            return null;
        }

        private void ConfigureFloatingTextPool()
        {
            if (m_floatingTextPrefab == null)
            {
                return;
            }

            int capacity = Mathf.Max(0, m_poolSize);
            GameObjectPoolService.SetMaxCapacity(m_floatingTextPrefab, capacity);
            GameObjectPoolService.Prewarm(m_floatingTextPrefab, capacity);
        }

        public void ShowText(string text, Vector2 position, Color color, string animationTrigger)
        {
            m_queue.Enqueue(new()
            {
                text = text,
                position = position,
                color = color,
                animationTrigger = animationTrigger
            });
        }
    }
}

