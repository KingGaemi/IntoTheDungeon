using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 필살기 진행: 요청 → (평타 중이면 끊고) 시전 시작 → CastTime에 검기 발사 → Duration 뒤 Idle.
    /// 상태(ActionState.Skill)를 직접 바꾸므로 StateChangedEvent도 여기서 낸다(뷰가 대시 공격 모션을 튼다).
    /// PhaseControlSystem 다음, AttackSystem·KinematicPlanner보다 먼저 돈다.
    /// </summary>
    public sealed class SpecialSkillSystem : GameSystem, ITick
    {
        struct WaveRequest
        {
            public Entity Owner;
            public TeamFlag Team;
            public int Damage;
            public Vec2 Pos;
            public float Height;
            public float FacingSign;
            public SpecialSkillComponent Skill;
        }

        const float RequestTimeout = 0.5f;   // 바로 못 쓰는 상태(공중 등)로 이만큼 지나면 요청을 버린다

        IEventHub _hub;
        readonly List<WaveRequest> _waves = new(4);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _hub)) Enabled = false;
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _waves.Clear();

            foreach (var chunk in em.GetChunks(typeof(SpecialSkillComponent),
                                               typeof(StateComponent),
                                               typeof(ActionPhaseComponent),
                                               typeof(StatusComponent),
                                               typeof(TransformComponent),
                                               typeof(TeamComponent)))
            {
                var skills = chunk.GetComponentArray<SpecialSkillComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var phases = chunk.GetComponentArray<ActionPhaseComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var sk = ref skills[i];
                    ref var state = ref states[i];
                    var e = entities[i];

                    if (sk.CooldownRemaining > 0f) sk.CooldownRemaining = System.MathF.Max(0f, sk.CooldownRemaining - dt);

                    if (state.Current.Control == ControlState.Dead)
                    {
                        sk.Casting = false;
                        sk.Requested = false;
                        continue;
                    }

                    if (sk.Casting)
                    {
                        sk.Timer += dt;
                        if (!sk.Fired && sk.Timer >= sk.CastTime)
                        {
                            sk.Fired = true;
                            _waves.Add(new WaveRequest
                            {
                                Owner = e,
                                Team = teams[i].TeamFlag,
                                Damage = (int)System.MathF.Round(statuses[i].Damage * sk.DamageMultiplier),
                                Pos = transforms[i].Position,
                                Height = transforms[i].Height,
                                FacingSign = state.FacingToSign(),
                                Skill = sk
                            });
                        }
                        if (sk.Timer >= sk.Duration)
                        {
                            sk.Casting = false;
                            if (state.Current.Action == ActionState.Skill)
                                SetAction(e, ref state, ActionState.Idle);
                        }
                        continue;
                    }

                    if (!sk.Requested) continue;
                    sk.RequestAge += dt;

                    if (sk.CooldownRemaining > 0f) { sk.Requested = false; continue; }   // 아직 못 씀
                    if (!state.CanAttack || transforms[i].Height > 0.01f)                  // 경직·공중이면 잠깐 기다림
                    {
                        if (sk.RequestAge > RequestTimeout) sk.Requested = false;
                        continue;
                    }

                    // 평타 중이면 끊는다(평타 히트박스도 안 나가게)
                    ref var phase = ref phases[i];
                    phase.ActionPhase = ActionPhase.None;
                    phase.PhaseTimer = 0f;
                    phase.ReadyToAct = false;
                    phase.DirtyPhase = false;

                    sk.Requested = false;
                    sk.Casting = true;
                    sk.Timer = 0f;
                    sk.Fired = false;
                    sk.CooldownRemaining = sk.Cooldown;

                    state.MoveDir = Vec2.Zero;
                    SetAction(e, ref state, ActionState.Skill, stopMoving: true);

                    // 대시 공격답게 앞으로 살짝 나간다
                    if (sk.DashSpeed > 0f && em.HasComponent<KnockbackComponent>(e))
                        em.GetComponent<KnockbackComponent>(e).Velocity = new Vec2(state.FacingToSign() * sk.DashSpeed, 0f);
                }
            }

            // 청크 순회 중 구조 변경을 피하려고 생성은 순회가 끝난 뒤에 한다.
            foreach (var w in _waves)
                CreateWave(em, w);
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

        static void CreateWave(IEntityManager em, in WaveRequest r)
        {
            var sk = r.Skill;
            var e = em.CreateEntity();
            em.AddComponent(e, new HitboxComponent
            {
                Owner = r.Owner,
                Team = r.Team,
                Damage = r.Damage,
                Center = new Vec2(r.Pos.X + r.FacingSign * (sk.WaveHalfExtents.X + 0.2f), r.Pos.Y),   // 몸 바로 앞에서 출발
                HalfExtents = sk.WaveHalfExtents,
                Height = r.Height,
                FacingSign = r.FacingSign,
                KnockbackSpeed = sk.WaveKnockback,
                LaunchSpeed = sk.WaveLaunch
            });
            em.AddComponent(e, new ProjectileComponent { Velocity = new Vec2(r.FacingSign * sk.WaveSpeed, 0f), DestroyOnHit = false });
            em.AddComponent(e, new LifetimeComponent { Remaining = sk.WaveLifetime });
            em.AddComponent(e, new HitListComponent());   // 관통: 같은 대상은 한 번만
            em.AddComponent(e, new ProjectileVisualComponent { Kind = sk.Visual, TotalLifetime = sk.WaveLifetime });
        }
    }
}
