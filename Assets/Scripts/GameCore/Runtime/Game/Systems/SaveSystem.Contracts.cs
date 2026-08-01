using System;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 正式世界存档数据块。
    /// 这里只定义地图、任务、背包、玩家与持久化对象的聚合形状，不承载文件层或系统入口逻辑。
    /// </summary>
    [Serializable]
    public class SaveDataBlock : DataBlock
    {
        public string header;
        public MapDataBlock map;
        public JournalDataBlock journal;
        public InventoryDataBlock inventory;
        public GameFlagsDataBlock gameFlags;
        public PlayerDataBlock player;
        public PersistenceDataBlock persistence;
    }
}
