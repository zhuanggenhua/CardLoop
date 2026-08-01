using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YokiFrame;

namespace FantasyWord.GameCore
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
        [InspectorName("启动状态")]
        [Tooltip("系统启动时压入状态栈的第一层状态。")]
        [SerializeField] private EGameState m_startupState = EGameState.Gameplay;

        /// <summary>
        /// 当前栈顶状态；栈为空时为 None。
        /// </summary>
        public EGameState currentState => m_stateStack.Count > 0 ? m_stateStack.Peek() : EGameState.None;

        private Stack<EGameState> m_stateStack = new();

        public override void OnSystemStart()
        {
            EventKit.Type.Register<ReturnToMainMenuRequestedEvent>(OnReturnToMainMenuRequested);
            AddLayer(m_startupState);
        }

        public override void OnSystemStop()
        {
            EventKit.Type.UnRegister<ReturnToMainMenuRequestedEvent>(OnReturnToMainMenuRequested);
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

        private void OnEnterMenuState() => Time.timeScale = 0.0f;
        private void OnEnterGameplayState() => Time.timeScale = 1.0f;
        private void OnEnterDialogueState() { }
        private void OnExitMenuState() { }
        private void OnExitGameplayState() { }
        private void OnExitDialogueState() { }

        private void OnReturnToMainMenuRequested(ReturnToMainMenuRequestedEvent _)
        {
            Time.timeScale = 1.0f;
            SceneManager.LoadScene(GameManager.Config.mainMenuSceneName);
        }
    }
}

