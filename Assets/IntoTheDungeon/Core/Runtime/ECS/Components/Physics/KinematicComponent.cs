using IntoTheDungeon.Core.Util.Physics;

namespace IntoTheDungeon.Core.ECS.Components.Physics
{
    public struct KinematicComponent : IComponentData 
    {
        public Vec2 Direction; // 단위 벡터
        public float Magnitude;   // 속력(스칼라)
        public readonly Vec2 Velocity => Direction * Magnitude;

        

    }
}