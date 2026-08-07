using System;

namespace GameCore
{
    public abstract class ABaseCondition : ICondition
    {
        private Action m_stateChangedCallback = null;
        private bool m_isListening;

        public abstract bool Evaluate();

        public virtual void StartListening(Action onStateChanged)
        {
            if (onStateChanged == null)
            {
                throw new ArgumentNullException(nameof(onStateChanged));
            }

            StopListening();
            m_stateChangedCallback = onStateChanged;
            m_isListening = true;
            OnStartListening();
        }

        public virtual void StopListening()
        {
            if (!m_isListening)
            {
                m_stateChangedCallback = null;
                return;
            }

            m_isListening = false;
            OnStopListening();
            m_stateChangedCallback = null;
        }

        protected void NotifyStateChange()
        {
            m_stateChangedCallback?.Invoke();
        }

        protected virtual void OnStartListening() { }
        protected virtual void OnStopListening() { }
    }
}
