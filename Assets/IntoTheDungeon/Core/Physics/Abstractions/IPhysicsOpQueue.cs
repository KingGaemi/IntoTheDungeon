
using System;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface IPhysicsOpQueue
    {
        int Count { get; }
        void EnqueueAddForce(PhysicsHandle handle, Vec2 force);
        void EnqueueTeleport(PhysicsHandle handle, Vec2 position, float angle = 0f);
        void EnqueueSyncEcsToPhys(PhysicsHandle h);
        void EnqueueSyncPhysToEcs(PhysicsHandle h);
        void EnqueueSetTransform(PhysicsHandle handle, Vec2 position, float angle);
        void Enqueue(PhysicsHandle handle, PhysicsOpKind kind, Vec2 vec, float angle = 0f, PhysFlags flags = PhysFlags.None);
        void EnqueueAddImpulse(PhysicsHandle h, Vec2 impulse);
        void EnqueueSetLinearVelocity(PhysicsHandle h, Vec2 v);
        void EnqueueSetAngularVelocity(PhysicsHandle h, float w);
        void EnqueueAddTorque(PhysicsHandle h, float torque);
        void EnsureCapacity(int min);
        ReadOnlySpan<PhysicsHandle> Handles { get; }
        ReadOnlySpan<PhysicsOpKind> Kinds { get; }
        ReadOnlySpan<Vec2> Vecs { get; }
        ReadOnlySpan<float> Angles { get; }
        ReadOnlySpan<PhysFlags> Flags { get; }
        void ClearFrame();
    }
}