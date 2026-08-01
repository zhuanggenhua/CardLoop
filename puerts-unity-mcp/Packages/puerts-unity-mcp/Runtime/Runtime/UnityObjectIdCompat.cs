using System.Reflection;
using UnityEngine;

namespace PuertsUnityMcp
{
    public static class UnityObjectIdCompat
    {
        private static readonly MethodInfo GetInstanceIdMethod =
            typeof(Object).GetMethod("GetInstanceID", BindingFlags.Instance | BindingFlags.Public);

        public static int GetInstanceId(Object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            return GetInstanceIdMethod?.Invoke(obj, null) is int id ? id : 0;
        }
    }
}
