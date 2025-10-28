using IntoTheDungeon.Core.Physics.Abstractions;
using UnityEngine;

namespace IntoTheDungeon.Unity.Bridge.Physics.Abstractions
{
    public interface IPhysicsPort
    {
        bool TryGetRigidbody(PhysicsHandle h, out Rigidbody2D rb);
        void AdoptBody(PhysicsHandle h, Rigidbody2D rb, Transform newRoot);
    }

}