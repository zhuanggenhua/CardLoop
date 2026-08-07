namespace GameCore
{
    public interface IDataBlockHandler<DataBlockType> where DataBlockType : DataBlock
    {
        public DataBlockType CreateDataBlock();
        public void LoadDataBlock(DataBlockType block);
    }
}

