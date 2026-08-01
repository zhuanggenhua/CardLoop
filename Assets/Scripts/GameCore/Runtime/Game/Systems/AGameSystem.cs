using UnityEngine;

namespace FantasyWord.GameCore
{
    public abstract class AGameSystem : MonoBehaviour
    {
        public virtual void OnSystemInit() { }
        public virtual void OnSystemStart() { }
        public virtual void OnSystemStop() { }

        public virtual void OnMapLoading() { }
        public virtual void OnMapLoaded() { }
        public virtual void OnMapUnloading() { }
        public virtual void OnMapUnloaded() { }

        public virtual void OnSaveFileLoaded() { }
    }
}

