using IntoTheDungeon.Core.Util.Physics;
namespace IntoTheDungeon.Core.Physics.Collision
{
    public struct Collider
    {
        public int LayerMask;
        public CollisionLayer Layer;
    }




    public struct BoxCollider : Collider
    {
        public Vec2 Size;      
        public float Width;
        public float Height;

    }
}

