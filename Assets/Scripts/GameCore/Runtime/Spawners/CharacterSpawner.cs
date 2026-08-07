using UnityEngine;

namespace GameCore
{
    public class CharacterSpawner : ACharacterSpawner
    {
        [Header("Spawner Location Settings")]
        [SerializeField] private Vector2 m_offset = Vector2.zero;

        protected override Vector2 FindSpawnLocation() => new Vector2(transform.position.x, transform.position.y) + m_offset;
    }
}

