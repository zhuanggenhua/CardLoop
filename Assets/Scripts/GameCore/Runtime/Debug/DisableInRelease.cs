using UnityEngine;

namespace FantasyWord.GameCore
{
    public class DisableInRelease : MonoBehaviour
    {
        private void Awake()
        {
            if (!Debug.isDebugBuild && !Application.isEditor)
            {
                gameObject.SetActive(false);
            }
        }
    }
}

