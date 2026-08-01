
#nullable enable
using UnityAiBridge.Data;

namespace UnityAiBridge.Extensions
{
    public static class ExtensionsRuntimeObjectRef
    {
        public static UnityEngine.Object? FindObject(this ObjectRef? objectRef)
        {
            if (objectRef == null)
                return null;

#if UNITY_EDITOR
            if (objectRef.InstanceID != 0)
            {
                return UnityAiBridge.Utils.ObjectIdCompat.InstanceIdToObject(objectRef.InstanceID);
            }
#endif
            return null;
        }
        public static ObjectRef? ToObjectRef(this UnityEngine.Object? obj)
        {
            return new ObjectRef(obj);
        }
    }
}
