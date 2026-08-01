using UnityEngine;
using UnityEngine.Serialization;

namespace FantasyWord.GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.FantasyWord_Save + nameof(PrefabReference))]
    public class PrefabReference : DatabaseEntry
    {
        [Header("References")]
        [SerializeField, FormerlySerializedAs("prefab")]
        private GameObject m_prefab;

        public GameObject prefab => m_prefab;
    }
}
