using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    [Serializable]
    public struct PersistableCheckpoint : ICheckpoint
    {
        [FormerlySerializedAs("map"), MapSelector]
        public string sceneAddress;
        public PersistableReference<Checkpoint> instance;

        public Vector3 position => instance.TryResolve(out Checkpoint checkpoint) ? checkpoint.transform.position : Vector3.zero;
        string ICheckpoint.sceneAddress => sceneAddress;
        public bool IsValid() => !string.IsNullOrEmpty(instance.identifier);
        public void UpdateSceneAddress() =>
            sceneAddress = CheckpointUtil.GetActualSceneAddress(sceneAddress);
    }
}

