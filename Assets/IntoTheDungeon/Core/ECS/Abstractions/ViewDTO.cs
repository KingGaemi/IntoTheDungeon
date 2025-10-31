using System;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Physics.Abstractions;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public struct ViewId : IEquatable<ViewId>
    {
        public int Value;
        public ViewId(int value) => Value = value;

        public readonly bool Equals(ViewId other) => Value == other.Value;
        public override readonly bool Equals(object obj) => obj is ViewId id && Equals(id);
        public override readonly int GetHashCode() => Value;

        public static implicit operator int(ViewId id) => id.Value;
        public static implicit operator ViewId(int value) => new(value);

        public override string ToString() => $"RecipeId({Value})";
    }
    public enum ViewOpKind { None, Spawn, Despawn, SetTransform }

    // 고정 길이 헤더 + 확장 가능한 사양
    public struct ViewOp
    {
        public ViewOpKind Kind;
        public Entity Entity;
        public int DataIndex;
    }
    public struct SetTransformOp
    {
        public float X, Y, RotDeg;
        public SetTransformOp(float x, float y, float r) { X = x; Y = y; RotDeg = r; }
    }

    // 유니티가 해석할 스폰 사양
    public struct ViewSpawnData
    {
        public RecipeId RecipeId;
        public PhysicsHandle PhysicsHandle;
        public int SceneLinkId;
        public short SortingLayerId;
        public short OrderInLayer;

    }

    public struct TransformData
    {
        public float X, Y, RotDeg;
    }


}
