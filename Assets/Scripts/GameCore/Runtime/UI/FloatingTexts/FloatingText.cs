using TMPro;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 单条浮动文字。实例由 <see cref="GameObjectPoolService"/> 出租时，动画结束必须归还到统一对象池。
    /// </summary>
    public class FloatingText : MonoBehaviour, IFloatingTextAnimationStateReceiver
    {
        // Component References
        private TextMeshProUGUI m_textMesh = null;
        private Animator m_animator = null;

        private void Awake()
        {
            m_textMesh = GetComponentInChildren<TextMeshProUGUI>();
            m_animator = GetComponentInChildren<Animator>();
        }

        /// <summary>
        /// 浮字动画结束后的正式入口。
        /// 当前由 StateMessageDispatcher 通过 <see cref="IFloatingTextAnimationStateReceiver"/> 正式调用；
        /// 若接不到这里，就应视为动画接线错误。
        /// </summary>
        public void OnFloatingTextAnimationEnd()
        {
            Stop();
        }

        public void Play(string text, Vector2 position, Color color, string animationTrigger)
        {
            gameObject.SetActive(true);
            transform.position = position;
            m_textMesh.color = color;
            m_textMesh.text = text;
            m_animator.SetTrigger(animationTrigger);
        }

        public void Stop()
        {
            if (TryGetComponent(out PooledGameObject _))
            {
                // YokiFrame 会在归还失败时自行销毁异常实例，这里不要再访问对象状态。
                GameObjectPoolService.Return(gameObject);
                return;
            }

            gameObject.SetActive(false);
        }
    }
}

