using System;

namespace FantasyWord.GameCore
{
    public interface ICondition
    {
        public bool Evaluate();
        public void StartListening(Action onStateChanged);
        public void StopListening();
    }
}

