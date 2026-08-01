using UnityEngine;

namespace FantasyWord.GameCore
{
    internal static class CollisionDispatcher
    {
        internal static void RegisterCollision(Movable source, GameObject target)
        {
            IMovableCollisionReceiver[] receivers = target.GetComponents<IMovableCollisionReceiver>();
            for (int i = 0; i < receivers.Length; ++i)
            {
                receivers[i].OnMovableCollision(source);
            }
        }
    }
}

