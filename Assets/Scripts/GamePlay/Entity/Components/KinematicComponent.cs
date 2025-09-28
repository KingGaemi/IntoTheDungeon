using IntoTheDungeon.Core.Abstractions.Physics;
using IntoTheDungeon.Core.ECS;

public sealed class KinematicComponent : IComponent
{
    public Vec2 Direction; // 단위 벡터
    public float Magnitude;   // 속력(스칼라)
    public Vec2 Velocity => Direction * Magnitude;

}