using System;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public readonly struct PhysicsHandle : IEquatable<PhysicsHandle>
    {
        public readonly int Index;
        public readonly ushort Version;

        public static readonly PhysicsHandle Invalid = new(-1, 0);

        public PhysicsHandle(int index, ushort version)
        {
            Index = index; Version = version;
        }

        public bool Equals(PhysicsHandle other) => Index == other.Index && Version == other.Version;

        public bool IsValid => Index >= 0;

        public bool Equals(int index) => Index == index;
        public override bool Equals(object obj) => obj is PhysicsHandle h && Equals(h);
        public override int GetHashCode() => HashCode.Combine(Index, Version);
        public override string ToString() => $"PhysicsHandle({Index}, v{Version})";

        public static bool operator ==(PhysicsHandle a, PhysicsHandle b)
            => a.Index == b.Index && a.Version == b.Version;

        public static bool operator !=(PhysicsHandle a, PhysicsHandle b)
            => !(a == b);
    }

    [Flags]
    public enum PhysFlags : byte
    {
        None = 0,
        HasPos = 1,
        HasAngle = 2,
        IsForce = 4,
        IsImpulse = 8,
        IsTorque = 16,
        HasLinVel = 32,
        HasAngVel = 64,
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

    public enum BodyType
    {
        Kinematic,
        Dynamic,
        Static
    }

    public enum ColliderType
    {
        Box,
        Capsule,
        Circle
    }

    public struct BodyCreateSpec
    {
        public PhysicsHandle Handle;
        public BodyType BodyType;
        public float Mass;
        public ColliderType ColliderType;
        public float GravityScale;

        public Vec2 Position;
        public float Rotation;
    }


}