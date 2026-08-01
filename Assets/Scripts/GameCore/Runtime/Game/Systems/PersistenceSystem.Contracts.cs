using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 持久化对象系统的数据块形状。
    /// 这里只定义预实例与运行时实例对象的聚合容器，不承载系统逻辑或实例化流程。
    /// </summary>
    [Serializable]
    public class PersistenceDataBlock : DataBlock
    {
        [SerializeReference] public PersistableDataBlock[] objects;
    }
}
