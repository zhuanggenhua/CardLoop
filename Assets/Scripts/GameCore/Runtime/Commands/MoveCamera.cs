using Sirenix.OdinInspector;
using System;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace GameCore
{
    /// <summary>
    /// 镜头移动策略接口。
    /// 命令只依赖这一层合同，具体移动到坐标、对象或复位由策略实现。
    /// </summary>
    public interface ICameraMovementStrategy
    {
        Task MoveCameraAsync();
    }

    /// <summary>
    /// 镜头移动策略基类。
    /// 它统一处理相机存在性、移动速度和异步插值，子类只提供目标点。
    /// </summary>
    public abstract class ACameraMovementStrategy : ICameraMovementStrategy
    {
        [LabelText("移动速度")]
        [Tooltip("镜头插值到目标位置的世界单位/秒。正式玩法相机缺失时会报警并跳过。")]
        [SerializeField] private float m_speed = 10f;

        public abstract Task MoveCameraAsync();

        public async Task MoveCameraToAsync(Vector2 targetPosition, bool localMode = false)
        {
            var targetCamera = GameManager.MainCamera;
            if (targetCamera == null)
            {
                Debug.LogError($"[{nameof(ACameraMovementStrategy)}] 当前没有可用的正式玩法相机，无法执行镜头移动。");
                return;
            }

            Vector3 _GetCameraPosition() => localMode ? targetCamera.transform.localPosition : targetCamera.transform.position;
            var initialPosition = _GetCameraPosition();

            float duration = 0.0f;

            float transitionDuration = Vector2.Distance(
                initialPosition,
                targetPosition
            ) / m_speed;

            while (duration < transitionDuration)
            {
                Vector2 positionThisFrame = Vector2.Lerp(
                    initialPosition,
                    targetPosition,
                    math.min(duration, transitionDuration) / transitionDuration
                );

                Vector3 currentPosition = new(
                    positionThisFrame.x,
                    positionThisFrame.y,
                    _GetCameraPosition().z
                );

                if (localMode)
                {
                    targetCamera.transform.localPosition = currentPosition;
                }
                else
                {
                    targetCamera.transform.position = currentPosition;
                }

                duration += Time.deltaTime;

                await Task.Yield();
            }
        }
    }

    /// <summary>
    /// 把正式玩法相机移动到固定世界坐标。
    /// </summary>
    [Serializable]
    public class MoveCameraToPosition : ACameraMovementStrategy
    {
        [LabelText("目标世界坐标")]
        [Tooltip("镜头移动的世界 XY 坐标，Z 轴保持当前相机值。")]
        [SerializeField] private Vector2 m_targetPosition;

        public override async Task MoveCameraAsync() => await MoveCameraToAsync(m_targetPosition);
    }

    /// <summary>
    /// 把正式玩法相机移动到指定场景对象位置。
    /// 目标对象由命令配置显式引用，不在运行时按名称查找。
    /// </summary>
    [Serializable]
    public class MoveCameraToGameObject : ACameraMovementStrategy
    {
        [LabelText("目标对象")]
        [Tooltip("镜头要跟随移动到的场景对象。必须由命令资产或场景显式配置。")]
        [SerializeField] private GameObject m_targetGameObject;

        public override async Task MoveCameraAsync() => await MoveCameraToAsync(m_targetGameObject.transform.position);
    }

    /// <summary>
    /// 把相机局部坐标复位到原点。
    /// 用于剧情或临时镜头结束后恢复父节点下的默认位置。
    /// </summary>
    [Serializable]
    public class ResetCamera : ACameraMovementStrategy
    {
        public override async Task MoveCameraAsync() => await MoveCameraToAsync(Vector2.zero, true);
    }

    /// <summary>
    /// 可序列化的镜头命令。
    /// 它只调度配置好的移动策略，不直接持有或搜索具体 Camera 实例。
    /// </summary>
    [Serializable]
    public class MoveCamera : IContextualCommand
    {
        [LabelText("镜头移动策略")]
        [Tooltip("选择移动到坐标、对象或复位等具体策略。缺失时会暴露配置错误。")]
        [SerializeReference, SubclassSelector] private ICameraMovementStrategy m_cameraMovementStrategy;

        public Task Execute()
        {
            return Execute(GameCommandContext.Script());
        }

        public Task Execute(GameCommandContext context)
        {
            if (m_cameraMovementStrategy == null)
            {
                throw new InvalidOperationException($"{nameof(MoveCamera)} 缺少镜头移动策略。");
            }

            return m_cameraMovementStrategy.MoveCameraAsync();
        }
    }
}

