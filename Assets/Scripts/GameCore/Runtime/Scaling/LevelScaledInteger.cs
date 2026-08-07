using System;
using Unity.Mathematics;

namespace GameCore
{
    [Serializable]
    public class LevelScaledInteger : LevelScaledValue<int>
    {
        protected override int Evalulate(float t)
        {
            return (int)math.floor(m_initialValue * t);
        }
    }
}

