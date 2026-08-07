using UnityEngine;

namespace GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.Save + nameof(SaveFile))]
    public class SaveFile : DatabaseEntry
    {
        [SerializeField] private SaveDataBlock m_content;

        /// <summary>
        /// 默认存档资产是编辑期模板；运行时只能拿深拷贝快照，不能直接改模板本体。
        /// </summary>
        public SaveDataBlock CreateContentSnapshot()
        {
            string contentJson = JsonUtility.ToJson(m_content, true);
            return JsonUtility.FromJson<SaveDataBlock>(contentJson);
        }
    }
}

