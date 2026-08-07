using System;

namespace GameCore
{
    [Serializable]
    public class LevelScaledStats : LevelScaledValue<Stats>
    {
        protected override Stats Evalulate(float t)
        {
            return m_initialValue * t;
        }
    }
}

