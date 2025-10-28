
using System;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface IPhysicsOpStore
    {
        public int Count { get; set; }
        int Push(PhysicsHandle h, PhysicsOpKind k, Vec2 v, float ang = 0, PhysFlags f = PhysFlags.None);
        ReadOnlySpan<PhysicsHandle> Handles { get; }
        ReadOnlySpan<PhysicsOpKind> Kinds { get; }
        ReadOnlySpan<PhysFlags> Flags { get; }
        ReadOnlySpan<Vec2> Vecs { get; }
        ReadOnlySpan<float> Angles { get; }
        void ClearFrame();
    }
}
