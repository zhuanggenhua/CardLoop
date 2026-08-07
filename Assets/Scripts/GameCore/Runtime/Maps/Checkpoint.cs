using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 场景内检查点组件。
    /// 它负责检查点存档语义，以及顺序推进和强制覆盖规则。
    /// </summary>
    public class Checkpoint : Persistable
    {
        [Header("检查点")]
        [Tooltip("玩家进入触发器时是否保存该检查点。关闭后仍可被命令或其它系统手动保存。")]
        [SerializeField] private bool m_saveOnPlayerEnter = true;

        [Tooltip("检查点顺序。默认情况下，只有顺序不低于当前检查点的点位才会覆盖重生点。")]
        [SerializeField] private int m_checkpointOrder = 0;

        [Tooltip("忽略顺序限制，玩家进入时强制把该点设置为当前重生点。适合关卡入口或剧情传送落点。")]
        [SerializeField] private bool m_forceAssignation = false;

        public int checkpointOrder => m_checkpointOrder;
        public bool forceAssignation => m_forceAssignation;

        public ICheckpoint GetData()
        {
            return new PersistableCheckpoint
            {
                sceneAddress = GameManager.MapSystem.GetCurrentSceneAddress(),
                instance = this
            };
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TrySaveCheckpoint(other);
        }

        private void OnTriggerEnter(Collider other)
        {
            TrySaveCheckpoint(other);
        }

        private void TrySaveCheckpoint(Component other)
        {
            CharacterActor traversalCharacter = GameManager.MapSystem.GetTraversalCharacter();
            if (!m_saveOnPlayerEnter ||
                traversalCharacter == null ||
                other.GetComponentInParent<CharacterActor>() != traversalCharacter)
            {
                return;
            }

            GameManager.MapSystem.SaveCheckpoint(GetData(), m_checkpointOrder, m_forceAssignation);
        }
    }
}
