using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace TableForge.Editor.Serialization
{
    [System.Serializable]
    internal class SerializableObject
    {
        public string name;
        public string path;
        public string guid;
        public int instanceID;
        private static readonly MethodInfo GetInstanceIdMethod =
            typeof(Object).GetMethod("GetInstanceID", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo InstanceIdToObjectMethod =
            typeof(EditorUtility).GetMethod(
                "InstanceIDToObject",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);
        
        public SerializableObject(string guid, string path, Object obj)
        {
            this.guid = guid;
            name = obj.name;
            instanceID = GetInstanceIdMethod?.Invoke(obj, null) is int id ? id : 0;
        }
        
        public Object ToObject()
        {
            return InstanceIdToObjectMethod?.Invoke(null, new object[] { instanceID }) as Object ??
                   AssetDatabase.LoadAssetAtPath<Object>(path) ??
                   AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
