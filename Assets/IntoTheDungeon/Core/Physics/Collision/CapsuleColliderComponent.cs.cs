using UnityEngine;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Core.Physics
{
    public sealed class CapsuleColliderComponent : IManagedComponent
    {
        public float Radius;

        public CapsuleCollider2D CapsuleCollider { get; }

        public CapsuleColliderComponent(CapsuleCollider2D collider)
        {
            if (collider == null)
                throw new System.ArgumentNullException(nameof(collider));
            
            CapsuleCollider = collider;
        }


    }
}
