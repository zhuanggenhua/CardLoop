using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 控制器默认存档块；没有额外运行状态的控制器可直接使用。
    /// </summary>
    [Serializable]
    public class ControllerDataBlock : IControllerDataBlock
    {

    }

    /// <summary>
    /// 控制器基类，统一 Movable 主体绑定、启停生命周期、帧更新和存档回调。
    /// </summary>
    public abstract class AController<T> : IController where T : Movable
    {
        protected T m_subject = null;
        protected bool m_running = false;

        protected virtual void OnInitialize() { }
        protected virtual void OnTerminate() { }
        protected virtual void OnUpdate() { }
        protected virtual void OnFixedUpdate() { }
        protected virtual void OnStart() { }
        protected virtual void OnStop() { }
        protected virtual void OnDrawGizmos() { }
        protected virtual Type GetDataBlockType() => typeof(ControllerDataBlock);
        protected virtual void OnSave(IControllerDataBlock block) { }
        protected virtual void OnLoad(IControllerDataBlock block) { }

        public void Initialize(Movable movable)
        {
            Debug.Assert(movable is T, $"Cannot initialize a controller without a subject of type {typeof(T).Name}");
            m_subject = (T)movable;
            OnInitialize();
        }

        public void Terminate()
        {
            if (m_running)
            {
                Stop();
            }
            OnTerminate();
        }

        public void Start()
        {
            if (!m_running)
            {
                m_running = true;
                OnStart();
            }
        }

        public virtual void Stop()
        {
            if (m_running)
            {
                m_running = false;
                OnStop();
            }
        }

        public virtual void FixedUpdate()
        {
            if (m_running)
            {
                OnFixedUpdate();
            }
        }

        public virtual void Update()
        {
            if (m_running)
            {
                OnUpdate();
            }
        }

        public virtual void DrawGizmos()
        {
            if (m_running)
            {
                OnDrawGizmos();
            }
        }

        public IControllerDataBlock CreateDataBlock()
        {
            ControllerDataBlock block = (ControllerDataBlock)Activator.CreateInstance(GetDataBlockType());
            OnSave(block);
            return block;
        }

        public void LoadDataBlock(IControllerDataBlock block)
        {
            OnLoad(block);
        }
    }
}

