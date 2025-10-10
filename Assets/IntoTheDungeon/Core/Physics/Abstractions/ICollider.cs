using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface ICollider
    {
        uint LayerMask { get; set; }
        CollisionLayer Layer { get; set; }
        bool IsTrigger { get; set; }
        Vec2 Offset { get; set; }

        PhysicsUtility.AABB GetAABB(Vec2 entityPos);
    }

}

