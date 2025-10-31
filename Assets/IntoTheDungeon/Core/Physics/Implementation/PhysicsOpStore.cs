using System;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;
namespace IntoTheDungeon.Core.Physics.Implementation
{
    public sealed class PhysicsOpStore : IPhysicsOpStore, IPhysicsCommandStore, IPhysicsFeedbackStore
    {
        public PhysicsHandle[] Handles;
        public Vec2[] Vec;
        public float[] Angle;
        public PhysFlags[] Flags;
        public PhysicsOpKind[] Kinds;
        public int Count { get; set; }
        ReadOnlySpan<PhysicsHandle> IPhysicsOpStore.Handles => new(Handles, 0, Count);
        ReadOnlySpan<PhysicsOpKind> IPhysicsOpStore.Kinds => new(Kinds, 0, Count);
        ReadOnlySpan<PhysFlags> IPhysicsOpStore.Flags => new(Flags, 0, Count);
        ReadOnlySpan<Vec2> IPhysicsOpStore.Vecs => new(Vec, 0, Count);
        ReadOnlySpan<float> IPhysicsOpStore.Angles => new(Angle, 0, Count);

        public PhysicsOpStore(int cap = 1024)
        {
            Handles = new PhysicsHandle[cap];
            Vec = new Vec2[cap];
            Angle = new float[cap];
            Flags = new PhysFlags[cap];
            Kinds = new PhysicsOpKind[cap];
        }

        public int Push(PhysicsHandle h, PhysicsOpKind k, Vec2 v, float ang = 0, PhysFlags f = PhysFlags.None)
        {
            int i = Count++;
            Handles[i] = h; Kinds[i] = k; Vec[i] = v; Angle[i] = ang; Flags[i] = f;
            return i;
        }

        public void ClearFrame() => Count = 0;
    }
}