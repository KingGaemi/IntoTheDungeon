using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Physics.Components
{
    /// <summary>
    /// 피격 등으로 받은 바닥 평면 밀림 속도. 스스로 걷는 속도(KinematicComponent)와 따로 더해지고 빠르게 줄어든다.
    /// </summary>
    public struct KnockbackComponent : IComponentData
    {
        public Vec2 Velocity;     // 바닥 평면(X 가로, Y 깊이)
        public float Damping;     // 클수록 빨리 멈춘다. 총 밀림 거리 ≈ 초기 속도 / Damping

        public static readonly KnockbackComponent Default = new() { Velocity = Vec2.Zero, Damping = 10f };
    }
}
