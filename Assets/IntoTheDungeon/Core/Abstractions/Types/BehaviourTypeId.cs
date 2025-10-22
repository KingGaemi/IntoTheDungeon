using System;

namespace IntoTheDungeon.Core.Abstractions.Types
{
    public readonly struct BehaviourTypeId : IEquatable<BehaviourTypeId>
    {
        public readonly int Value;
        public BehaviourTypeId(int value) => Value = value;

        public bool Equals(BehaviourTypeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BehaviourTypeId id && Equals(id);
        public override int GetHashCode() => Value;

        public static implicit operator int(BehaviourTypeId id) => id.Value;
        public static implicit operator BehaviourTypeId(int value) => new(value);
        public override string ToString() => $"BehaviourTypeId({Value})";

        public static bool operator ==(BehaviourTypeId left, BehaviourTypeId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BehaviourTypeId left, BehaviourTypeId right)
        {
            return !(left == right);
        }
    }
}
