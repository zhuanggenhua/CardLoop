using UnityEngine;
using MackySoft.SerializeReferenceExtensions;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 传送门要求的纵向移动方向；None 表示不限制纵向输入。
    /// </summary>
    public enum EVerticalDirection { None, Up, Down }

    /// <summary>
    /// 传送门要求的横向移动方向；None 表示不限制横向输入。
    /// </summary>
    public enum EHorizontalDirection { None, Left, Right }

    /// <summary>
    /// 带触发方向限制的检查点传送器，只允许当前地图通行角色触发传送。
    /// </summary>
    public class Teleporter : Checkpoint
    {
        [Header("目标设置")]
        [InspectorName("目标检查点")]
        [Tooltip("传送完成后到达的检查点。使用 SerializeReference 支持不同检查点实现。")]
        [SerializeReference, SubclassSelector] private ICheckpoint m_destination;

        [InspectorName("抵达后保存检查点")]
        [Tooltip("开启后，传送完成时把目标检查点保存为当前复活/读档位置。")]
        [SerializeField] private bool m_saveCheckpointOnArrival = false;

        [Header("触发设置")]
        [InspectorName("要求纵向移动")]
        [Tooltip("限制角色必须朝指定纵向方向移动时才触发传送。None 表示不检查纵向。")]
        [SerializeField] private EVerticalDirection m_requiredVerticalMovement = EVerticalDirection.None;

        [InspectorName("要求横向移动")]
        [Tooltip("限制角色必须朝指定横向方向移动时才触发传送。None 表示不检查横向。")]
        [SerializeField] private EHorizontalDirection m_requiredHorizontalMovement = EHorizontalDirection.None;

        [Header("音频")]
        [InspectorName("触发音效")]
        [Tooltip("传送正式触发时播放的音效解析器；为空时不播放音效。")]
        [SerializeField] private AudioClipResolver m_activationAudio;

        // 传送过程跨帧完成，用静态锁避免多个传送门在同一段流程内重复触发。
        private static bool _teleportationInProgress = false;

        private void OnTriggerStay2D(Collider2D collision)
        {
            CharacterActor traversalCharacter = GameManager.MapSystem.GetTraversalCharacter();
            CharacterActor collisionCharacter = collision != null
                ? collision.GetComponentInParent<CharacterActor>()
                : null;

            if (!_teleportationInProgress &&
                traversalCharacter != null &&
                collisionCharacter == traversalCharacter)
            {
                if (traversalCharacter.dead) return;

                if (m_requiredVerticalMovement == EVerticalDirection.Up && !traversalCharacter.IsMovingUp()) return;
                if (m_requiredVerticalMovement == EVerticalDirection.Down && !traversalCharacter.IsMovingDown()) return;
                if (m_requiredHorizontalMovement == EHorizontalDirection.Left && !traversalCharacter.IsMovingLeft()) return;
                if (m_requiredHorizontalMovement == EHorizontalDirection.Right && !traversalCharacter.IsMovingRight()) return;

                traversalCharacter.InterruptPush();

                GameRuntimeEvents.RequestAudioPlayback(m_activationAudio);

                _teleportationInProgress = true;

                GameManager.MapSystem.TeleportTo(m_destination, null, () =>
                {
                    if (m_saveCheckpointOnArrival)
                    {
                        GameManager.MapSystem.SaveCheckpoint(m_destination);
                    }
                    _teleportationInProgress = false;
                });
            }
        }
    }
}

