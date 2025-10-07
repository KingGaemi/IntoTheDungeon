namespace IntoTheDungeon.Core.Physics.Collision
{
    public struct CircleColliderComponent : ColliderComponent
    {
       
        
        public float Radius;

        public CircleCollider2D CircleCollider { get; }

        public CircleColliderComponent(CircleCollider2D collider)
        {
            if (collider == null)
                throw new System.ArgumentNullException(nameof(collider));
            
            CircleCollider = collider;
        }


    }
}
