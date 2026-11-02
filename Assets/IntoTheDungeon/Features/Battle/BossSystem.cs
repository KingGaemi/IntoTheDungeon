using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.AI;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>보스 표시. 그림은 Scale배로 크게 그린다(BossRenderer).</summary>
    public struct BossComponent : IComponentData
    {
        public float Scale;
    }

    /// <summary>
    /// 보스 내려찍기: 쿨이 돌고 파티가 가까우면 멈춰서 힘을 모으고(바닥에 빨간 예고 범위가 차오름),
    /// 다 차면 주변을 내려찍어 큰 피해 + 띄우기 + 경직. 예고 범위 밖으로 빠지면 피할 수 있다.
    /// </summary>
    public struct BossSlamComponent : IComponentData
    {
        public float Cooldown, Remaining;
        public float ChargeTime, RecoveryTime;
        public float Radius;            // 가로 반지름(깊이는 절반)
        public float DamageMultiplier;  // 보스 공격력 × 이 값
        public bool Charging, Fired;
        public float Timer;             // 힘 모으기 시작부터
        public float ImpactAge;         // 내려찍은 뒤 지난 시간(연출용, 아직이면 음수)
        public Vec2 Center;
    }

    /// <summary>보스 수치(임시값)와 승격.</summary>
    public static class Boss
    {
        public const string SpawnName = "Boss";   // 웨이브에서 보스로 나올 때 스폰 이름
        public const float Scale = 1.6f;
        public const float HpMultiplier = 3f;
        public static readonly PrimaryStats StatBonus = new(6, 2, 0, 6);

        /// <summary>보스로 만든다: 스탯 보너스·몸집·내려찍기. 스탯 재계산 전에 부른다(체력 배율은 재계산 뒤 ApplyHp).</summary>
        public static void Promote(IEntityManager em, Entity e)
        {
            em.AddComponent(e, new BossComponent { Scale = Scale });
            em.AddComponent(e, new BossSlamComponent
            {
                Cooldown = 7f, Remaining = 4f,
                ChargeTime = 1.4f, RecoveryTime = 0.6f,
                Radius = 2.4f, DamageMultiplier = 2.5f,
                ImpactAge = -1f
            });
            if (em.HasComponent<PrimaryStatsComponent>(e)) em.GetComponent<PrimaryStatsComponent>(e).Bonus += StatBonus;
            if (em.HasComponent<MeleeAttackComponent>(e))   // 보스 평타는 범위
            {
                ref var melee = ref em.GetComponent<MeleeAttackComponent>(e);
                melee.CleaveBase = melee.Cleave = true;
            }
            if (em.HasComponent<BodyComponent>(e))
            {
                ref var body = ref em.GetComponent<BodyComponent>(e);
                body.RadiusMul *= Scale;
                body.MassMul *= 4f;
                body.HeadHeight *= Scale;
            }
        }

        public static void ApplyHp(IEntityManager em, Entity e)
        {
            ref var st = ref em.GetComponent<StatusComponent>(e);
            st.MaxHp = (int)System.MathF.Round(st.MaxHp * HpMultiplier);
            st.CurrentHp = st.MaxHp;
        }
    }

    public sealed class BossSystem : GameSystem, ITick
    {
        IEventHub _hub;
        BattleProgress _progress;
        readonly List<(Entity owner, TeamFlag team, int damage, BossSlamComponent slam, float height, float facing)> _slams = new(2);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _hub) && world.TryGet(out _progress);
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _slams.Clear();
            bool fighting = _progress.Phase == BattlePhase.Fighting;

            foreach (var chunk in em.GetChunks(typeof(BossSlamComponent), typeof(StateComponent), typeof(ActionPhaseComponent),
                                               typeof(StatusComponent), typeof(TransformComponent), typeof(TeamComponent)))
            {
                var slams = chunk.GetComponentArray<BossSlamComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var phases = chunk.GetComponentArray<ActionPhaseComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var s = ref slams[i];
                    ref var state = ref states[i];
                    var e = entities[i];
                    if (s.ImpactAge >= 0f) s.ImpactAge += dt;

                    if (state.Current.Control == ControlState.Dead) { s.Charging = false; continue; }

                    if (s.Charging)
                    {
                        s.Timer += dt;
                        if (!s.Fired && s.Timer >= s.ChargeTime)
                        {
                            s.Fired = true;
                            s.ImpactAge = 0f;
                            _slams.Add((e, teams[i].TeamFlag, (int)System.MathF.Round(statuses[i].Damage * s.DamageMultiplier), s,
                                        transforms[i].Height, state.FacingToSign()));
                        }
                        if (s.Timer >= s.ChargeTime + s.RecoveryTime)
                        {
                            s.Charging = false;
                            s.Remaining = s.Cooldown;
                            if (state.Current.Action == ActionState.Skill) SetAction(e, ref state, ActionState.Idle);
                        }
                        continue;
                    }

                    if (!fighting) continue;
                    if (s.Remaining > 0f) { s.Remaining -= dt; continue; }
                    if (!state.CanAttack || transforms[i].Height > 0.01f) continue;
                    if (!PartyNear(em, transforms[i].Position, s.Radius + 1f)) continue;

                    // 힘 모으기 시작: 평타를 끊고 그 자리에 멈춘다(시전 중엔 경직도 안 걸림)
                    if (state.Current.Action == ActionState.Attack && em.HasComponent<CombatAIComponent>(e))
                        em.GetComponent<CombatAIComponent>(e).AttackInterrupted = true;
                    ref var phase = ref phases[i];
                    phase.ActionPhase = ActionPhase.None;
                    phase.PhaseTimer = 0f;
                    phase.ReadyToAct = false;
                    phase.DirtyPhase = false;

                    s.Charging = true;
                    s.Fired = false;
                    s.Timer = 0f;
                    s.Center = transforms[i].Position;
                    state.MoveDir = Vec2.Zero;
                    SetAction(e, ref state, ActionState.Skill, stopMoving: true);
                }
            }

            // 청크 순회가 끝난 뒤 생성
            foreach (var r in _slams)
            {
                var hit = em.CreateEntity();
                em.AddComponent(hit, new HitboxComponent
                {
                    Owner = r.owner,
                    Team = r.team,
                    Damage = r.damage,
                    Center = r.slam.Center,
                    HalfExtents = new Vec2(r.slam.Radius, r.slam.Radius * 0.5f),
                    Height = r.height,
                    FacingSign = r.facing,
                    KnockbackSpeed = 1.5f,
                    LaunchSpeed = 5f,
                    HitStun = 0.6f
                });
                em.AddComponent(hit, new LifetimeComponent { Remaining = 0.12f });
                em.AddComponent(hit, new HitListComponent());
            }
        }

        static bool PartyNear(IEntityManager em, Vec2 pos, float range)
        {
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != Party.Team || !statuses[i].IsAlive) continue;
                    var d = transforms[i].Position - pos;
                    if (System.MathF.Abs(d.X) <= range && System.MathF.Abs(d.Y) <= range * 0.5f) return true;
                }
            }
            return false;
        }

        void SetAction(Entity e, ref StateComponent state, ActionState action, bool stopMoving = false)
        {
            var prev = state.Current;
            var next = prev;
            next.Action = action;
            if (stopMoving) next.Movement = MovementState.Idle;

            var mask = ChangeMask.None;
            if (prev.Action != next.Action) mask |= ChangeMask.Action;
            if (prev.Movement != next.Movement) mask |= ChangeMask.Movement;
            if (mask == ChangeMask.None) return;

            next.Version = prev.Version + 1;
            state.Current = next;
            _hub.Publish(StateChangedEvent.FromSnapshot(e, next, mask));
        }
    }
}
