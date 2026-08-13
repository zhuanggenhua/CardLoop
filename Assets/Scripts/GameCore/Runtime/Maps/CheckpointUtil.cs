using UnityEngine;

namespace GameCore
{
    public static class CheckpointUtil
    {
        public static string GetActualSceneAddress(string sceneAddress)
        {
            if (string.IsNullOrEmpty(sceneAddress))
            {
                return GameManager.SceneSystem.CurrentSceneAddress;
            }

            return sceneAddress;
        }
    }
}
