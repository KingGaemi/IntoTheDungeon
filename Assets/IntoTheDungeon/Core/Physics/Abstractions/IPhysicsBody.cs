using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    [System.Flags] public enum VelAxes { None = 0, X = 1, Y = 2, Both = X | Y }
    public enum VelMode { Set, Add }

    public struct PhysicsBodyRef : IComponentData { public PhysicsHandle Handle; } // 청크에 있음
    public interface IPhysicsBody
    {
        UnityEngine.Rigidbody2D Rb { get; }
    }
}