using UnityEngine;

namespace GameCore
{
    [RequireComponent(typeof(Collider2D))]
    public class CharacterAreaSpawner : ACharacterSpawner
    {
        private Collider2D m_collider = null;

        private void Start()
        {
            m_collider = GetComponent<Collider2D>();
        }

        protected override Vector2 FindSpawnLocation()
        {
            while (true)
            {
                Vector2 point = new()
                {
                    x = Random.Range(m_collider.bounds.min.x, m_collider.bounds.max.x),
                    y = Random.Range(m_collider.bounds.min.y, m_collider.bounds.max.y)
                };

                if (m_collider.OverlapPoint(point))
                {
                    return point;
                }
            }
        }
    }
}

