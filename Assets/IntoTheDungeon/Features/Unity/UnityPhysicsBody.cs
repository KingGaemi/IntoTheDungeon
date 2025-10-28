using UnityEngine;
using IntoTheDungeon.Core.Physics.Abstractions;


namespace IntoTheDungeon.Core.Runtime.Physics
{
    public sealed class UnityPhysicsBody : IPhysicsBody
    {
        public Rigidbody2D Rb { get; }
        public UnityPhysicsBody(Rigidbody2D rb)
        {
            Rb = rb;
        }
    }
}