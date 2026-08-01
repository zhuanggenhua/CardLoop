using UnityEngine;

namespace FantasyWord.GameCore
{
    public static class AnimationUtils
    {
        public static bool HasParameter(Animator animator, string parameter)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == parameter)
                {
                    return true;
                }
            }

            return false;
        }

        public static EDirection GetDirectionFromVector(Vector2 direction)
        {
            bool isHorizontal = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);

            float axisValue = isHorizontal ? direction.x : direction.y;

            if (axisValue != 0.0f)
            {
                if (isHorizontal)
                {
                    return axisValue > 0.0f ? EDirection.Right : EDirection.Left;
                }
                else
                {
                    return axisValue > 0.0f ? EDirection.Up : EDirection.Down;
                }
            }

            return EDirection.None;
        }
    }
}
