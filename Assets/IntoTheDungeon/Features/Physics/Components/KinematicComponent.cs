using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Physics.Components
{
    public struct KinematicComponent : IComponentData 
    {
        public Vec2 Direction; // 단위 벡터
        public float Magnitude;   // 속력(스칼라)
        public readonly Vec2 Velocity => Direction * Magnitude;

        

    }
}