using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    public static class CoroutineKit
    {
        private static readonly WaitForEndOfFrame sWaitForEndOfFrame = new();
        private static readonly WaitForFixedUpdate sWaitForFixedUpdate = new();
        private static readonly Dictionary<float, WaitForSeconds> sWaitForSeconds = new();

        public static WaitForEndOfFrame WaitForEndOfFrame => sWaitForEndOfFrame;

        public static WaitForFixedUpdate WaitForFixedUpdate => sWaitForFixedUpdate;

        public static WaitForSeconds WaitForSeconds(float seconds)
        {
            if (seconds <= 0f)
            {
                seconds = 0f;
            }

            if (!sWaitForSeconds.TryGetValue(seconds, out var wait))
            {
                wait = new WaitForSeconds(seconds);
                sWaitForSeconds.Add(seconds, wait);
            }

            return wait;
        }

        public static IEnumerator WaitForSecondsRealtime(float seconds)
        {
            if (seconds <= 0f)
            {
                yield break;
            }

            var elapsedTime = 0f;
            while (elapsedTime < seconds)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        public static IEnumerator WaitForFrames(int frames = 1)
        {
            for (var i = 0; i < frames; i++)
            {
                yield return null;
            }
        }
    }
}
