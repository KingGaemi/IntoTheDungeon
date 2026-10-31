using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 공격자가 가진 근접 공격 정의. 공격 Active 시점에 이 값으로 히트박스를 만든다.
    /// </summary>
    public struct MeleeAttackComponent : IComponentData
    {
        public Vec2 Offset;       // 오른쪽 기준, 공격자 바닥 위치로부터 (X 가로, Y 깊이)
        public Vec2 HalfExtents;
        public float ActiveTime;  // 히트박스 유지 시간(초)
        public float KnockbackSpeed;  // 맞은 쪽을 공격 방향으로 밀어내는 초기 속도
        public float LaunchSpeed;     // 맞은 쪽을 위로 띄우는 초기 속도(0이면 안 띄움)

        public static readonly MeleeAttackComponent Default = new()
        {
            Offset = new Vec2(0.5f, 0f),
            HalfExtents = new Vec2(0.4f, 0.25f),   // Y = 깊이 반두께
            ActiveTime = 0.1f,
            KnockbackSpeed = 3f,   // 약 0.3 유닛 밀림
            LaunchSpeed = 5f       // 약 0.4 유닛 뜨고 0.33초 체공
        };
    }
}
