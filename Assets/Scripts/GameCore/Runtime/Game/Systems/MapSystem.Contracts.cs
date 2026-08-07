using System;
using UnityEngine;
using UnityEngine.Serialization;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 地图系统的正式存档数据块。
    /// 这里只保存当前地图、检查点栈和检查点顺序状态，不承载运行时入口逻辑。
    /// </summary>
    [Serializable]
    public class MapDataBlock : DataBlock
    {
        [SerializeReference, SubclassSelector] public ICheckpoint[] checkpoints;
        [FormerlySerializedAs("currentMap"), HideInInspector]
        public string currentSceneAddress;
        [HideInInspector] public bool playtest;
        [HideInInspector] public bool hasOrderedCheckpoint;
        [HideInInspector] public int currentCheckpointOrder;
    }
}
