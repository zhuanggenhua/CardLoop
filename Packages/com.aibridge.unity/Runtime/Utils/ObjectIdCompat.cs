#nullable enable

using System.Reflection;
using UnityEngine;

namespace UnityAiBridge.Utils
{
    public static class ObjectIdCompat
    {
        private static readonly MethodInfo? GetInstanceIdMethod =
            typeof(Object).GetMethod("GetInstanceID", BindingFlags.Instance | BindingFlags.Public);

        public static int GetInstanceId(Object? obj)
        {
            if (obj == null)
                return 0;

            return GetInstanceIdMethod?.Invoke(obj, null) is int id ? id : 0;
        }

#if UNITY_EDITOR
        private static readonly MethodInfo? InstanceIdToObjectMethod =
            typeof(UnityEditor.EditorUtility).GetMethod(
                "InstanceIDToObject",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(int) },
                null);

        public static Object? InstanceIdToObject(int instanceId)
        {
            if (instanceId == 0)
                return null;

            return InstanceIdToObjectMethod?.Invoke(null, new object[] { instanceId }) as Object;
        }
#endif
    }
}
