using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>도발당함: Remaining 동안 Taunter만 노린다(CombatAISystem).</summary>
    public struct TauntedComponent : IComponentData
    {
        public Entity Taunter;
        public float Remaining;
    }

    /// <summary>방패 막기: Remaining 동안 받는 피해 × DamageMul(HitDetectSystem). Elapsed·Radius는 연출용.</summary>
    public struct GuardComponent : IComponentData
    {
        public float Remaining, Duration, Elapsed;
        public float DamageMul;
        public float Radius;   // 도발이 닿은 범위(시전 순간 퍼지는 고리)
    }

    /// <summary>
    /// 전사 필살기(도발 + 방패): 주변 적이 몇 초간 전사만 노리고, 그동안 전사가 받는 피해가 줄어든다.
    /// SpecialSkillSystem이 발동 순간 Apply를 부르고, 이 시스템은 남은 시간을 줄인다.
    /// </summary>
    public sealed class TauntSystem : GameSystem, ITick
    {
        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            foreach (var chunk in em.GetChunks(typeof(TauntedComponent)))
            {
                var taunts = chunk.GetComponentArray<TauntedComponent>();
                for (int i = 0; i < chunk.Count; i++)
                    if (taunts[i].Remaining > 0f) taunts[i].Remaining -= dt;
            }
            foreach (var chunk in em.GetChunks(typeof(GuardComponent)))
            {
                var guards = chunk.GetComponentArray<GuardComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (guards[i].Remaining > 0f) guards[i].Remaining -= dt;
                    guards[i].Elapsed += dt;
                }
            }
        }

        static readonly List<Entity> s_hits = new(16);

        /// <summary>철벽: 도발 없이 반경 안(깊이는 절반) 같은 편 살아 있는 전원에게 방패.</summary>
        static void PartyGuard(IEntityManager em, Entity owner, TeamFlag team, Vec2 pos, in SpecialSkillComponent sk)
        {
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != team || !statuses[i].IsAlive) continue;
                    var d = transforms[i].Position - pos;
                    if (System.MathF.Abs(d.X) <= sk.TauntRadius && System.MathF.Abs(d.Y) <= sk.TauntRadius * 0.5f) s_hits.Add(entities[i]);
                }
            }
            var guard = new GuardComponent { Remaining = sk.GuardDuration, Duration = sk.GuardDuration, DamageMul = sk.GuardDamageMul, Radius = 1f };
            foreach (var e in s_hits)
            {
                if (em.HasComponent<GuardComponent>(e)) em.GetComponent<GuardComponent>(e) = guard;
                else em.AddComponent(e, guard);
            }
        }

        /// <summary>도발 발동: 반경 안(깊이는 절반)의 살아 있는 적에게 도발을 걸고, 시전자에게 방패를 건다.</summary>
        public static void Apply(IEntityManager em, Entity owner, TeamFlag team, Vec2 pos, SpecialSkillComponent sk)
        {
            s_hits.Clear();
            if (CombatPerks.Mine(em, owner)) sk.TauntRadius *= CombatPerks.TauntRadiusMul;   // 유물(뿔피리)
            if (sk.PartyGuard) { PartyGuard(em, owner, team, pos, sk); return; }
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag == team || teams[i].TeamFlag == TeamFlag.None) continue;
                    var d = transforms[i].Position - pos;
                    if (!statuses[i].IsAlive)
                    {
                        // 무너진 해골(뼈 무더기)도 도발 범위 안이면 저지력으로 부술 수 있다
                        if (Battle.Revive.IsDowned(em, entities[i]) && System.MathF.Abs(d.X) <= sk.TauntRadius && System.MathF.Abs(d.Y) <= sk.TauntRadius * 0.5f)
                            Battle.Revive.ShatterRequests.Add((entities[i], sk.StopPower));
                        continue;
                    }
                    if (System.MathF.Abs(d.X) <= sk.TauntRadius && System.MathF.Abs(d.Y) <= sk.TauntRadius * 0.5f) s_hits.Add(entities[i]);
                }
            }

            // 청크 순회가 끝난 뒤 붙인다(구조 변경)
            var taunt = new TauntedComponent { Taunter = owner, Remaining = sk.TauntDuration };
            foreach (var e in s_hits)
            {
                if (em.HasComponent<TauntedComponent>(e)) em.GetComponent<TauntedComponent>(e) = taunt;
                else em.AddComponent(e, taunt);
                PatternBreak.Apply(em, e, sk.StopPower, flinch: true, pushDir: em.GetComponent<TransformComponent>(e).Position.X >= pos.X ? 1f : -1f, source: owner);
            }

            var guard = new GuardComponent { Remaining = sk.GuardDuration, Duration = sk.GuardDuration, DamageMul = sk.GuardDamageMul, Radius = sk.TauntRadius };
            if (em.HasComponent<GuardComponent>(owner)) em.GetComponent<GuardComponent>(owner) = guard;
            else em.AddComponent(owner, guard);
        }
    }

    /// <summary>
    /// 저지(필살기 공용: 전사 도발·케서린 검기·궁수 강화 화살). 필살기마다 저지력(대부분 10)이 있고,
    /// 강타·내려찍기 패턴은 요구 저지력이 있다. 한 방의 저지력이 요구치와 같거나 높아야 패턴 취소 + 그로기(넘은 만큼 길게)
    /// (여러 번 맞혀도 합산하지 않음, 모자라면 「저지력 부족 10/15」만). 휘두르려던 평타는 저지력이 있으면 끊고 경직.
    /// 저지력은 장비(망치·철퇴 등)로 올린다 → 성능이 조금 낮아도 저지력 높은 장비·동료를 고를 이유가 생긴다.
    /// CC 저항·슈퍼아머를 무시한다(HitStunComponent.PendingBreak). 결과는 InterruptFeed에 남겨 화면이 연출한다.
    /// </summary>
    public static class PatternBreak
    {
        public const float PatternBreakStun = 2f, AttackBreakStun = 0.8f, FlinchStun = 0.35f;
        // 패턴 파괴 그로기는 저지력이 요구치를 넘은 만큼 길어진다: 기본 2초 + 1 넘을 때마다 0.15초(최대 5초)
        public const float StunPerExcess = 0.15f, MaxPatternBreakStun = 5f;

        /// <summary>패턴을 깼을 때 그로기 시간(저지력 power, 요구치 required).</summary>
        public static float PatternStun(int power, int required)
            => System.MathF.Min(MaxPatternBreakStun, PatternBreakStun + StunPerExcess * System.Math.Max(0, power - required));
        public const int DefaultStopPower = 10;
        public const float BreakCooldownRefund = 0.5f;   // 패턴을 끊으면 그 필살기 재사용 대기를 이만큼 돌려받는다(메아리 종이면 전부)
        public const float GroggyDamageMul = 1.5f;       // 기절(그로기)한 적이 받는 피해 배율(처형인의 도끼로 더 오름)

        public const float AttackBreakPush = 8f, AttackBreakLaunch = 2.5f;

        /// <param name="pushDir">밀어낼 방향(+1 오른쪽, -1 왼쪽, 0 = 밀지 않음)</param>
        public static InterruptKind Apply(IEntityManager em, Entity e, int power, bool flinch, float pushDir = 0f, Entity source = default)
        {
            if (power <= 0 || !em.HasComponent<HitStunComponent>(e) || !em.TryGetComponent<StateComponent>(e, out var state)) return InterruptKind.Flinch;
            var kind = InterruptKind.Flinch;
            float stun = flinch ? FlinchStun : 0f;
            int required = 0;

            if (em.HasComponent<Battle.BossSlamComponent>(e))
            {
                ref var slam = ref em.GetComponent<Battle.BossSlamComponent>(e);
                if (slam.Charging && !slam.Fired)
                {
                    required = slam.StopRequired;
                    if (power >= slam.StopRequired)   // 한 방 기준(합산 없음)
                    {
                        slam.Charging = false;
                        slam.Remaining = slam.Cooldown;   // 다시 쓰려면 재사용 대기를 처음부터
                        kind = InterruptKind.PatternBroken;
                        stun = PatternStun(power, slam.StopRequired);   // 저지력이 남을수록 오래 기절
                        RewardBreaker(em, source);
                    }
                    else kind = InterruptKind.StopFailed;   // 저지력 부족: 패턴은 계속(경직도 안 걸림)
                }
            }
            if (kind == InterruptKind.StopFailed)
            {
                Feed(em, e, kind, power, required, source);
                return kind;
            }
            if (kind == InterruptKind.Flinch && state.Current.Action == ActionState.Attack
                && em.TryGetComponent<ActionPhaseComponent>(e, out var phase) && phase.ActionPhase == ActionPhase.Windup)
            {
                kind = InterruptKind.AttackBroken;
                stun = AttackBreakStun;
                Push(em, e, pushDir);   // 평타 끊기는 「끊김!」 글자 없이 크게 밀려나는 느낌으로
            }

            ref var hs = ref em.GetComponent<HitStunComponent>(e);
            if (hs.PendingBreak < stun) hs.PendingBreak = stun;
            if (kind == InterruptKind.Flinch) return kind;
            Feed(em, e, kind, power, required, source);
            return kind;
        }

        // 끊은 쪽 보상: 그 필살기 재사용 대기 절반(메아리 종이면 전부 + 마나 절반)을 돌려받아 다음 패턴도 끊을 수 있게
        static void RewardBreaker(IEntityManager em, Entity source)
        {
            if (!em.Exists(source) || !em.HasComponent<SpecialSkillComponent>(source)) return;
            ref var sk = ref em.GetComponent<SpecialSkillComponent>(source);
            bool echo = CombatPerks.BreakEcho && CombatPerks.Mine(em, source);
            sk.CooldownRemaining *= echo ? 0f : 1f - BreakCooldownRefund;
            if (echo && em.HasComponent<DerivedStatsComponent>(source))
            {
                ref var ds = ref em.GetComponent<DerivedStatsComponent>(source);
                ds.Mana = System.MathF.Min(ds.MaxMana, ds.Mana + sk.ManaCost * 0.5f);
            }
        }

        // 평소 넉백보다 세게 밀고 살짝 띄운다(정신력 넉백 저항은 그대로 적용)
        static void Push(IEntityManager em, Entity e, float dir)
        {
            if (dir == 0f) return;
            float keep = em.TryGetComponent<DerivedStatsComponent>(e, out var d) ? 1f - d.KnockbackResist : 1f;
            if (em.HasComponent<KnockbackComponent>(e))
            {
                ref var k = ref em.GetComponent<KnockbackComponent>(e);
                float speed = AttackBreakPush * keep;
                if (System.MathF.Abs(k.Velocity.X) < speed) k.Velocity = new Vec2(System.MathF.Sign(dir) * speed, k.Velocity.Y);
            }
            if (em.HasComponent<HeightMotionComponent>(e))
            {
                ref var m = ref em.GetComponent<HeightMotionComponent>(e);
                float launch = AttackBreakLaunch * keep;
                if (m.VelocityZ < launch) m.VelocityZ = launch;
            }
        }

        /// <summary>연출 피드에 남긴다. source = 끊은 쪽(파티원, 없으면 default), Target = e.</summary>
        public static void Feed(IEntityManager em, Entity e, InterruptKind kind, int power, int required, Entity source = default)
        {
            var pos = em.GetComponent<TransformComponent>(e);
            float head = em.TryGetComponent<Physics.Components.BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
            InterruptFeed.Pending.Add(new InterruptInfo { Kind = kind, Position = pos.Position, Height = pos.Height + head, Power = power, Required = required, Source = source, Target = e });
        }
    }

    public enum InterruptKind : byte { Flinch, AttackBroken, PatternBroken, StopFailed, Shattered, Revived, Empowered, Shielded, Enraged }   // Shattered: 되살아나는 몹을 부숨, Revived: 다시 일어남, Empowered: 치유 의식으로 광폭, Enraged: 보스 2페이즈

    /// <summary>
    /// 파티 유물의 전투 효과(파티 공용). 방 시작 때 BattleFlowSystem이 판의 유물로 채운다.
    /// 숫자 보정(PartyModifiers)과 달리 싸우는 방식을 바꾸는 것들.
    /// </summary>
    public static class CombatPerks
    {
        /// <summary>유물 효과를 받는 내 파티원인가(슬롯이 있는 파티원만, 합류 전 동료는 아님).</summary>
        public static bool Mine(IEntityManager em, Entity e) => em.Exists(e) && em.HasComponent<Battle.PartySlotComponent>(e);

        public static float GroggyDamageAdd;     // 처형인의 도끼: 기절한 적에게 주는 피해 배율 +
        public static bool BreakEcho;            // 메아리 종: 끊으면 재사용 대기 전부 + 마나 절반 환급
        public static float CorpseBlast;         // 불씨 주머니: 쓰러진 적이 터져 주변 적에게 그 최대 체력 × 이 값
        public static float GuardReflect;        // 가시 방패: 방패 중 받은(막기 전) 피해 × 이 값을 때린 적에게
        public static float Lifesteal;           // 피의 성배: 입힌 피해 × 이 값 회복
        public static bool PierceArrows;         // 관통 깃촉: 평타 화살 관통
        public static float TauntRadiusMul = 1f; // 전쟁의 뿔피리: 도발 범위 배율

        public static float RotDamageAdd;        // 부패의 손길: 중독된 적이 받는 피해 +
        public static float PoisonBurst;         // 역병의 손가락: 중독된 적이 죽을 때 남은 독 피해 × 이 값을 주변 적에게

        public static void Set(IntoTheDungeon.Features.Run.PartyModifiers m)
        {
            PoisonBurst = m.PoisonBurst;
            RotDamageAdd = m.RotDamageAdd;
            GroggyDamageAdd = m.GroggyDamageAdd;
            BreakEcho = m.BreakEcho;
            CorpseBlast = m.CorpseBlast;
            GuardReflect = m.GuardReflect;
            Lifesteal = m.Lifesteal;
            PierceArrows = m.PierceArrows;
            TauntRadiusMul = m.TauntRadiusMul;
        }
    }

    /// <summary>
    /// 파티원 한 명의 빌드 효과(그 파티원 유물 + 장비 + 레벨업 특성). 방 시작·레벨 업 때 BattleFlowSystem이 채운다.
    /// 합류 전 동료에겐 없다(내 유물을 안 받음).
    /// </summary>
    public struct MemberPerkComponent : IComponentData
    {
        public float PoisonRatio, PoisonDuration;   // 맞힌 피해 × 비율이 초당 독으로
        public float KillCooldownCut;               // 적이 쓰러질 때마다 필살기 대기 -초
        public float Lifesteal;                     // 그 파티원만 흡혈(특성)
        public float KillManaAdd;                   // 적이 쓰러질 때 마나 +(영혼 흡입관·흡수)
        public float Thorns;                        // 근접으로 맞으면 맞힌 피해 × 이 값을 돌려줌(가시 갑주·가시 갑옷)
    }

    public struct InterruptInfo
    {
        public InterruptKind Kind;
        public Vec2 Position;   // 바닥 위치(가로, 깊이)
        public float Height;    // 발 높이 + 머리 높이
        public int Power, Required;   // 패턴 저지: 맞힌 저지력 / 요구 저지력
        public Entity Source;   // 끊은 쪽(파티원), 없으면 default
        public Entity Target;   // 끊긴 쪽(몹), 없으면 default
    }

    /// <summary>패턴을 끊은 순간들(연출용). 게임 로직이 쌓고 화면(InterruptFxRenderer)이 꺼내 간다.</summary>
    public static class InterruptFeed
    {
        public static readonly List<InterruptInfo> Pending = new(8);
    }
}
