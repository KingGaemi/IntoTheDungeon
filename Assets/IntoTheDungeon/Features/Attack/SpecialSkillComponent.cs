using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 필살기 정의 + 시전 상태. 파티원별 버튼으로 직접 발동한다.
    /// 시전하면 평타를 끊고 필살기 모션(컨트롤러의 대시 공격 상태)으로 앞으로 살짝 나가며, CastTime에 큰 투사체를 날린다.
    /// 캐릭터마다 프리셋이 다르다(케서린 검기, 궁수 강화 화살).
    /// </summary>
    public struct SpecialSkillComponent : IComponentData
    {
        // 정의
        public float Cooldown;          // 재사용 대기(초)
        public float ManaCost;          // 시전할 때 쓰는 마나(재사용 대기도 끝나 있어야 함)
        public float CastTime;          // 시전 시작부터 투사체가 나가는 시점(초)
        public float Duration;          // 시전 전체 길이(초). 필살기 클립 길이에 맞춘다
        public float DashSpeed;         // 시전 시작 때 앞으로 미는 속도(넉백 시스템으로 이동, 거리 ≈ 속도/10)
        public float DamageMultiplier;  // 투사체 데미지 = 공격력 × 이 값
        public float WaveSpeed;         // 투사체 비행 속도(유닛/초)
        public float WaveLifetime;      // 투사체 유지 시간(초). 사거리 ≈ 속도 × 시간
        public Vec2 WaveHalfExtents;    // 투사체 판정(X 가로 반폭, Y 깊이 반두께)
        public float WaveKnockback;
        public float WaveLaunch;
        public float WaveHitStun;       // 맞은 쪽 경직(초). 필살기는 크게
        public ProjectileVisual Visual; // 투사체 그림

        // 도발 + 방패(TauntRadius > 0이면 투사체 대신 이것: TauntSystem)
        public float TauntRadius;       // 이 가로 반경(깊이는 절반) 안의 적이 시전자만 노린다
        public float TauntDuration;     // 도발 유지(초)
        public float GuardDuration;     // 방패 유지(초)
        public float GuardDamageMul;    // 방패 동안 받는 피해 배율

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
            Cooldown = 20f,                  // 평타 비중을 높이려고 길게(필살기는 노리는 순간에)
            CooldownRemaining = 8f,          // 전투 시작하자마자 쓰지 않게(첫 필살기는 8초 뒤)
            ManaCost = 60f,                  // 케서린 마나 70·초당 1.6 → 다시 찰 때까지 대략 40초
            CastTime = 0.3f,
            Duration = 0.7f,                 // Catherine Dash_Attack.anim 길이
            DashSpeed = 6f,
            DamageMultiplier = 4f,           // 기본 공격력이 오른 만큼 낮춤(한 방 피해는 예전과 비슷)
            WaveSpeed = 10f,
            WaveLifetime = 0.6f,             // 사거리 약 6
            WaveHalfExtents = new Vec2(0.6f, 0.5f),
            WaveKnockback = 7f,
            WaveLaunch = 4f,
            WaveHitStun = 0.8f,
            Visual = ProjectileVisual.SwordWave
        };

        // 중갑 전사: 칼을 치켜들고 외쳐 주변 적을 끌어모으고, 방패를 세워 받는 피해를 줄인다
        public static readonly SpecialSkillComponent TauntGuard = new()
        {
            Cooldown = 14f,                  // 오크 강타를 끊는 게 핵심이라 다른 필살기보다 자주
            CooldownRemaining = 1.5f,        // 전투 시작하자마자 첫 강타를 끊을 수 있게
            ManaCost = 25f,                  // 전사 마나 60·초당 1.3 → 약 19초마다(재사용 대기와 비슷)
            CastTime = 0.25f,
            Duration = 0.75f,                // Warrior_Taunt(칼 들기) 동안 멈춤
            TauntRadius = 4.5f,
            TauntDuration = 5f,
            GuardDuration = 6f,
            GuardDamageMul = 0.5f
        };

        // 궁수: 제자리에서 빠르게 시위를 당겨 한 줄을 꿰뚫는 강화 화살(검기보다 가늘고 멀리)
        public static readonly SpecialSkillComponent PowerShot = new()
        {
            Cooldown = 20f,                  // 평타 비중을 높이려고 길게(필살기는 노리는 순간에)
            CooldownRemaining = 8f,          // 전투 시작하자마자 쓰지 않게
            ManaCost = 60f,                  // 궁수 마나 80·초당 1.9 → 대략 30초
            CastTime = 0.58f,                // Archer_Skill(24fps) 14번째 프레임 = 시위를 놓는 순간
            Duration = 0.71f,                // Archer_Skill 길이(17프레임/24fps)
            DashSpeed = 0f,
            DamageMultiplier = 3f,           // 기본 공격력이 오른 만큼 낮춤
            WaveSpeed = 16f,
            WaveLifetime = 0.7f,             // 사거리 약 11
            WaveHalfExtents = new Vec2(0.5f, 0.3f),
            WaveKnockback = 5f,
            WaveLaunch = 0f,
            WaveHitStun = 0.6f,
            Visual = ProjectileVisual.PowerArrow
        };
    }

    public enum ProjectileVisual : byte { SwordWave, Arrow, PowerArrow }

    /// <summary>날아가는 히트박스(검기·화살 등). ProjectileMotionSystem이 Hitbox.Center를 옮긴다.</summary>
    public struct ProjectileComponent : IComponentData
    {
        public Vec2 Velocity;       // 바닥 평면(X 가로, Y 깊이)
        public bool DestroyOnHit;   // 첫 대상에 맞으면 사라짐(일반 화살). false면 관통
    }

    /// <summary>투사체 표시용. 그림은 Unity 쪽 ProjectileRenderer가 그린다.</summary>
    public struct ProjectileVisualComponent : IComponentData
    {
        public ProjectileVisual Kind;
        public float TotalLifetime;   // 사라지면서 옅어지는 비율 계산용
    }
}
