using System;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public struct PhysicsHandle : IEquatable<PhysicsHandle>
    {
        public int Value;
        public PhysicsHandle(int value) => Value = value;

        public readonly bool Equals(PhysicsHandle other) => Value == other.Value;
        public override readonly bool Equals(object obj) => obj is PhysicsHandle id && Equals(id);
        public override readonly int GetHashCode() => Value;

        public static implicit operator int(PhysicsHandle id) => id.Value;
        public static implicit operator PhysicsHandle(int value) => new(value);

        public override string ToString() => $"RecipeId({Value})";
    }

    public enum PhysicsOpKind { None, AddForce, Teleport, PhysSync, SetTransform }


    public struct PhysicsOp
    {
        public PhysicsOpKind Kind;
        public Entity Entity;
        public int DataIndex;
    }

}