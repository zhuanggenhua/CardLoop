using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 游戏全局状态层，用于切换输入映射和暂停语义。
    /// </summary>
    public enum EGameState
    {
        None,
        Menu,
        Dialogue,
        Gameplay
    }

    /// <summary>
    /// 全局状态栈系统，菜单和对话以层的形式覆盖 gameplay 状态。
    /// </summary>
    public class GameStateSystem : AGameSystem
    {
        private static readonly Type[] SystemStartupDependencies = { typeof(InputSystem) };

        [LabelText("启动状态")]
        [Tooltip("系统启动时压入状态栈的第一层状态。")]
        [SerializeField] private EGameState m_startupState = EGameState.Gameplay;

        /// <summary>
        /// 当前栈顶状态；栈为空时为 None。
        /// </summary>
        public EGameState currentState => m_stateStack.Count > 0 ? m_stateStack.Peek() : EGameState.None;

        private Stack<EGameState> m_stateStack = new();
        private readonly HashSet<object> m_externalPauseLocks = new();
        private bool m_isRunning;

        /// <summary>
        /// 外部短时流程是否正在暂停 Gameplay 时间；菜单暂停仍由状态栈决定。
        /// </summary>
        public bool IsExternallyPaused => m_externalPauseLocks.Count > 0;

        public override IReadOnlyCollection<Type> StartupDependencies => SystemStartupDependencies;

        public override void OnSystemStart()
        {
            if (m_isRunning)
            {
                throw new InvalidOperationException("游戏状态系统已经启动，不能重复启动。");
            }

            m_isRunning = true;
            try
            {
                m_stateStack.Clear();
                m_externalPauseLocks.Clear();
                AddLayer(m_startupState);
            }
            catch
            {
                OnSystemStop();
                throw;
            }
        }

        public override void OnSystemStop()
        {
            m_isRunning = false;
            m_stateStack.Clear();
            m_externalPauseLocks.Clear();
            Time.timeScale = 1.0f;
        }

        public override void OnSystemShutdown()
        {
            m_externalPauseLocks.Clear();
            Time.timeScale = 1.0f;
        }

        /// <summary>
        /// 申请外部暂停。用于商贩解锁等短时流程接管桌面，不能替代菜单或对话状态。
        /// </summary>
        public void AddExternalPauseLock(object requester)
        {
            if (requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }
            if (!m_externalPauseLocks.Add(requester))
            {
                throw new InvalidOperationException("同一个请求方重复申请外部暂停。");
            }
            ApplyTimeScaleForCurrentState();
        }

        /// <summary>
        /// 释放外部暂停。释放不存在的暂停锁属于内部生命周期错误，必须直接暴露。
        /// </summary>
        public void RemoveExternalPauseLock(object requester)
        {
            if (requester == null)
            {
                throw new ArgumentNullException(nameof(requester));
            }
            if (!m_externalPauseLocks.Remove(requester))
            {
                throw new InvalidOperationException("请求方释放了并未持有的外部暂停锁。");
            }
            ApplyTimeScaleForCurrentState();
        }

        /// <summary>
        /// 移除栈顶状态层；调用方必须保证要移除的状态就是当前栈顶。
        /// </summary>
        public void RemoveLayer(EGameState state)
        {
            Debug.AssertFormat(m_stateStack.Peek() == state, "Failed removing layer {0}. Make sure the layer you tried removing is at the top of the state stack", state);
            OnExitState(m_stateStack.Pop());

            if (m_stateStack.Count > 0)
            {
                OnEnterState(m_stateStack.Peek());
            }
        }

        /// <summary>
        /// 在状态栈顶压入新状态层，并立即进入该状态。
        /// </summary>
        public void AddLayer(EGameState state)
        {
            m_stateStack.Push(state);
            OnEnterState(m_stateStack.Peek());
        }

        private void OnEnterState(EGameState state)
        {
            OnStateChanged();

            switch (state)
            {
                case EGameState.Menu: OnEnterMenuState(); break;
                case EGameState.Dialogue: OnEnterDialogueState(); break;
                case EGameState.Gameplay: OnEnterGameplayState(); break;
            }
        }

        private void OnExitState(EGameState state)
        {
            OnStateChanged();

            switch (state)
            {
                case EGameState.Menu: OnExitMenuState(); break;
                case EGameState.Dialogue: OnExitDialogueState(); break;
                case EGameState.Gameplay: OnExitGameplayState(); break;
            }
        }

        private void OnStateChanged()
        {
            switch (currentState)
            {
                case EGameState.Gameplay:
                    GameManager.InputSystem.SetActionMap(EActionMap.Gameplay);
                    break;

                case EGameState.Dialogue:
                    GameManager.InputSystem.SetActionMap(EActionMap.UI);
                    break;

                case EGameState.Menu:
                    GameManager.InputSystem.SetActionMap(EActionMap.UI);
                    break;

                default:
                    GameManager.InputSystem.SetActionMap(EActionMap.None);
                    break;
            }
        }

        private void OnEnterMenuState() => ApplyTimeScaleForCurrentState();
        private void OnEnterGameplayState() => ApplyTimeScaleForCurrentState();
        private void OnEnterDialogueState() => ApplyTimeScaleForCurrentState();
        private void OnExitMenuState() => ApplyTimeScaleForCurrentState();
        private void OnExitGameplayState() => ApplyTimeScaleForCurrentState();
        private void OnExitDialogueState() => ApplyTimeScaleForCurrentState();

        private void ApplyTimeScaleForCurrentState()
        {
            Time.timeScale = IsExternallyPaused || m_stateStack.Contains(EGameState.Menu)
                ? 0.0f
                : 1.0f;
        }

    }
}

