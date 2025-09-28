using UnityEngine;
using IntoTheDungeon.Core.Abstractions.Physics;


namespace IntoTheDungeon.Core.Runtime.Physics
{   
    public sealed class UnityPhysicsBody : IPhysicsBody
    {
        private readonly Rigidbody2D _rb;
        public UnityPhysicsBody(Rigidbody2D rb) => _rb = rb;

        public void SetLinearVelocity(float x, float y)
            => _rb.linearVelocity = new Vector2(x, y);

        public (float x, float y) GetLinearVelocity()
            => (_rb.linearVelocity.x, _rb.linearVelocity.y);
    }
}