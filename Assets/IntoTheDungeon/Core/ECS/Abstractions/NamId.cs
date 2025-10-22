using System;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    // 인덱스 기반 ID - 충돌 없음 보장
    public readonly struct NameId : IEquatable<NameId>
    {
        public readonly int Value;

        internal NameId(int value) => Value = value;

        public bool Equals(NameId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is NameId other && Equals(other);
        public override int GetHashCode() => Value;

        public static bool operator ==(NameId a, NameId b) => a.Value == b.Value;
        public static bool operator !=(NameId a, NameId b) => a.Value != b.Value;
    }


}