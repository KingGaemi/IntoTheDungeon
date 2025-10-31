using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Physics.Abstractions;

namespace IntoTheDungeon.Features.Unity
{
    public interface IEntityRoot
    {
        Entity Entity { set; get; }
        UnityEngine.Transform Transform { get; }
        UnityEngine.Rigidbody2D Rigidbody { get; }
        UnityEngine.Collider2D Collider { get; }
        PhysicsHandle PhysicsHandle { get; set; }
    }
}