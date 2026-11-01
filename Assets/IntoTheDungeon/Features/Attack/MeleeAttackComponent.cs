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
        public float HitStun;         // 맞은 쪽 경직(초). 0이면 경직 없음

        public static readonly MeleeAttackComponent Default = new()
        {
            // 판정은 AI가 공격을 시작하는 거리(1.0)보다 넉넉하게: 몸 앞 0.05~1.25 + 상대 몸통 0.3 → 약 1.55까지 닿는다.
            // 휘두르는 사이 다른 공격에 조금 밀려도 헛치지 않게 하는 여유(플레이어 눈엔 "잘 맞는다"로 보임)
            Offset = new Vec2(0.65f, 0f),
            HalfExtents = new Vec2(0.6f, 0.25f),   // Y = 깊이 반두께
            ActiveTime = 0.1f,
            KnockbackSpeed = 1f,   // 평타는 약 0.1 유닛만 살짝(큰 넉백은 넉백을 주는 공격에만, 경직과 같은 정책)
            LaunchSpeed = 2f,      // 평타: 약 0.07 유닛 들썩(0.13초). 띄우기 전용 공격은 따로 크게
            HitStun = 0f           // 평타는 경직 없음(경직은 경직을 주는 공격에만)
        };
    }
}
