using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public struct PersistableCheckpoint : ICheckpoint
    {
        [MapSelector] public string map;
        public PersistableReference<Checkpoint> instance;

        public Vector3 position => instance.TryResolve(out Checkpoint checkpoint) ? checkpoint.transform.position : Vector3.zero;
        string ICheckpoint.map => map;
        public bool IsValid() => !string.IsNullOrEmpty(instance.identifier);
        public void UpdateMapName() => map = CheckpointUtil.GetActualMapName(map);
    }
}

