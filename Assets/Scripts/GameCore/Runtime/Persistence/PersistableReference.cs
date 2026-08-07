using System;
using UnityEngine;

namespace GameCore
{
    [Serializable]
    public struct PersistableReference<T> where T : Persistable
    {
        public string identifier => m_identifier;

        [SerializeField] private string m_identifier;

        public PersistableReference(T instance) => this = instance;

        public bool TryResolve(out T persistable)
        {
            persistable = null;

            if (string.IsNullOrEmpty(m_identifier))
            {
                return false;
            }

            if (!GameManager.Exists() || !GameManager.TryGetSystem<PersistenceSystem>(out PersistenceSystem persistenceSystem))
            {
                return false;
            }

            return persistenceSystem.TryResolvePersistable(m_identifier, out persistable);
        }

        public T ResolveOrNull()
        {
            return TryResolve(out T persistable) ? persistable : null;
        }

        public static implicit operator PersistableReference<T>(T instance) => new()
        {
            m_identifier = instance?.GetPersistentIdentifier()
        };
    }
}
