using System;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Abstractions
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

    [Flags]
    public enum PhysFlags : byte
    {
        None = 0,
        HasPos = 1,
        HasAngle = 2,
        IsForce = 4,
        IsImpulse = 8,
        IsTorque = 16
    }

    public enum PhysicsOpKind
    {
        None,
        AddForce,
        AddImpulse,
        AddTorque,
        Teleport,
        SyncEcsToPhys,
        SyncPhysToEcs,
        SetTransform,
        SetLinearVelocity,
        SetAngularVelocity
    }

    public struct PhysicsOp
    {
        public PhysicsHandle Handle;
        public PhysFlags Flags;   // HasPos, HasAngle, HasPivot
        public Vec2 PosOrForce;     // HasPos일 때 위치/이동, AddForce면 힘
        public float Angle;         // HasAngle일 때 사용(라디안)
        public Vec2 Pivot;          // HasPivot일 때만(특수 회전 중심 필요 시)
        public PhysicsOpKind Kind;  // AddForce, Teleport, PhysSync, SetTransform...
    }


}