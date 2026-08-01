using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    public static class GameObjectPoolService
    {
        private const string RootName = "[YokiFrame GameObject Pools]";

        private static readonly Dictionary<GameObject, PoolBucket> Pools = new();
        private static Transform sRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Pools.Clear();
            sRoot = null;
        }

        public static GameObject Rent(GameObject prefab, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            return GetOrCreateBucket(prefab).Rent(parent);
        }

        public static GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var instance = Rent(prefab, parent);
            if (instance == null)
            {
                return null;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0)
            {
                return;
            }

            GetOrCreateBucket(prefab).Prewarm(count);
        }

        public static void SetMaxCapacity(GameObject prefab, int maxCapacity)
        {
            if (prefab == null)
            {
                return;
            }

            GetOrCreateBucket(prefab).SetMaxCapacity(maxCapacity);
        }

        public static bool Return(GameObject instance)
        {
            if (instance == null)
            {
                return false;
            }

            if (!instance.TryGetComponent(out PooledGameObject pooled) || pooled.Prefab == null)
            {
                DestroyObject(instance);
                return false;
            }

            if (pooled.InPool || !Pools.TryGetValue(pooled.Prefab, out var bucket))
            {
                return false;
            }

            return bucket.Return(instance, pooled);
        }

        public static void Clear(GameObject prefab)
        {
            if (prefab == null || !Pools.Remove(prefab, out var bucket))
            {
                return;
            }

            bucket.Dispose();
        }

        public static void ClearAll()
        {
            foreach (var bucket in Pools.Values)
            {
                bucket.Dispose();
            }

            Pools.Clear();
        }

        private static PoolBucket GetOrCreateBucket(GameObject prefab)
        {
            if (Pools.TryGetValue(prefab, out var bucket))
            {
                return bucket;
            }

            bucket = new PoolBucket(prefab, EnsureRoot());
            Pools.Add(prefab, bucket);
            return bucket;
        }

        private static Transform EnsureRoot()
        {
            if (sRoot != null)
            {
                return sRoot;
            }

            var root = new GameObject(RootName);
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(root);
            }

            sRoot = root.transform;
            return sRoot;
        }

        private static void DestroyObject(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        private sealed class PoolBucket
        {
            private readonly GameObject mPrefab;
            private readonly Transform mRoot;
            private readonly Queue<GameObject> mInactive = new();
            private readonly HashSet<GameObject> mAllInstances = new();
            private int mMaxCapacity = -1;

            public PoolBucket(GameObject prefab, Transform root)
            {
                mPrefab = prefab;

                var bucketRoot = new GameObject($"prefab:{prefab.name}");
                bucketRoot.transform.SetParent(root, false);
                mRoot = bucketRoot.transform;
            }

            public GameObject Rent(Transform parent)
            {
                GameObject instance = null;
                while (mInactive.Count > 0 && instance == null)
                {
                    instance = mInactive.Dequeue();
                }

                if (instance == null)
                {
                    if (!CanCreateInstance)
                    {
                        return null;
                    }

                    instance = CreateInstance();
                }

                instance.transform.SetParent(parent, false);
                instance.SetActive(true);
                instance.GetComponent<PooledGameObject>().MarkRented();
                return instance;
            }

            public bool Return(GameObject instance, PooledGameObject pooled)
            {
                if (mMaxCapacity >= 0 && mInactive.Count >= mMaxCapacity)
                {
                    mAllInstances.Remove(instance);
                    DestroyObject(instance);
                    return false;
                }

                pooled.MarkReturned();
                instance.SetActive(false);
                instance.transform.SetParent(mRoot, false);
                mInactive.Enqueue(instance);
                return true;
            }

            public void Prewarm(int count)
            {
                var targetCount = mMaxCapacity < 0 ? count : Mathf.Min(count, mMaxCapacity);
                while (LiveCount < targetCount)
                {
                    var instance = CreateInstance();
                    instance.SetActive(false);
                    instance.transform.SetParent(mRoot, false);
                    instance.GetComponent<PooledGameObject>().MarkReturned();
                    mInactive.Enqueue(instance);
                }
            }

            public void SetMaxCapacity(int maxCapacity)
            {
                mMaxCapacity = maxCapacity < 0 ? -1 : maxCapacity;

                while (mMaxCapacity >= 0 && mInactive.Count > mMaxCapacity)
                {
                    var instance = mInactive.Dequeue();
                    mAllInstances.Remove(instance);
                    DestroyObject(instance);
                }
            }

            public void Dispose()
            {
                mInactive.Clear();

                foreach (var instance in mAllInstances)
                {
                    if (instance != null)
                    {
                        DestroyObject(instance);
                    }
                }

                mAllInstances.Clear();

                if (mRoot != null)
                {
                    DestroyObject(mRoot.gameObject);
                }
            }

            private bool CanCreateInstance => mMaxCapacity < 0 || LiveCount < mMaxCapacity;

            private int LiveCount
            {
                get
                {
                    var total = 0;
                    foreach (var instance in mAllInstances)
                    {
                        if (instance != null)
                        {
                            total++;
                        }
                    }

                    return total;
                }
            }

            private GameObject CreateInstance()
            {
                var instance = Object.Instantiate(mPrefab);
                instance.name = mPrefab.name;
                mAllInstances.Add(instance);

                if (!instance.TryGetComponent(out PooledGameObject pooled))
                {
                    pooled = instance.AddComponent<PooledGameObject>();
                }

                pooled.Initialize(mPrefab);
                return instance;
            }
        }
    }
}
