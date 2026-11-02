using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 공격이 Active에 들어선 순간(ReadyToAct) 근접 히트박스, 또는 원거리면 화살(날아가는 히트박스)을 만든다.
    /// </summary>
    public sealed class AttackSystem : GameSystem, ITick
    {
        struct Request
        {
            public Entity Owner;
            public TeamFlag Team;
            public int Damage;
            public Vec2 OwnerPos;
            public float OwnerHeight;
            public float FacingSign;
            public MeleeAttackComponent Melee;
            public bool IsRanged;
            public RangedAttackComponent Ranged;
        }

        readonly List<Request> _requests = new(16);

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _requests.Clear();

            foreach (var chunk in em.GetChunks(typeof(ActionPhaseComponent),
                                               typeof(StateComponent),
                                               typeof(StatusComponent),
                                               typeof(TransformComponent),
                                               typeof(TeamComponent)))
            {
                var actions = chunk.GetComponentArray<ActionPhaseComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var action = ref actions[i];
                    if (!action.ReadyToAct) continue;

                    // PhaseControlSystem은 사이클이 끝날 때까지 true로 두므로, 여기서 한 번 쓰고 끈다.
                    action.ReadyToAct = false;
                    if (!states[i].CanAttack) continue;

                    var e = entities[i];
                    MeleeAttackComponent meleeDef = default;
                    bool ranged = em.TryGetComponent<RangedAttackComponent>(e, out var rangedDef);
                    if (!ranged && !em.TryGetComponent<MeleeAttackComponent>(e, out meleeDef)) continue;
                    _requests.Add(new Request
                    {
                        Owner = e,
                        Team = teams[i].TeamFlag,
                        Damage = statuses[i].Damage,
                        OwnerPos = transforms[i].Position,
                        OwnerHeight = transforms[i].Height,
                        FacingSign = states[i].FacingToSign(),
                        Melee = meleeDef,
                        IsRanged = ranged,
                        Ranged = rangedDef
                    });
                }
            }

            // 청크 순회 중 구조 변경을 피하려고 생성은 순회가 끝난 뒤에 한다.
            foreach (var r in _requests)
            {
                if (r.IsRanged) CreateArrow(em, r);
                else CreateMeleeHitbox(em, r);
            }
        }

        static void CreateArrow(IEntityManager em, in Request r)
        {
            var d = r.Ranged;
            var e = em.CreateEntity();
            em.AddComponent(e, new HitboxComponent
            {
                Owner = r.Owner,
                Team = r.Team,
                Damage = r.Damage,
                Center = new Vec2(r.OwnerPos.X + r.FacingSign * d.SpawnOffsetX, r.OwnerPos.Y),
                HalfExtents = d.HalfExtents,
                Height = r.OwnerHeight,
                FacingSign = r.FacingSign,
                KnockbackSpeed = d.KnockbackSpeed,
                LaunchSpeed = 0f,
                HitStun = d.HitStun
            });
            em.AddComponent(e, new ProjectileComponent { Velocity = new Vec2(r.FacingSign * d.Speed, 0f), DestroyOnHit = !d.Pierce });
            em.AddComponent(e, new LifetimeComponent { Remaining = d.Lifetime });
            em.AddComponent(e, new HitListComponent());
            em.AddComponent(e, new ProjectileVisualComponent { Kind = ProjectileVisual.Arrow, TotalLifetime = d.Lifetime });
        }

        static void CreateMeleeHitbox(IEntityManager em, in Request r)
        {
            var e = em.CreateEntity();
            em.AddComponent(e, new HitboxComponent
            {
                Owner = r.Owner,
                Team = r.Team,
                Damage = r.Damage,
                Center = FollowOwnerSystem.ComputeCenter(r.OwnerPos, r.Melee.Offset, r.FacingSign),
                HalfExtents = r.Melee.HalfExtents,
                Height = r.OwnerHeight,
                FacingSign = r.FacingSign,
                KnockbackSpeed = r.Melee.KnockbackSpeed,
                LaunchSpeed = r.Melee.LaunchSpeed,
                HitStun = r.Melee.HitStun,
                MaxTargets = r.Melee.Cleave ? 0 : 1   // 일반 평타는 한 명, 범위 평타는 휘두른 범위 전부
            });
            em.AddComponent(e, new FollowOwnerComponent { Offset = r.Melee.Offset });
            em.AddComponent(e, new LifetimeComponent { Remaining = r.Melee.ActiveTime });
            em.AddComponent(e, new HitListComponent());
        }
    }
}
