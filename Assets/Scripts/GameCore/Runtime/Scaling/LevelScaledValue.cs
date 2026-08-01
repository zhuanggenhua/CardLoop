using UnityEngine;

namespace FantasyWord.GameCore
{
    public abstract class LevelScaledValue<T>
    {
        [SerializeField] protected T m_initialValue;
        [SerializeField] private AnimationCurve m_evolutionProfile = AnimationCurve.Linear(0.0f, 0.0f, 1.0f, 1.0f);
        [SerializeField] private float m_evolutionScale = 1.0f;

        private const int kLevelRange = Constants.MaxLevel - Constants.MinLevel + 1;

        public T this[int level]
        {
            get
            {
                float t = (level - 1) / (float)kLevelRange;

                if (m_evolutionProfile != null)
                {
                    return Evalulate(1.0f + (m_evolutionProfile.Evaluate(t) * m_evolutionScale));
                }
                else
                {
                    Debug.LogWarning("No animation curve set, falling back to the initial value");
                    return m_initialValue;
                }
            }
        }

        protected abstract T Evalulate(float t);
    }
}

