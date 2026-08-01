using System.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 一次 Transform 抖动协程的句柄，保留协程 owner、目标和初始位置以便中断时复位。
    /// </summary>
    public readonly struct ShakeHandler
    {
        private readonly MonoBehaviour m_owner;
        private readonly Transform m_target;
        private readonly Vector3 m_initialPosition;
        private readonly Coroutine m_coroutine;

        internal ShakeHandler(MonoBehaviour owner, Transform target, Vector3 initialPosition, Coroutine coroutine)
        {
            m_owner = owner;
            m_target = target;
            m_initialPosition = initialPosition;
            m_coroutine = coroutine;
        }

        internal MonoBehaviour Owner => m_owner;
        internal Transform Target => m_target;
        internal Vector3 InitialPosition => m_initialPosition;
        internal Coroutine Coroutine => m_coroutine;
    }

    /// <summary>
    /// Transform 局部位置抖动工具。协程 owner 必须由调用方显式传入，避免把表现协程挂到全局 GameManager。
    /// </summary>
    public static class TransformShaker
    {
        /// <summary>
        /// 启动一次局部位置抖动，并返回可用于中断和复位的句柄。
        /// </summary>
        public static ShakeHandler Shake(MonoBehaviour owner, Transform target, float amplitude, float2 frequency, float duration)
        {
            if (owner == null || target == null)
            {
                return default;
            }

            return new ShakeHandler(
                owner,
                target,
                target.localPosition,
                owner.StartCoroutine(
                    ShakeCoroutine(target, amplitude, frequency, duration)
                ));
        }

        /// <summary>
        /// 如果句柄仍有协程在运行，则停止抖动并把目标恢复到启动时的局部位置。
        /// </summary>
        public static bool InterruptShakeIfInProgress(ShakeHandler handler)
        {
            if (handler.Owner != null && handler.Coroutine != null)
            {
                handler.Owner.StopCoroutine(handler.Coroutine);

                if (handler.Target)
                {
                    handler.Target.localPosition = handler.InitialPosition;
                }

                return true;
            }

            return false;
        }

        private static IEnumerator ShakeCoroutine(Transform target, float amplitude, float2 frequency, float duration)
        {
            if (target == null)
            {
                yield break;
            }

            float elapsedTime = 0f;
            Vector3 initialPosition = target.localPosition;

            while (target != null && elapsedTime < duration)
            {
                float2 offset = math.sin(frequency * elapsedTime) * amplitude;
                target.localPosition = new(initialPosition.x + offset.x, initialPosition.y + offset.y, initialPosition.z);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (target != null)
            {
                target.localPosition = initialPosition;
            }
        }
    }
}
