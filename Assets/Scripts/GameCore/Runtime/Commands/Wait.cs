using System;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FantasyWord.GameCore
{
    [Serializable]
    public class Wait : IContextualCommand
    {
        [Min(0.0f)]
        [SerializeField] private float m_duration = 1.0f;

        public async Task Execute()
        {
            await Execute(GameCommandContext.Script());
        }

        public async Task Execute(GameCommandContext context)
        {
            await UniTask.WaitForSeconds(Mathf.Max(0.0f, m_duration));
        }
    }
}
