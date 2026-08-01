using System;
using UnityEngine;

namespace FantasyWord.GameCore
{
    /// <summary>
    /// 移动指定角色的命令，目标角色由 Inspector 显式配置。
    /// </summary>
    [Serializable]
    public class MoveCharacter : MoveCharacterBase
    {
        [InspectorName("目标角色")]
        [Tooltip("要移动的角色。")]
        [SerializeField] private CharacterBase m_toMove = null;

        protected override CharacterBase targetCharacter => m_toMove;
    }
}

