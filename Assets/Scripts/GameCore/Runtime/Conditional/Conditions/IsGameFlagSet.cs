using System;
using UnityEngine;
using YokiFrame;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class IsGameFlagSet : ABaseCondition
    {
        [SerializeField] private string m_gameFlag = string.Empty;

        public override bool Evaluate() => GameManager.GameFlagSystem.Get(m_gameFlag);

        protected override void OnStartListening()
        {
            EventKit.Type.Register<GameFlagChangedEvent>(OnGameFlagChanged);
        }

        protected override void OnStopListening()
        {
            EventKit.Type.UnRegister<GameFlagChangedEvent>(OnGameFlagChanged);
        }

        private void OnGameFlagChanged(GameFlagChangedEvent gameFlagChangedEvent) => NotifyStateChange();
    }
}

