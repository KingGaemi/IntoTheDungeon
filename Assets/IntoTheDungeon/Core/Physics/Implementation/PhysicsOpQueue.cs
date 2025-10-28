using System;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Implementation
{
    public sealed class PhysicsOpQueue : IPhysicsOpQueue
    {
        PhysicsHandle[] _handles;
        PhysicsOpKind[] _kinds;
        PhysFlags[] _flags;
        Vec2[] _vecs;
        float[] _angles;

        public int Count { get; private set; }

        public ReadOnlySpan<PhysicsHandle> Handles => new(_handles, 0, Count);
        public ReadOnlySpan<PhysicsOpKind> Kinds => new(_kinds, 0, Count);
        public ReadOnlySpan<Vec2> Vecs => new(_vecs, 0, Count);
        public ReadOnlySpan<float> Angles => new(_angles, 0, Count);
        public ReadOnlySpan<PhysFlags> Flags => new(_flags, 0, Count);

        public PhysicsOpQueue(int capacity = 512)
        {
            _handles = new PhysicsHandle[capacity];
            _kinds = new PhysicsOpKind[capacity];
            _flags = new PhysFlags[capacity];
            _vecs = new Vec2[capacity];
            _angles = new float[capacity];
            Count = 0;
        }

        public void EnqueueAddForce(PhysicsHandle h, Vec2 force)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.AddForce;
            _flags[i] = PhysFlags.IsForce;
            _vecs[i] = force;
            _angles[i] = 0f;
        }

        public void EnqueueAddImpulse(PhysicsHandle h, Vec2 impulse)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.AddImpulse;
            _flags[i] = PhysFlags.IsImpulse;
            _vecs[i] = impulse;
            _angles[i] = 0f;
        }

        public void EnqueueAddTorque(PhysicsHandle h, float torque)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.AddTorque;
            _flags[i] = PhysFlags.IsTorque | PhysFlags.HasAngle;
            _vecs[i] = default;
            _angles[i] = torque;
        }

        public void EnqueueTeleport(PhysicsHandle h, Vec2 position, float angle = 0f)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.Teleport;
            _flags[i] = PhysFlags.HasPos | PhysFlags.HasAngle;
            _vecs[i] = position;
            _angles[i] = angle;
        }

        public void EnqueueSyncEcsToPhys(PhysicsHandle h)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.SyncEcsToPhys;
            _flags[i] = PhysFlags.None;
            _vecs[i] = default;
            _angles[i] = 0f;
        }

        public void EnqueueSyncPhysToEcs(PhysicsHandle h)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.SyncPhysToEcs;
            _flags[i] = PhysFlags.None;
            _vecs[i] = default;
            _angles[i] = 0f;
        }

        public void EnqueueSetTransform(PhysicsHandle h, Vec2 position, float angle)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.SetTransform;
            _flags[i] = PhysFlags.HasPos | PhysFlags.HasAngle;
            _vecs[i] = position;
            _angles[i] = angle;
        }

        public void EnqueueSetLinearVelocity(PhysicsHandle h, Vec2 v)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.SetLinearVelocity;
            _flags[i] = PhysFlags.HasPos;
            _vecs[i] = v;
            _angles[i] = 0f;
        }

        public void EnqueueSetAngularVelocity(PhysicsHandle h, float w)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = PhysicsOpKind.SetAngularVelocity;
            _flags[i] = PhysFlags.HasAngle;
            _vecs[i] = default;
            _angles[i] = w;
        }

        public void Enqueue(PhysicsHandle h, PhysicsOpKind kind, Vec2 vec, float angle = 0f, PhysFlags flags = PhysFlags.None)
        {
            EnsureCapacity(Count + 1);
            int i = Count++;
            _handles[i] = h;
            _kinds[i] = kind;
            _flags[i] = flags;
            _vecs[i] = vec;
            _angles[i] = angle;
        }

        public void EnsureCapacity(int min)
        {
            if (_handles.Length >= min) return;

            int newCap = _handles.Length * 2;
            if (newCap < min) newCap = min;

            Array.Resize(ref _handles, newCap);
            Array.Resize(ref _kinds, newCap);
            Array.Resize(ref _flags, newCap);
            Array.Resize(ref _vecs, newCap);
            Array.Resize(ref _angles, newCap);
        }

        public void ClearFrame() => Count = 0;
    }
}