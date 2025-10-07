using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Core.Physics.Collision
{
    public struct ColliderComponent : IComponentData
    {
        public int LayerMask;
        public CollisionLayer Layer;
        



        
    }
}
