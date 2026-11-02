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

        /// <summary>도발 발동: 반경 안(깊이는 절반)의 살아 있는 적에게 도발을 걸고, 시전자에게 방패를 건다.</summary>
        public static void Apply(IEntityManager em, Entity owner, TeamFlag team, Vec2 pos, in SpecialSkillComponent sk)
        {
            s_hits.Clear();
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag == team || teams[i].TeamFlag == TeamFlag.None || !statuses[i].IsAlive) continue;
                    var d = transforms[i].Position - pos;
                    if (System.MathF.Abs(d.X) <= sk.TauntRadius && System.MathF.Abs(d.Y) <= sk.TauntRadius * 0.5f) s_hits.Add(entities[i]);
                }
            }

            // 청크 순회가 끝난 뒤 붙인다(구조 변경)
            var taunt = new TauntedComponent { Taunter = owner, Remaining = sk.TauntDuration };
            foreach (var e in s_hits)
            {
                if (em.HasComponent<TauntedComponent>(e)) em.GetComponent<TauntedComponent>(e) = taunt;
                else em.AddComponent(e, taunt);
                Interrupt(em, e);
            }

            var guard = new GuardComponent { Remaining = sk.GuardDuration, Duration = sk.GuardDuration, DamageMul = sk.GuardDamageMul, Radius = sk.TauntRadius };
            if (em.HasComponent<GuardComponent>(owner)) em.GetComponent<GuardComponent>(owner) = guard;
            else em.AddComponent(owner, guard);
        }

        // 패턴 끊기(임시값): 보스가 힘을 모으던 내려찍기는 취소 + 그로기, 휘두르려던 평타는 끊고 경직, 나머지는 움찔
        public const float PatternBreakStun = 2f, AttackBreakStun = 0.8f, FlinchStun = 0.35f;

        static void Interrupt(IEntityManager em, Entity e)
        {
            if (!em.HasComponent<HitStunComponent>(e) || !em.TryGetComponent<StateComponent>(e, out var state)) return;
            var kind = InterruptKind.Flinch;
            float stun = FlinchStun;

            if (em.HasComponent<Battle.BossSlamComponent>(e))
            {
                ref var slam = ref em.GetComponent<Battle.BossSlamComponent>(e);
                if (slam.Charging && !slam.Fired)
                {
                    slam.Charging = false;
                    slam.Remaining = slam.Cooldown;   // 다시 쓰려면 재사용 대기를 처음부터
                    kind = InterruptKind.PatternBroken;
                    stun = PatternBreakStun;
                }
            }
            if (kind == InterruptKind.Flinch && state.Current.Action == ActionState.Attack
                && em.TryGetComponent<ActionPhaseComponent>(e, out var phase) && phase.ActionPhase == ActionPhase.Windup)
            {
                kind = InterruptKind.AttackBroken;
                stun = AttackBreakStun;
            }

            ref var hs = ref em.GetComponent<HitStunComponent>(e);
            if (hs.PendingBreak < stun) hs.PendingBreak = stun;
            if (kind == InterruptKind.Flinch) return;

            var pos = em.GetComponent<TransformComponent>(e);
            float head = em.TryGetComponent<Physics.Components.BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
            InterruptFeed.Pending.Add(new InterruptInfo { Kind = kind, Position = pos.Position, Height = pos.Height + head });
        }
    }

    public enum InterruptKind : byte { Flinch, AttackBroken, PatternBroken }

    public struct InterruptInfo
    {
        public InterruptKind Kind;
        public Vec2 Position;   // 바닥 위치(가로, 깊이)
        public float Height;    // 발 높이 + 머리 높이
    }

    /// <summary>패턴을 끊은 순간들(연출용). 게임 로직이 쌓고 화면(InterruptFxRenderer)이 꺼내 간다.</summary>
    public static class InterruptFeed
    {
        public static readonly List<InterruptInfo> Pending = new(8);
    }
}
