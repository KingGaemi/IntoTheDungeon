using IntoTheDungeon.Core.Util.Physics;
namespace IntoTheDungeon.Core.Physics.Collision
{
    public interface ICollider
    {
        public int LayerMask { get; set; }
        public CollisionLayer Layer { get; set; }
    }

}

