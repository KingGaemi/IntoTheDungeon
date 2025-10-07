using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Util.Physics;

namespace IntoTheDungeon.Core.Physics.Collision
{
    public struct BoxColliderComponent : IComponentData, ICollider
    {
        public int LayerMask { get; set; }
        public CollisionLayer Layer { get; set; }
        public Vec2 Size;
        public Vec2 Offset;
        public bool IsTrigger;

        // 중복 데이터 제거: Width/Height는 계산 프로퍼티로
        public readonly float Width => Size.X;
        public readonly float Height => Size.Y;

        // AABB 계산 헬퍼
        public readonly PhysicsUtility.AABB GetAABB(Vec2 entityPosition)
        {
            Vec2 center = entityPosition + Offset;
            return PhysicsUtility.AABB.FromCenterSize(center, Size);
        }
    }
}
