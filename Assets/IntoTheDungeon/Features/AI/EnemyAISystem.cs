using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.AI
{
    /// <summary>
    /// EnemyAIComponent를 가진 엔티티가 가장 가까운 적(다른 팀, 생존)을 바닥 평면에서 쫓아가, 같은 줄에 서면 공격하도록 의도를 쌓는다.
    /// 상태가 바뀔 때만 의도를 넣는다(플레이어 입력과 같은 방식). CharacterIntentApplySystem보다 먼저 돌아야 한다.
    /// </summary>
    public sealed class EnemyAISystem : GameSystem, ITick
    {
        struct Target
        {
            public Entity E;
            public TeamFlag Team;
            public Vec2 Pos;
        }

        readonly List<Target> _targets = new(32);
        float _time;

        public void Tick(float dt)
        {
            _time += dt;
            var em = _world.EntityManager;
            CollectTargets(em);

            foreach (var chunk in em.GetChunks(typeof(EnemyAIComponent),
                                               typeof(CharacterIntentBuffer),
                                               typeof(StateComponent),
                                               typeof(TransformComponent),
                                               typeof(TeamComponent)))
            {
                var ais = chunk.GetComponentArray<EnemyAIComponent>();
                var buffers = chunk.GetComponentArray<CharacterIntentBuffer>();
                var states = chunk.GetComponentArray<StateComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var ai = ref ais[i];
                    ref var buf = ref buffers[i];
                    var cur = states[i].Current;
                    if (cur.Control == ControlState.Dead) continue;

                    // 공격 한 사이클이 끝나는 순간부터 쿨다운
                    bool attacking = cur.Action == ActionState.Attack;
                    if (ai.WasAttacking && !attacking) ai.NextAttackAt = _time + ai.AttackCooldown;
                    ai.WasAttacking = attacking;
                    if (attacking) continue;   // 휘두르는 중엔 건드리지 않는다

                    var self = transforms[i].Position;
                    if (!TryFindNearest(entities[i], teams[i].TeamFlag, self, out var target, out float dist))
                    {
                        StopIfMoving(ref buf, cur);
                        ai.State = EnemyAIState.Idle;
                        continue;
                    }

                    float dx = target.Pos.X - self.X;
                    float dz = target.Pos.Y - self.Y;   // 깊이 차이
                    var facing = System.MathF.Abs(dx) > 0.05f ? Facing2DExt.FromX(dx) : cur.Facing;

                    // 공격 중이던 상태면 조금 더 벗어나야 다시 쫓는다(경계에서 떨지 않게)
                    float slack = ai.State == EnemyAIState.Attack ? ai.ChaseResume : 0f;
                    bool inX = System.MathF.Abs(dx) <= ai.AttackRange + slack;
                    bool inZ = System.MathF.Abs(dz) <= ai.DepthTolerance + slack * 0.5f;

                    if (dist > ai.AggroRange)
                    {
                        StopIfMoving(ref buf, cur);
                        ai.State = EnemyAIState.Idle;
                    }
                    else if (!(inX && inZ))
                    {
                        // 가로가 멀면 가로로, 줄이 어긋나 있으면 깊이로(둘 다면 대각선)
                        var want = new Vec2(inX ? 0f : System.MathF.Sign(dx), inZ ? 0f : System.MathF.Sign(dz)).Normalized;
                        bool moving = cur.Movement != MovementState.Idle;
                        if (!moving || !states[i].MoveDir.ApproximatelyEquals(want, 0.01f))
                            buf.Add(CharacterIntent.Move(want));
                        ai.State = EnemyAIState.Chase;
                    }
                    else
                    {
                        ai.State = EnemyAIState.Attack;
                        bool moving = cur.Movement != MovementState.Idle;
                        if (cur.Facing != facing)
                        {
                            buf.Add(CharacterIntent.Move(facing));   // 방향만 돌리고
                            buf.Add(CharacterIntent.Stop());          // 바로 멈춘다
                        }
                        else if (moving)
                        {
                            buf.Add(CharacterIntent.Stop());
                        }

                        if (_time >= ai.NextAttackAt)
                            buf.Add(CharacterIntent.Attack());
                    }
                }
            }
        }

        static void StopIfMoving(ref CharacterIntentBuffer buf, in StateSnapshot cur)
        {
            if (cur.Movement != MovementState.Idle)
                buf.Add(CharacterIntent.Stop());
        }

        void CollectTargets(IEntityManager em)
        {
            _targets.Clear();
            foreach (var chunk in em.GetChunks(typeof(TransformComponent),
                                               typeof(TeamComponent),
                                               typeof(StatusComponent),
                                               typeof(StateComponent)))
            {
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (!statuses[i].IsAlive) continue;
                    _targets.Add(new Target { E = entities[i], Team = teams[i].TeamFlag, Pos = transforms[i].Position });
                }
            }
        }

        bool TryFindNearest(Entity self, TeamFlag myTeam, Vec2 selfPos, out Target best, out float bestDist)
        {
            best = default;
            bestDist = float.MaxValue;
            bool found = false;
            foreach (var t in _targets)
            {
                if (t.E == self || t.Team == myTeam || t.Team == TeamFlag.None) continue;
                float d = (t.Pos - selfPos).Magnitude;   // 바닥 평면 거리
                if (d < bestDist) { bestDist = d; best = t; found = true; }
            }
            return found;
        }
    }
}
