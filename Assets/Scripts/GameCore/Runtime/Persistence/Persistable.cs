using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    public class Persistable : MonoBehaviour, IDataBlockHandler<PersistableDataBlock>
    {
        private static readonly HashSet<Persistable> s_loadedPersistables = new();

        [SerializeField] private bool m_autoPersistWhenPreInstanced = false;
        [SerializeField] private bool m_disablePermanentDestroy = false;
        [SerializeReference, SubclassSelector] private ICommand m_executeOnDeath = null;
        [SerializeReference, HideInInspector] private APersistenceInfo m_persistenceInfo = null;

        private bool m_destroyed = false;
        private UnityEvent m_destroyedEvent = new();
        [SerializeField, Tooltip("启用后，此场景实例不读取或写入持久化数据。")]
        private bool m_forceNoPersistence = false;

        internal bool isMarkedAsDestroyed => m_destroyed;

        public void AddDestroyedListener(UnityAction listener)
        {
            m_destroyedEvent.AddListener(listener);
        }

        public void RemoveDestroyedListener(UnityAction listener)
        {
            m_destroyedEvent.RemoveListener(listener);
        }

        public void DisablePersistence()
        {
            m_forceNoPersistence = true;
            TryUnregisterWithPersistenceSystem();
        }

        protected bool IsMarkedAsDestroyed()
        {
            return m_destroyed;
        }

        protected void MarkAsDestroyed()
        {
            m_destroyed = true;
        }

        protected void MarkAsNotDestroyed()
        {
            m_destroyed = false;
        }

        public virtual void Destroy()
        {
            Destroy(GameCommandContext.Script());
        }

        public virtual void Destroy(GameCommandContext context)
        {
            MarkAsDestroyed();
            NotifyPersistenceSystemAboutDestruction();
            m_destroyedEvent.Invoke();
            m_executeOnDeath.ExecuteFireAndReport(context, nameof(Persistable), this);
            Destroy(gameObject);
        }

        private bool IsPersistent()
        {
            return
                !m_forceNoPersistence &&
                m_persistenceInfo != null &&
                m_persistenceInfo.IsValid();
        }

        public bool IsAutomaticallyPersisted()
        {
            return
                IsPreInstanced() && m_autoPersistWhenPreInstanced ||
                IsRuntimeInstanced();
        }

        public bool IsPreInstanced()
        {
            return
                IsPersistent() &&
                m_persistenceInfo is PreInstancedPersistentDataHandler;
        }

        public bool IsRuntimeInstanced()
        {
            return
                IsPersistent() &&
                m_persistenceInfo is RuntimeInstancedPersistentDataHandler;
        }

        /// <summary>
        /// 返回当前持久化对象的正式标识符；运行时调用方不再直接拿到底层 persistence handler。
        /// </summary>
        public string GetPersistentIdentifier()
        {
            return m_persistenceInfo is IIdentifiablePersistentDataHandler handler ? handler.GetIdentifier() : null;
        }

#if UNITY_EDITOR
        public APersistenceInfo EditorPersistenceInfo
        {
            get => m_persistenceInfo;
            set => m_persistenceInfo = value;
        }

        public bool HasEditorPersistenceInfo() => m_persistenceInfo != null;
#endif

        public void MakeRuntimeInstanced(PrefabReference instance, string identifier)
        {
            if (!GameManager.Database.TryCreateReference(instance, out DatabaseEntryReference<PrefabReference> prefabReference))
            {
                throw new InvalidOperationException(
                    $"[{nameof(Persistable)}] 运行时持久化 PrefabReference 必须先登记到 DatabaseRegistry：{(instance ? instance.name : "<null>")}");
            }

            m_persistenceInfo = new RuntimeInstancedPersistentDataHandler
            {
                prefab = prefabReference,
                map = GameManager.MapSystem.GetCurrentMapName(),
                identifier = identifier
            };

            TryRegisterWithPersistenceSystem();
        }

        public void MakeCustomInstanced(string identifier)
        {
            m_persistenceInfo = new CustomInstancedPersistentDataHandler
            {
                identifier = identifier
            };

            TryRegisterWithPersistenceSystem();
        }

        public PersistableDataBlock CreateDataBlock()
        {
            Debug.Assert(m_persistenceInfo != null && m_persistenceInfo.IsValid(), "Cannot save data block with missing persistence info");

            PersistableDataBlock block = (PersistableDataBlock)Activator.CreateInstance(GetDataBlockType());

            block.info = m_persistenceInfo;
            block.state =
                m_destroyed ?
                EPersistableObjectState.Destroyed :
                    gameObject.activeInHierarchy ?
                    EPersistableObjectState.Active :
                    EPersistableObjectState.Inactive;

            OnSave(block);
            return block;
        }

        public void LoadDataBlock(PersistableDataBlock block)
        {
            if (!ApplyPersistableState(block.state))
            {
                return;
            }

            OnLoad(block);
        }

        private void NotifyPersistenceSystemAboutDestruction()
        {
            PersistableDataBlock dataBlock = IsPersistent() ? CreateDataBlock() : null;
            GameManager.PersistenceSystem.NotifyPersistableDestroyed(new PersistableDestructionSnapshot(
                dataBlock,
                GetPersistentIdentifier(),
                GetOwnershipKind(),
                IsAutomaticallyPersisted()));
        }

        private EPersistableOwnershipKind GetOwnershipKind()
        {
            if (m_persistenceInfo is RuntimeInstancedPersistentDataHandler)
            {
                return EPersistableOwnershipKind.RuntimeInstanced;
            }

            if (m_persistenceInfo is PreInstancedPersistentDataHandler)
            {
                return EPersistableOwnershipKind.PreInstanced;
            }

            if (m_persistenceInfo is CustomInstancedPersistentDataHandler)
            {
                return EPersistableOwnershipKind.CustomInstanced;
            }

            return EPersistableOwnershipKind.None;
        }

        protected EPersistableObjectState CapturePersistableState()
        {
            return
                m_destroyed ?
                EPersistableObjectState.Destroyed :
                    gameObject.activeInHierarchy ?
                    EPersistableObjectState.Active :
                    EPersistableObjectState.Inactive;
        }

        protected bool ApplyPersistableState(EPersistableObjectState state)
        {
            switch (state)
            {
                case EPersistableObjectState.Active:
                    gameObject.SetActive(true);
                    return true;
                case EPersistableObjectState.Inactive:
                    gameObject.SetActive(false);
                    return true;
                case EPersistableObjectState.Destroyed:
                    if (!m_disablePermanentDestroy)
                    {
                        Destroy(gameObject);
                    }

                    return false;
                default:
                    return true;
            }
        }

        protected virtual Type GetDataBlockType() => typeof(PersistableDataBlock);
        protected virtual void OnSave(PersistableDataBlock block) { }
        protected virtual void OnLoad(PersistableDataBlock block) { }

        protected virtual void Awake()
        {
            RegisterLoadedPersistable();
        }

        protected virtual void OnDestroy()
        {
            UnregisterLoadedPersistable();
        }

        internal static Persistable[] CreateLoadedPersistablesSnapshot()
        {
            List<Persistable> snapshot = new();
            List<Persistable> missingPersistables = null;

            foreach (Persistable persistable in s_loadedPersistables)
            {
                if (persistable == null)
                {
                    missingPersistables ??= new List<Persistable>();
                    missingPersistables.Add(persistable);
                    continue;
                }

                snapshot.Add(persistable);
            }

            if (missingPersistables != null)
            {
                foreach (Persistable persistable in missingPersistables)
                {
                    s_loadedPersistables.Remove(persistable);
                }
            }

            return snapshot.ToArray();
        }

        internal bool HasValidPersistenceInfo() => IsPersistent();

        private void RegisterLoadedPersistable()
        {
            s_loadedPersistables.Add(this);
            TryRegisterWithPersistenceSystem();
        }

        private void UnregisterLoadedPersistable()
        {
            s_loadedPersistables.Remove(this);
            TryUnregisterWithPersistenceSystem();
        }

        private void TryRegisterWithPersistenceSystem()
        {
            if (GameManager.Exists() &&
                GameManager.TryGetSystem(out PersistenceSystem persistenceSystem))
            {
                persistenceSystem.RegisterPersistable(this);
            }
        }

        private void TryUnregisterWithPersistenceSystem()
        {
            if (GameManager.Exists() &&
                GameManager.TryGetSystem(out PersistenceSystem persistenceSystem))
            {
                persistenceSystem.UnregisterPersistable(this);
            }
        }
    }
}



