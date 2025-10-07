using UnityEngine;

namespace IntoTheDungeon.Core.Physics.Collision
{
    public struct BoxColliderComponent : ColliderComponent
    {
        public float Radius;

        public BoxCollider2D BoxCollider { get; }

       

    }
}
