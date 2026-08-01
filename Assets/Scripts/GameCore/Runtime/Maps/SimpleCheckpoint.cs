using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public struct SimpleCheckpoint : ICheckpoint
    {
        [MapSelector] public string map;
        public Vector3 position;

        string ICheckpoint.map => map;
        Vector3 ICheckpoint.position => position;
        public bool IsValid() => true;
        public void UpdateMapName() => map = CheckpointUtil.GetActualMapName(map);
    }
}

