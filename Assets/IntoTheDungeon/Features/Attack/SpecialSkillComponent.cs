using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 필살기 정의 + 시전 상태. 플레이어가 버튼으로 직접 발동한다(자동전투 중에도).
    /// 시전하면 평타를 끊고 대시 공격 모션으로 앞으로 살짝 나가며, CastTime에 큰 검기를 날린다.
    /// </summary>
    public struct SpecialSkillComponent : IComponentData
    {
        // 정의
        public float Cooldown;          // 재사용 대기(초)
        public float CastTime;          // 시전 시작부터 검기가 나가는 시점(초)
        public float Duration;          // 시전 전체 길이(초). 대시 공격 클립 길이에 맞춘다
        public float DashSpeed;         // 시전 시작 때 앞으로 미는 속도(넉백 시스템으로 이동, 거리 ≈ 속도/10)
        public float DamageMultiplier;  // 검기 데미지 = 공격력 × 이 값
        public float WaveSpeed;         // 검기 비행 속도(유닛/초)
        public float WaveLifetime;      // 검기 유지 시간(초). 사거리 ≈ 속도 × 시간
        public Vec2 WaveHalfExtents;    // 검기 판정(X 가로 반폭, Y 깊이 반두께)
        public float WaveKnockback;
        public float WaveLaunch;

        // 시전 상태
        public bool Requested;
        public float RequestAge;
        public bool Casting;
        public float Timer;
        public bool Fired;
        public float CooldownRemaining;

        public readonly bool IsReady => CooldownRemaining <= 0f && !Casting;
        public readonly float CooldownRatio => Cooldown > 0f ? CooldownRemaining / Cooldown : 0f;   // 1 막 씀 ~ 0 준비됨

        public void Request() { Requested = true; RequestAge = 0f; }

        // 케서린: 대시 공격 + 큰 검기 한 방
        public static readonly SpecialSkillComponent SwordWave = new()
        {
            Cooldown = 6f,
            CastTime = 0.3f,
            Duration = 0.7f,                 // Catherine Dash_Attack.anim 길이
            DashSpeed = 6f,
            DamageMultiplier = 5f,
            WaveSpeed = 10f,
            WaveLifetime = 0.6f,             // 사거리 약 6
            WaveHalfExtents = new Vec2(0.6f, 0.5f),
            WaveKnockback = 7f,
            WaveLaunch = 4f
        };
    }

    /// <summary>날아가는 히트박스(검기·화살 등). ProjectileMotionSystem이 Hitbox.Center를 옮긴다.</summary>
    public struct ProjectileComponent : IComponentData
    {
        public Vec2 Velocity;   // 바닥 평면(X 가로, Y 깊이)
    }

    /// <summary>검기 표시용 표식. 그림은 Unity 쪽 SwordWaveRenderer가 그린다.</summary>
    public struct SwordWaveComponent : IComponentData
    {
        public float TotalLifetime;   // 사라지면서 옅어지는 비율 계산용
    }
}
