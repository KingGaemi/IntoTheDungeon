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
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>보스 표시. 그림은 Scale배로 크게 그린다(BossRenderer).</summary>
    public struct BossComponent : IComponentData
    {
        public float Scale;
    }

    /// <summary>몹 패턴 종류. 모두 예고(힘 모으기) 뒤에 터지고, 그동안 요구 저지력 이상 필살기 한 방이면 끊긴다.</summary>
    public enum PatternKind : byte
    {
        Slam,    // 강타·내려찍기: 주변 파티원에게 큰 피해(못 끊으면 치명상)
        Heal,    // 치유 의식(주술사): 범위 안 몹 회복 + 광폭(공격력 영구 증가, 겹침)
        Hatch,   // 부화(알집): 새끼 거미가 Power마리 깨어난다
        Barrier, // 마법 보호막(주술사): 범위 안 몹이 Power초 동안 물리 피해를 거의 안 받는다 → 끊거나 마법 피해로
        Charge   // 돌진(오크 돌격병): 가장 가까운 파티원 쪽으로 일직선, 처음 닿은 한 명이 큰 피해 → 끊거나 전열이 방패로 받아낸다
    }

    /// <summary>마법 보호막: 남은 동안 물리 피해 × Boss.BarrierPhysicalMul.</summary>
    public struct BarrierComponent : IComponentData
    {
        public float Remaining, Duration;
    }

    /// <summary>
    /// 몹 패턴(강타·치유 의식·부화): 쿨이 돌고 조건이 맞으면 멈춰서 힘을 모으고(바닥에 예고 범위가 차오름),
    /// 다 차면 터진다. 강타는 예고 범위 밖으로 빠지면 피할 수 있다.
    /// </summary>
    public struct BossSlamComponent : IComponentData
    {
        public PatternKind Kind;
        public float Power;             // 치유: 최대 체력 비율 / 부화: 마릿수
        public float Cooldown, Remaining;
        public float ChargeTime, RecoveryTime;
        public float Radius;            // 가로 반지름(깊이는 절반)
        public float DamageMultiplier;  // 보스 공격력 × 이 값
        public bool Charging, Fired;
        public float Timer;             // 힘 모으기 시작부터
        public float ImpactAge;         // 내려찍은 뒤 지난 시간(연출용, 아직이면 음수)
        public Vec2 Center;
        public int StopRequired;        // 요구 저지력: 힘 모으는 동안 맞힌 필살기 한 방의 저지력이 이 이상이면 끊김
        public PatternKind AltKind;     // Alternates면 쓸 때마다 Kind·Power와 번갈아(주술사: 치유 의식 ↔ 마법 보호막)
        public float AltPower;
        public bool Alternates;
        public float Depth;             // 깊이 반폭(0이면 Radius의 절반). 돌진은 가늘고 길다
        public float Dir;               // 돌진 방향(+1 오른쪽, -1 왼쪽)
    }

    /// <summary>보스 수치(임시값)와 승격.</summary>
    public static class Boss
    {
        public const string SpawnName = "Boss";   // 웨이브에서 보스로 나올 때 스폰 이름
        public const float Scale = 1.6f;
        public const float HpMultiplier = 3f;
        public static readonly PrimaryStats StatBonus = new(6, 2, 0, 6);
        public const int StopRequired = 15;   // 보스 내려찍기 요구 저지력(저지력 장비·유물이 하나는 있어야 끊김). 못 끊으면 치명상이라 20 → 15
        public const float EnrageMul = 1.25f; // 치유 의식 한 번마다 몹 공격력 ×
        public const string HatchRecipe = "Spider1";
        public const float BarrierPhysicalMul = 0.2f;   // 보호막 동안 받는 물리 피해 배율
        public const float ChargeDepth = 0.6f, ChargeDashSpeed = 28f;

        /// <summary>부화로 깨어난 새끼(레시피, 위치). BattleFlowSystem이 다음 프레임에 적으로 스폰한다.</summary>
        public static readonly List<(string recipe, Vec2 pos)> PatternSpawns = new(8);

        /// <summary>보스로 만든다: 스탯 보너스·몸집·내려찍기. 스탯 재계산 전에 부른다(체력 배율은 재계산 뒤 ApplyHp).</summary>
        public static void Promote(IEntityManager em, Entity e)
        {
            em.AddComponent(e, new BossComponent { Scale = Scale });
            var slam = new BossSlamComponent
            {
                Cooldown = 9f, Remaining = 4f,
                ChargeTime = 1.8f, RecoveryTime = 0.6f,   // 예고를 길게(눌러서 끊을 틈), 대신 못 끊으면 치명상
                Radius = 3f, DamageMultiplier = 6f,
                ImpactAge = -1f,
                StopRequired = StopRequired
            };
            // 원래 강타가 있던 몹(오크 등)이면 보스 내려찍기로 덮어쓴다
            if (em.HasComponent<BossSlamComponent>(e)) em.GetComponent<BossSlamComponent>(e) = slam;
            else em.AddComponent(e, slam);
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
            foreach (var chunk in em.GetChunks(typeof(BarrierComponent)))
            {
                var barriers = chunk.GetComponentArray<BarrierComponent>();
                for (int i = 0; i < chunk.Count; i++) if (barriers[i].Remaining > 0f) barriers[i].Remaining -= dt;
            }

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
                            if (s.Alternates) { (s.Kind, s.AltKind) = (s.AltKind, s.Kind); (s.Power, s.AltPower) = (s.AltPower, s.Power); }   // 다음엔 다른 의식
                            if (state.Current.Action == ActionState.Skill) SetAction(e, ref state, ActionState.Idle);
                        }
                        continue;
                    }

                    if (!fighting) continue;
                    if (s.Remaining > 0f) { s.Remaining -= dt; continue; }
                    // 알집은 맞아서 움찔해도 부화를 멈추지 않는다(끊으려면 저지력)
                    if ((!state.CanAttack && !(s.Kind == PatternKind.Hatch && state.Current.Control == ControlState.Stunned)) || transforms[i].Height > 0.01f) continue;
                    bool ready = s.Kind switch
                    {
                        PatternKind.Heal or PatternKind.Barrier => AlliesNear(em, e, teams[i].TeamFlag, transforms[i].Position, s.Radius),   // 곁에 지킬 몹이 있을 때
                        PatternKind.Hatch => true,
                        PatternKind.Charge => PartyNear(em, transforms[i].Position, s.Radius * 2f),   // 돌진 길이(반지름 × 2) 안에 파티원
                        _ => PartyNear(em, transforms[i].Position, s.Radius + 1f)
                    };
                    if (!ready) continue;

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
                    if (s.Kind == PatternKind.Charge)   // 가장 가까운 파티원 쪽으로 일직선(예고도 그 줄)
                    {
                        var pos = transforms[i].Position;
                        s.Dir = NearestPartyDir(em, pos);
                        s.Depth = Boss.ChargeDepth;
                        s.Center = new Vec2(pos.X + s.Dir * s.Radius, pos.Y);
                    }
                    state.MoveDir = Vec2.Zero;
                    SetAction(e, ref state, ActionState.Skill, stopMoving: true);
                }
            }

            // 청크 순회가 끝난 뒤 생성
            foreach (var r in _slams)
            {
                if (r.slam.Kind == PatternKind.Heal) { Heal(em, r.team, r.slam); continue; }
                if (r.slam.Kind == PatternKind.Barrier) { Barrier(em, r.team, r.slam); continue; }
                bool charge = r.slam.Kind == PatternKind.Charge;
                if (r.slam.Kind == PatternKind.Hatch)
                {
                    int n = System.Math.Max(1, (int)r.slam.Power);
                    for (int k = 0; k < n; k++)
                        Boss.PatternSpawns.Add((Boss.HatchRecipe, new Vec2(r.slam.Center.X + (k % 2 == 0 ? -0.7f : 0.7f) * (1 + k / 2), r.slam.Center.Y + (k % 3 - 1) * 0.35f)));
                    continue;
                }
                bool boss = em.HasComponent<BossComponent>(r.owner);   // 일반 몹 강타는 조금 약하게 띄우고 짧게 굳힘
                var hit = em.CreateEntity();
                em.AddComponent(hit, new HitboxComponent
                {
                    Owner = r.owner,
                    Team = r.team,
                    Damage = r.damage,
                    Center = r.slam.Center,
                    HalfExtents = new Vec2(r.slam.Radius, r.slam.Depth > 0f ? r.slam.Depth : r.slam.Radius * 0.5f),
                    Height = r.height,
                    FacingSign = charge ? r.slam.Dir : r.facing,
                    MaxTargets = charge ? 1 : 0,   // 돌진: 처음 닿은 한 명(전열)이 다 받는다
                    KnockbackSpeed = charge ? 7f : 1.5f,
                    LaunchSpeed = boss ? 5f : 3f,
                    HitStun = boss ? 0.6f : 0.4f,
                    HpRatioDamage = RunState.Current.TutorialFirstBattle ? Encounters.TutorialSlamHpRatio : 0f
                });
                em.AddComponent(hit, new LifetimeComponent { Remaining = 0.12f });
                em.AddComponent(hit, new HitListComponent());
                if (charge && em.HasComponent<KnockbackComponent>(r.owner))   // 몸도 그 줄을 따라 튀어 나간다
                    em.GetComponent<KnockbackComponent>(r.owner).Velocity = new Vec2(r.slam.Dir * Boss.ChargeDashSpeed, 0f);
            }
        }

        // 마법 보호막: 범위 안 같은 편 몹(자신 포함)이 Power초 동안 물리 피해를 거의 안 받는다
        static void Barrier(IEntityManager em, TeamFlag team, in BossSlamComponent s)
        {
            s_targets.Clear();
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != team || !statuses[i].IsAlive) continue;
                    var d = transforms[i].Position - s.Center;
                    if (System.MathF.Abs(d.X) > s.Radius || System.MathF.Abs(d.Y) > s.Radius * 0.5f) continue;
                    s_targets.Add(entities[i]);
                    float head = em.TryGetComponent<BodyComponent>(entities[i], out var body) ? body.HeadHeight : 1.2f;
                    InterruptFeed.Pending.Add(new InterruptInfo { Kind = InterruptKind.Shielded, Position = transforms[i].Position, Height = transforms[i].Height + head });
                }
            }
            var barrier = new BarrierComponent { Remaining = s.Power, Duration = s.Power };
            foreach (var e in s_targets)   // 청크 순회가 끝난 뒤 붙인다
            {
                if (em.HasComponent<BarrierComponent>(e)) em.GetComponent<BarrierComponent>(e) = barrier;
                else em.AddComponent(e, barrier);
            }
        }

        static readonly List<Entity> s_targets = new(16);

        // 가장 가까운 살아 있는 파티원 쪽(+1/-1)
        static float NearestPartyDir(IEntityManager em, Vec2 pos)
        {
            float best = float.MaxValue, dir = 1f;
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != Party.Team || !statuses[i].IsAlive) continue;
                    var d = transforms[i].Position - pos;
                    float dist = d.X * d.X + d.Y * d.Y;
                    if (dist < best) { best = dist; dir = d.X >= 0f ? 1f : -1f; }
                }
            }
            return dir;
        }

        // 치유 의식: 범위 안 같은 편 몹(자신 포함)을 최대 체력 비율만큼 회복하고 광폭(공격력 영구 증가, 겹침)
        static void Heal(IEntityManager em, TeamFlag team, in BossSlamComponent s)
        {
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent), typeof(HpModificationQueue)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var queues = chunk.GetComponentArray<HpModificationQueue>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != team || !statuses[i].IsAlive) continue;
                    var d = transforms[i].Position - s.Center;
                    if (System.MathF.Abs(d.X) > s.Radius || System.MathF.Abs(d.Y) > s.Radius * 0.5f) continue;
                    queues[i].Enqueue(HpModification.Heal(System.Math.Max(1, (int)System.MathF.Round(statuses[i].MaxHp * s.Power)), entities[i]));
                    statuses[i].Damage = (int)System.MathF.Round(statuses[i].Damage * Boss.EnrageMul);
                    float head = em.TryGetComponent<BodyComponent>(entities[i], out var body) ? body.HeadHeight : 1.2f;
                    InterruptFeed.Pending.Add(new InterruptInfo { Kind = InterruptKind.Empowered, Position = transforms[i].Position, Height = transforms[i].Height + head });
                }
            }
        }

        static bool AlliesNear(IEntityManager em, Entity self, TeamFlag team, Vec2 pos, float range)
        {
            foreach (var chunk in em.GetChunks(typeof(TeamComponent), typeof(StatusComponent), typeof(TransformComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (teams[i].TeamFlag != team || !statuses[i].IsAlive || entities[i] == self) continue;
                    var d = transforms[i].Position - pos;
                    if (System.MathF.Abs(d.X) <= range && System.MathF.Abs(d.Y) <= range * 0.5f) return true;
                }
            }
            return false;
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

    /// <summary>치유 의식 등으로 쌓인 광폭 스택(표시용 카운터). 공격력 배율 자체는 기존대로 StatusComponent.Damage에 곱해져 있다.</summary>
    public struct EnrageComponent : IComponentData { public int Stacks; }

    /// <summary>보스 2페이즈 설정·상태. patterns-boss 트랙이 Promote에서 붙이고 Tick에서 발동, battle-hud가 눈금 표시에 읽는다.</summary>
    public struct BossPhaseComponent : IComponentData
    {
        public float Threshold;        // 체력 비율(0.5)
        public bool Triggered;
        public float DamageMul, CooldownMul, ChargeMul, Barrier, KbResistAdd;
        public PatternKind Kind; public float Power; public bool Swap;
    }
}
