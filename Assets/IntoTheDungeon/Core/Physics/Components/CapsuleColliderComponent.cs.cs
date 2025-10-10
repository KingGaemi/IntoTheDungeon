using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;


namespace IntoTheDungeon.Core.Physics.Collision.Components
{
    public sealed class CapsuleColliderComponent : IComponentData, ICollider
    {
        public uint LayerMask { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public CollisionLayer Layer { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public bool IsTrigger { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public Vec2 Offset { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public PhysicsUtility.AABB GetAABB(Vec2 entityPos)
        {
            throw new System.NotImplementedException();
        }
    }
}
