using System;
using Gameplay.Content;

namespace Gameplay.Tabletop
{
    /// <summary>
    /// 一局可堆叠卡牌状态中的卡牌引用。它由权威卡牌状态自动分配，不是作者维护的内容 ID。
    /// </summary>
    public readonly struct TabletopCardId : IEquatable<TabletopCardId>
    {
        /// <summary>
        /// 仅供卡牌状态创建局内卡牌引用。外部模块只能持有和传递该值，不能自行分配。
        /// </summary>
        internal TabletopCardId(ulong value)
        {
            Value = value;
        }

        /// <summary>权威卡牌状态分配的局内序号；零值保留为“无卡牌”。</summary>
        public ulong Value { get; }

        /// <summary>该引用是否指向一个已分配过的局内卡牌序号。</summary>
        public bool IsValid => Value != 0;

        public bool Equals(TabletopCardId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TabletopCardId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();

        public static bool operator ==(TabletopCardId left, TabletopCardId right) => left.Equals(right);
        public static bool operator !=(TabletopCardId left, TabletopCardId right) => !left.Equals(right);
    }

    /// <summary>
    /// 可堆叠牌桌卡牌的最小运行时状态。生命、装备、行动和其它规则状态归后续模块。
    /// </summary>
    public sealed class TabletopCard
    {
        /// <summary>
        /// 由卡牌状态创建最小局内卡牌。内容 ID 只说明它引用哪份作者数据，不包含运行时规则状态。
        /// </summary>
        internal TabletopCard(TabletopCardId id, ContentId contentId)
        {
            Id = id;
            ContentId = contentId;
        }

        /// <summary>本局内用于引用该卡牌实例的身份，不可跨局作为存档内容身份。</summary>
        public TabletopCardId Id { get; }

        /// <summary>该卡牌实例引用的作者内容身份；多张局内卡牌可以共享同一内容 ID。</summary>
        public ContentId ContentId { get; }
    }
}
