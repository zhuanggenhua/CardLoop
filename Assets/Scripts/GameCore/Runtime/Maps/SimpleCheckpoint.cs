using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    [Serializable]
    public struct SimpleCheckpoint : ICheckpoint
    {
        [FormerlySerializedAs("map"), SceneAddressSelector]
        public string sceneAddress;
        public Vector3 position;

        string ICheckpoint.sceneAddress => sceneAddress;
        Vector3 ICheckpoint.position => position;
        public bool IsValid() => true;
        public void UpdateSceneAddress() =>
            sceneAddress = CheckpointUtil.GetActualSceneAddress(sceneAddress);
    }
}

