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
    /// 공격이 Active에 들어선 순간(ReadyToAct) 근접 히트박스 엔티티를 만든다.
    /// </summary>
    public sealed class AttackSystem : GameSystem, ITick
    {
        struct Request
        {
            public Entity Owner;
            public TeamFlag Team;
            public int Damage;
            public Vec2 OwnerPos;
            public float FacingSign;
            public MeleeAttackComponent Melee;
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
                                               typeof(TeamComponent),
                                               typeof(MeleeAttackComponent)))
            {
                var actions = chunk.GetComponentArray<ActionPhaseComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var melees = chunk.GetComponentArray<MeleeAttackComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var action = ref actions[i];
                    if (!action.ReadyToAct) continue;

                    // PhaseControlSystem은 사이클이 끝날 때까지 true로 두므로, 여기서 한 번 쓰고 끈다.
                    action.ReadyToAct = false;
                    if (!states[i].CanAttack) continue;

                    _requests.Add(new Request
                    {
                        Owner = entities[i],
                        Team = teams[i].TeamFlag,
                        Damage = statuses[i].Damage,
                        OwnerPos = transforms[i].Position,
                        FacingSign = states[i].FacingToSign(),
                        Melee = melees[i]
                    });
                }
            }

            // 청크 순회 중 구조 변경을 피하려고 생성은 순회가 끝난 뒤에 한다.
            foreach (var r in _requests)
                CreateMeleeHitbox(em, r);
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
                HalfExtents = r.Melee.HalfExtents
            });
            em.AddComponent(e, new FollowOwnerComponent { Offset = r.Melee.Offset });
            em.AddComponent(e, new LifetimeComponent { Remaining = r.Melee.ActiveTime });
            em.AddComponent(e, new HitListComponent());
        }
    }
}
