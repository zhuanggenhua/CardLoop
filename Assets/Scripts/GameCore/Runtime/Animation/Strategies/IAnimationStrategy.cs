using UnityEngine;
using UnityEngine.Events;

namespace GameCore
{
    public interface IAnimationStrategy
    {
        void AddDeathAnimationStartedListener(UnityAction listener);
        void RemoveDeathAnimationStartedListener(UnityAction listener);
        void AddDeathAnimationEndedListener(UnityAction listener);
        void RemoveDeathAnimationEndedListener(UnityAction listener);

        void Initialize();
        void Pause();
        void Resume();
        void OnInvincibleAnimationStart();
        void OnInvincibleAnimationStop();
        void OnDeathAnimationStart();
        void OnDeathAnimationStop();
        void SetLookAtDirection(Vector2 direction);
        void SetTargetDirection(Vector2 direction);
        void SetMovement(Vector2 speed);
        bool PlayHitAnimation();
        bool PlayDeathAnimation();
        bool PlayInvincibleAnimation();
        bool IsInvincibleAnimationPlaying();
    }
}

