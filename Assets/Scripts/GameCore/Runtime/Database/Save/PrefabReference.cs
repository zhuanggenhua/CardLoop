using UnityEngine;
using UnityEngine.Serialization;

namespace GameCore
{
    [CreateAssetMenu(menuName = AssetMenuIndexer.Save + nameof(PrefabReference))]
    public class PrefabReference : DatabaseEntry
    {
        [Header("References")]
        [SerializeField, FormerlySerializedAs("prefab")]
        private GameObject m_prefab;

        public GameObject prefab => m_prefab;
    }
}
