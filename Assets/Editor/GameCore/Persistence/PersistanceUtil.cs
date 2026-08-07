using UnityEditor;
using UnityEngine;

namespace GameCore
{
    public static class PersistanceUtil
    {
        public static string GenerateIdentifier()
        {
            return System.Guid.NewGuid().ToString();
        }

        public static void GenerateIdentifierFor(Persistable persistable, string actionName = null)
        {
            SetIdentifierFor(persistable, GenerateIdentifier(), actionName);
        }

        public static void SetIdentifierFor(Persistable persistable, string value, string actionName = null)
        {
            Debug.Assert(!string.IsNullOrEmpty(value), "The identifier can't be null or empty");
            Debug.Assert(!Application.isPlaying, "Identifier should not be set at runtime");

            string action =
                actionName != null ? actionName :
                    !persistable.HasEditorPersistenceInfo() ?
                    "Created" :
                    "Updated";

            PreInstancedPersistentDataHandler handler = new();
            handler.identifier = value;
            persistable.EditorPersistenceInfo = handler;

            Debug.Log($"<Persistable {action}> <b>{persistable.name}</b> => <i>{handler.identifier}</i>");

            EditorUtility.SetDirty(persistable);
        }

        public static string GetIdentifierFrom(Persistable persistable)
        {
            Debug.Assert(persistable.EditorPersistenceInfo is PreInstancedPersistentDataHandler, "This object doesn't have an identifier");
            return persistable.GetPersistentIdentifier();
        }

        public static bool IsMissingIdentifier(Persistable persistable)
        {
            return
                !persistable.HasEditorPersistenceInfo() ||
                persistable.EditorPersistenceInfo is not PreInstancedPersistentDataHandler ||
                string.IsNullOrEmpty(GetIdentifierFrom(persistable));
        }
    }
}

