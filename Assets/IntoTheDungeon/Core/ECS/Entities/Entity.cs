namespace IntoTheDungeon.Core.ECS.Entities
{
    public readonly struct Entity : System.IEquatable<Entity>
    {
        public readonly int Index;    // 메타데이터 배열의 인덱스
        public readonly int Version;  // Entity 재사용 감지


        public Entity(int index, int version)
        {
            Index = index;
            Version = version;
        }

        public bool Equals(Entity other)
            => Index == other.Index && Version == other.Version;

        public override bool Equals(object obj)
            => obj is Entity other && Equals(other);

        public override int GetHashCode()
            => System.HashCode.Combine(Index, Version);

        public override string ToString()
            => $"Entity({Index}, v{Version})";

        // public bool IsValid() => Index > 0 && Version > 0;

        public static bool operator ==(Entity a, Entity b) => a.Equals(b);
        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);

        public static readonly Entity Null = new Entity(-1, 0);
    }
}