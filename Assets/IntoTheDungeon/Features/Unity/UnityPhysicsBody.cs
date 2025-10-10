using UnityEngine;
using IntoTheDungeon.Core.Physics.Abstractions;


namespace IntoTheDungeon.Core.Runtime.Physics
{
    public sealed class UnityPhysicsBody : IPhysicsBody
    {
        public Rigidbody2D Rb { get;}
        public UnityPhysicsBody(Rigidbody2D rb)
        {
            Rb = rb;
        }

        public void SetLinearVelocity(float x, float y)
            => Rb.linearVelocity = new Vector2(x, y);

        public (float x, float y) GetLinearVelocity()
            => (Rb.linearVelocity.x, Rb.linearVelocity.y);
    }
}