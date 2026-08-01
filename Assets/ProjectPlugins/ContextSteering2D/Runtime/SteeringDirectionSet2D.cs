using System;
using UnityEngine;

namespace ContextSteering2D
{
    public sealed class SteeringDirectionSet2D
    {
        public static readonly SteeringDirectionSet2D Eight = new(8);
        public static readonly SteeringDirectionSet2D Sixteen = new(16);
        public static readonly SteeringDirectionSet2D ThirtyTwo = new(32);

        private readonly Vector2[] m_directions;

        public SteeringDirectionSet2D(int sampleCount)
        {
            if (sampleCount < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleCount), "Context steering requires at least four sample directions.");
            }

            m_directions = new Vector2[sampleCount];
            float step = Mathf.PI * 2.0f / sampleCount;
            for (int i = 0; i < sampleCount; i++)
            {
                float radians = Mathf.PI * 0.5f - step * i;
                m_directions[i] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
            }
        }

        public int Count => m_directions.Length;
        public ReadOnlySpan<Vector2> Directions => m_directions;
        public Vector2 this[int index] => m_directions[index];
    }
}
