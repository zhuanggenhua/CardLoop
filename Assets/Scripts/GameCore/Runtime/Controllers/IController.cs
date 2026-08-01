using System;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 控制器存档块基类，具体控制器可扩展自己的运行状态字段。
    /// </summary>
    [Serializable]
    public class IControllerDataBlock : DataBlock
    {

    }

    /// <summary>
    /// Movable 控制器合同，由角色或物体统一驱动初始化、启停、更新和 Gizmos 绘制。
    /// </summary>
    public interface IController : IDataBlockHandler<IControllerDataBlock>
    {
        public void Initialize(Movable movable);
        public void Terminate();
        public void Start();
        public void Stop();
        public void FixedUpdate();
        public void Update();
        public void DrawGizmos();
    }
}

