using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    [System.Flags] public enum VelAxes { None=0, X=1, Y=2, Both=X|Y }
    public enum VelMode { Set, Add }

    public struct PhysicsBodyRef : IComponentData { public int Handle; } // 청크에 있음
    public struct PhysicsCommand : IComponentData {
        public bool HasVel;
        public VelAxes Axes;   // X만, Y만, 둘다
        public VelMode Mode; 
        public Util.Vec2 V;
    } // 청크에 있음

    public interface IPhysicsBody
    {
        void SetLinearVelocity(float x, float y);
        (float x, float y) GetLinearVelocity();
    }
}