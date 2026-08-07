using azixMcAze.SerializableDictionary;
using MackySoft.SerializeReferenceExtensions;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    public partial class GameConfig
    {
        [Header("Playtest Settings")]
        [SerializeField, FormerlySerializedAs("playtestSaveFile")]
        private SaveFile m_playtestSaveFile = null;

        [SerializeReference, SubclassSelector, FormerlySerializedAs("toExecuteOnPlayerDeath")]
        private ICommand m_toExecuteOnPlayerDeath = null;

        [Header("Save Settings")]
        [FormerlySerializedAs("persistentIdentifierMappings")]
        [SerializeField]
        private SerializableDictionary<string, string> m_persistentIdentifierMappings = new();

        public bool hasPlayerDeathAction => m_toExecuteOnPlayerDeath != null;

        /// <summary>
        /// 玩家死亡后的收口动作由 GameConfig 自己拥有并执行，外部不再直接拿到底层命令对象。
        /// </summary>
        public void ExecutePlayerDeathAction(GameCommandContext context)
        {
            m_toExecuteOnPlayerDeath.ExecuteFireAndReport(context, nameof(GameConfig), this);
        }

        /// <summary>
        /// PlayMode 覆盖只需要一份可加载的数据快照，不应直接拿到底层 SaveFile 资产本体。
        /// </summary>
        public SaveDataBlock CreatePlaytestSaveDataSnapshot()
        {
            return m_playtestSaveFile != null
                ? m_playtestSaveFile.CreateContentSnapshot()
                : new SaveDataBlock();
        }

        public bool TryGetPersistentIdentifierMapping(string identifier, out string actualIdentifier)
        {
            actualIdentifier = identifier;
            return !string.IsNullOrEmpty(identifier)
                && m_persistentIdentifierMappings != null
                && m_persistentIdentifierMappings.TryGetValue(identifier, out actualIdentifier);
        }

        public string GetActualPersistentIdentifier(string identifier)
        {
            return TryGetPersistentIdentifierMapping(identifier, out string actualIdentifier)
                ? actualIdentifier
                : identifier;
        }
    }
}
