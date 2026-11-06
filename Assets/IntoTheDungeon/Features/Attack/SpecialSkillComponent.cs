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
        public int StopPower;           // 저지력: 이 한 방이 강타·내려찍기의 요구 저지력과 같거나 높아야 끊긴다(= 기본 + 장비 보너스)
        public int StopPowerBase;       // 기본 저지력(필살기는 대부분 10)

        // 도발 + 방패(TauntRadius > 0이면 투사체 대신 이것: TauntSystem)
        public float TauntRadius;       // 이 가로 반경(깊이는 절반) 안의 적이 시전자만 노린다
        public float TauntDuration;     // 도발 유지(초)
        public float GuardDuration;     // 방패 유지(초)
        public float GuardDamageMul;    // 방패 동안 받는 피해 배율
        public float GuardDurationBase, GuardMulBase;   // 유물·특성 보정 전 값(다시 계산할 때 기준)
        public bool PartyGuard;         // 철벽: 도발 없이 반경 안 파티원 전원에게 방패

        // 필살기 갈래(교체 필살기·유물·특성)
        public bool Centered;           // 몸 한가운데에 판정(회오리 베기)
        public float Offset;            // 몸 앞 이 거리에 판정(화살비). 0이면 몸 바로 앞에서 날아감
        public bool Magic, MagicBase;   // 마법 피해(마법 보호막 관통, 마법 공격력이 더해짐)
        public float DamageMulBase;     // 유물·특성 보정 전 피해 배율

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
            StopPower = PatternBreak.DefaultStopPower, StopPowerBase = PatternBreak.DefaultStopPower,
            Cooldown = 12f,                  // 20이면 초반에 누를 게 없어 지루함 → 12
            CooldownRemaining = 0f,          // 방마다 준비된 채로 시작(첫 강타부터 끊을 기회)
            ManaCost = 50f,                  // 케서린 마나 70: 필살기 몫(50)을 남기고도 기본 스킬(12)을 쓸 수 있게 60 → 50
            CastTime = 0.3f,
            Duration = 0.7f,                 // Catherine Dash_Attack.anim 길이
            DashSpeed = 6f,
            DamageMultiplier = 4f,           // 기본 공격력이 오른 만큼 낮춤(한 방 피해는 예전과 비슷)
            WaveSpeed = 10f,
            WaveLifetime = 0.6f,             // 사거리 약 6
            WaveHalfExtents = new Vec2(0.6f, 1.1f),   // 깊이(위아래)를 넓게: 줄이 조금 어긋난 적도 맞게(0.5 → 1.1)
            WaveKnockback = 7f,
            WaveLaunch = 4f,
            WaveHitStun = 0.8f,
            Visual = ProjectileVisual.SwordWave
        };

        // 중갑 전사: 칼을 치켜들고 외쳐 주변 적을 끌어모으고, 방패를 세워 받는 피해를 줄인다
        public static readonly SpecialSkillComponent TauntGuard = new()
        {
            StopPower = PatternBreak.DefaultStopPower, StopPowerBase = PatternBreak.DefaultStopPower,
            Cooldown = 10f,                  // 오크 강타·거미 떼를 끄는 게 핵심이라 다른 필살기보다 자주
            CooldownRemaining = 0f,          // 방마다 준비된 채로 시작(첫 강타부터 끊을 기회)
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
            StopPower = PatternBreak.DefaultStopPower, StopPowerBase = PatternBreak.DefaultStopPower,
            Cooldown = 12f,                  // 20이면 초반에 누를 게 없어 지루함 → 12
            CooldownRemaining = 0f,          // 방마다 준비된 채로 시작(첫 강타부터 끊을 기회)
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

        // ───────── 교체 필살기(레벨업 특성): 저지력 대신 다른 답 ─────────

        // 케서린: 제자리에서 크게 돌며 주변을 벤다(떼 몹·알집 정리)
        public static readonly SpecialSkillComponent Whirlwind = new()
        {
            Cooldown = 10f, ManaCost = 40f,
            CastTime = 0.3f, Duration = 0.7f,
            DamageMultiplier = 5.5f,
            WaveSpeed = 0f, WaveLifetime = 0.2f,
            WaveHalfExtents = new Vec2(2.3f, 1.2f),
            WaveKnockback = 6f, WaveLaunch = 3f, WaveHitStun = 0.5f,
            Visual = ProjectileVisual.SwordWave,
            Centered = true
        };

        // 궁수: 앞쪽 넓은 땅에 마력 화살을 쏟는다(마법 피해 → 보호막 관통)
        public static readonly SpecialSkillComponent ArrowRain = new()
        {
            Cooldown = 11f, ManaCost = 55f,
            CastTime = 0.58f, Duration = 0.71f,
            DamageMultiplier = 3.2f,
            WaveSpeed = 0f, WaveLifetime = 0.35f,
            WaveHalfExtents = new Vec2(2.4f, 1.3f),
            Offset = 5f,
            WaveKnockback = 1f, WaveHitStun = 0.3f,
            Visual = ProjectileVisual.Orb,
            Magic = true, MagicBase = true
        };

        // 중갑 전사: 방패를 세우고 외쳐 주변 파티원 전원에게 방패(강타·돌진을 받아낸다)
        public static readonly SpecialSkillComponent Bulwark = new()
        {
            Cooldown = 14f, ManaCost = 30f,
            CastTime = 0.25f, Duration = 0.75f,
            TauntRadius = 6f,
            GuardDuration = 4f, GuardDamageMul = 0.4f, GuardDurationBase = 4f, GuardMulBase = 0.4f,
            PartyGuard = true
        };

        /// <summary>프리셋 → 필살기(보정 전 기준값을 채워서).</summary>
        public static SpecialSkillComponent Of(Character.SkillPreset preset)
        {
            var sk = preset switch
            {
                Character.SkillPreset.PowerShot => PowerShot,
                Character.SkillPreset.TauntGuard => TauntGuard,
                Character.SkillPreset.Whirlwind => Whirlwind,
                Character.SkillPreset.ArrowRain => ArrowRain,
                Character.SkillPreset.Bulwark => Bulwark,
                _ => SwordWave
            };
            sk.DamageMulBase = sk.DamageMultiplier;
            if (sk.GuardDurationBase <= 0f) { sk.GuardDurationBase = sk.GuardDuration; sk.GuardMulBase = sk.GuardDamageMul; }
            return sk;
        }
    }

    public enum ProjectileVisual : byte { SwordWave, Arrow, PowerArrow, Orb }

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
