using System;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 地图过场仍由 TransitionSystem 真正执行，
    /// 这里的委托参数只负责把卸载、加载和完成回调作为正式合同交给过场入口。
    /// </summary>
    public class MapLoadingDelegationParams
    {
        public Action<Action> unloadDelegate;
        public Action<Action> loadDelegate;
        public Action completionDelegate;
    }

    /// <summary>
    /// 地图系统的正式存档数据块。
    /// 这里只保存当前地图、检查点栈和检查点顺序状态，不承载运行时入口逻辑。
    /// </summary>
    [Serializable]
    public class MapDataBlock : DataBlock
    {
        [SerializeReference, SubclassSelector] public ICheckpoint[] checkpoints;
        [HideInInspector] public string currentMap;
        [HideInInspector] public bool playtest;
        [HideInInspector] public bool hasOrderedCheckpoint;
        [HideInInspector] public int currentCheckpointOrder;
    }
}
