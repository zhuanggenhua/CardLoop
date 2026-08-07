using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    public class UIControllerButton : MonoBehaviour
    {
        [SerializeField] private Image m_image = null;
        [SerializeField] private SpriteRenderer m_spriteRenderer = null;
        [SerializeField] private UIControllerButtonManager.EAction m_action;

        private Dictionary<UIControllerButtonManager.EButtonState, Sprite> m_sprites;
        private Func<bool> m_isPressed = null;
        private UIControllerButtonManager m_manager = null;
        private bool m_registered = false;
        private bool m_promptVisible = true;

        public UIControllerButtonManager.EAction action => m_action;

        private void OnEnable()
        {
            RegisterWithManagerIfReady();
        }

        private void Start()
        {
            RegisterWithManagerIfReady();
        }

        private void OnDisable()
        {
            UnregisterFromManager();
        }

        private void OnDestroy()
        {
            UnregisterFromManager();
        }

        private void Update()
        {
            RefreshSprite();
        }

        private UIControllerButtonManager.EButtonState GetCurrentButtonState()
        {
            if (m_isPressed != null)
            {
                return
                    m_isPressed() ?
                    UIControllerButtonManager.EButtonState.Pressed :
                    UIControllerButtonManager.EButtonState.Idle;
            }

            return UIControllerButtonManager.EButtonState.Idle;
        }

        public void Initialize(Dictionary<UIControllerButtonManager.EButtonState, Sprite> sprites, Func<bool> isPressed = null)
        {
            m_sprites = sprites;
            m_isPressed = isPressed;
            RefreshSprite();
        }

        public void SetPromptVisible(bool visible)
        {
            if (m_promptVisible == visible)
            {
                return;
            }

            m_promptVisible = visible;
            RefreshSprite();
        }

        public void SetAction(UIControllerButtonManager.EAction action)
        {
            m_action = action;
            RegisterWithManagerIfReady();
            m_manager?.ForceUpdateButton(this);
        }

        private void RegisterWithManagerIfReady()
        {
            if (m_registered)
            {
                return;
            }

            m_manager = ResolveManager();
            if (m_manager == null)
            {
                return;
            }

            m_manager.RegisterButton(this);
            m_registered = true;
        }

        private void UnregisterFromManager()
        {
            if (!m_registered)
            {
                return;
            }

            m_manager?.UnregisterButton(this);
            m_registered = false;
        }

        private UIControllerButtonManager ResolveManager()
        {
            Canvas canvasRoot = GetComponentInParent<Canvas>(true);
            if (canvasRoot == null)
            {
                return GetComponentInParent<UIControllerButtonManager>(true);
            }

            // 控制器提示按钮只在当前正式 UI 根内解析管理器，避免再依赖全局静态单例。
            return canvasRoot.GetComponentInChildren<UIControllerButtonManager>(true);
        }

        private void RefreshSprite()
        {
            Sprite sprite = null;
            if (m_promptVisible && m_sprites != null)
            {
                m_sprites.TryGetValue(GetCurrentButtonState(), out sprite);
            }

            if (m_image)
            {
                m_image.sprite = sprite;
                m_image.enabled = sprite != null;
            }

            if (m_spriteRenderer)
            {
                m_spriteRenderer.sprite = sprite;
                m_spriteRenderer.enabled = sprite != null;
            }
        }
    }
}
