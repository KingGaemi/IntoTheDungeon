using UnityEngine;
using IntoTheDungeon.Core.ECS.Components;


namespace IntoTheDungeon.Core.Runtime.Physics
{
    public sealed class UnityPhysicsBody : IManagedComponent
    {
        public Rigidbody2D Rb { get; }
        public UnityPhysicsBody(Rigidbody2D rb)
        {
            if (rb == null)
                throw new System.ArgumentNullException(nameof(rb));

            Rb = rb;
        }


        // public void SetLinearVelocity(float x, float y)
        //     => _rb.linearVelocity = new Vector2(x, y);

        // public (float x, float y) GetLinearVelocity()
        //     => (_rb.linearVelocity.x, _rb.linearVelocity.y);
    }
}