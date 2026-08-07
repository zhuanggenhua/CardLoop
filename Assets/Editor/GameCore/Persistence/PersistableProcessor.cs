using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameCore
{
    // 在编辑期为场景内 Persistable 自动补唯一标识，避免保存系统遇到无标识对象。
    [InitializeOnLoad]
    static class PersistableProcessor
    {
        private static HashSet<int> m_persistables = new();

        static PersistableProcessor()
        {
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        }

        private static bool CanProcessEditModePersistables()
        {
            return !Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static IEnumerable<Persistable> FindPersistables() =>
            Object.FindObjectsByType<Persistable>(FindObjectsSortMode.InstanceID)
                .Where(IsEligibleEditModePersistable);

        private static bool IsEligibleEditModePersistable(Persistable persistable)
        {
            if (persistable == null)
            {
                return false;
            }

            if (EditorUtility.IsPersistent(persistable))
            {
                return false;
            }

            if (HasDontSaveFlag(persistable.hideFlags) || HasDontSaveFlag(persistable.gameObject.hideFlags))
            {
                return false;
            }

            Scene scene = persistable.gameObject.scene;
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool HasDontSaveFlag(HideFlags flags)
        {
            return (flags & HideFlags.DontSave) != 0 || (flags & HideFlags.DontSaveInEditor) != 0;
        }

        private static void CacheInitialPersistables()
        {
            m_persistables = FindPersistables().Select(GetObjectId).ToHashSet();
        }

        private static void FixInvalidIdentifiers()
        {
            if (CanProcessEditModePersistables())
            {
                Persistable[] invalidPersistables = FindPersistables()
                    .Where(p => PersistanceUtil.IsMissingIdentifier(p))
                    .ToArray();

                if (invalidPersistables.Length > 0)
                {
                    foreach (Persistable persistable in invalidPersistables)
                    {
                        PersistanceUtil.GenerateIdentifierFor(persistable, "Fixed");
                        m_persistables.Add(GetObjectId(persistable));
                    }

                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
        }

        private static void OnActiveSceneChanged(Scene previous, Scene current)
        {
            if (CanProcessEditModePersistables())
            {
                CacheInitialPersistables();
            }
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (CanProcessEditModePersistables())
            {
                CacheInitialPersistables();
            }
        }

        private static void OnHierarchyChanged()
        {
            if (CanProcessEditModePersistables())
            {
                foreach (Persistable persistable in FindPersistables())
                {
                    if (m_persistables.Add(GetObjectId(persistable)))
                    {
                        PersistanceUtil.GenerateIdentifierFor(persistable);
                    }
                }

                // 兜底修复粘贴、反序列化或脚本刷新后仍缺失的标识。
                FixInvalidIdentifiers();
            }
        }

        private static int GetObjectId(Object target)
        {
#if UNITY_6000_0_OR_NEWER
            return target != null ? target.GetEntityId().GetHashCode() : 0;
#else
            return target != null ? target.GetInstanceID() : 0;
#endif
        }
    }
}

