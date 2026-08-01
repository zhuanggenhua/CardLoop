using UnityEngine;

namespace YokiFrame
{
    public sealed class PooledGameObject : MonoBehaviour
    {
        internal GameObject Prefab { get; private set; }
        internal bool InPool { get; private set; }

        internal void Initialize(GameObject prefab)
        {
            Prefab = prefab;
        }

        internal void MarkRented()
        {
            InPool = false;
        }

        internal void MarkReturned()
        {
            InPool = true;
        }

        public bool ReturnToPool()
        {
            return GameObjectPoolService.Return(gameObject);
        }
    }
}
