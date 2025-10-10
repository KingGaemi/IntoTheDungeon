using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Collision.Components
{
    public struct BoxColliderComponent : IComponentData, ICollider
    {
        public uint LayerMask { get; set; }
        public CollisionLayer Layer { get; set; }
        public Vec2 Size;
        public bool IsTrigger { get; set; }
        public Vec2 Offset { get; set; }

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
