using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    public enum EPersistableObjectState
    {
        Active,
        Inactive,
        Destroyed
    }

    /// <summary>
    /// Persistable 的最小存档块，只保存持久化身份和对象当前状态。
    /// </summary>
    [Serializable]
    public class PersistableDataBlock : DataBlock
    {
        [SerializeReference, HideInInspector] public APersistenceInfo info = null;
        public EPersistableObjectState state;
    }

    /// <summary>
    /// 持久化对象销毁时交给持久化系统的最小快照。
    /// 它只保留持久化标识和销毁后的数据块，不再把 live Persistable 实例广播出去。
    /// </summary>
    public readonly struct PersistableDestructionSnapshot
    {
        public PersistableDestructionSnapshot(
            PersistableDataBlock dataBlock,
            string identifier,
            EPersistableOwnershipKind ownershipKind,
            bool automaticallyPersisted)
        {
            DataBlock = dataBlock;
            Identifier = identifier;
            OwnershipKind = ownershipKind;
            AutomaticallyPersisted = automaticallyPersisted;
        }

        public PersistableDataBlock DataBlock { get; }

        public string Identifier { get; }

        public EPersistableOwnershipKind OwnershipKind { get; }

        public bool AutomaticallyPersisted { get; }

        public bool IsPreInstanced => OwnershipKind == EPersistableOwnershipKind.PreInstanced;

        public bool IsRuntimeInstanced => OwnershipKind == EPersistableOwnershipKind.RuntimeInstanced;
    }
}
