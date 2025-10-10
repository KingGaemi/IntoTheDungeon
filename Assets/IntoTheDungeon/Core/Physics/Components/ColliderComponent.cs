using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Collision.Components
{
    public struct ColliderComponent : IComponentData, ICollider
    {
        public uint LayerMask { get; set; }
        public CollisionLayer Layer { get; set; }
        bool ICollider.IsTrigger { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        Vec2 ICollider.Offset { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public Vec2 Offset;
        public bool IsTrigger;

        public PhysicsUtility.AABB GetAABB(Vec2 entityPos)
        {
            throw new System.NotImplementedException();
        }
    }
}
