using System;

namespace GameCore
{
    /// <summary>
    /// 控制组对外快照。
    /// UI、相机或调试面板只读取这份只读投影，不直接接触控制组内部集合。
    /// </summary>
    public readonly struct PlayerControlGroupSnapshot
    {
        public PlayerControlGroupSnapshot(CharacterBase[] members)
        {
            Members = members ?? Array.Empty<CharacterBase>();
        }

        public CharacterBase[] Members { get; }
        public CharacterBase PrimaryMember => MemberCount > 0 ? Members[0] : null;
        public int MemberCount => Members?.Length ?? 0;
        public bool IsValid => MemberCount > 0;
    }
}
