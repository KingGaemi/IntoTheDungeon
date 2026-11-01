using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Features.AI
{
    /// <summary>
    /// CombatAIComponent를 가진 엔티티가 가장 가까운 적(다른 팀, 생존)을 바닥 평면에서 쫓아가, 같은 줄에 서면 공격하도록 의도를 쌓는다.
    /// 적이 없으면 파티원은 파티 중심으로 모인다.
    /// 상태가 바뀔 때만 의도를 넣는다(플레이어 입력과 같은 방식). CharacterIntentApplySystem보다 먼저 돌아야 한다.
    /// </summary>
    public sealed class CombatAISystem : GameSystem, ITick
    {
        struct Target
        {
            public Entity E;
            public TeamFlag Team;
            public Vec2 Pos;
        }

        readonly List<Target> _targets = new(32);
        float _time;

        bool _hasPartyCenter;
        Vec2 _partyCenter;

        BattleProgress _progress;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            world.TryGet(out _progress);
        }

        public void Tick(float dt)
        {
            _time += dt;
            if (_progress != null && _progress.Phase == BattlePhase.Map) return;   // 지도에서 방 고르는 동안은 가만히
            var em = _world.EntityManager;
            CollectTargets(em);
            _hasPartyCenter = Party.TryGetCenter(em, out _partyCenter, out _);

            foreach (var chunk in em.GetChunks(typeof(CombatAIComponent),
                                               typeof(CharacterIntentBuffer),
                                               typeof(StateComponent),
                                               typeof(TransformComponent),
                                               typeof(TeamComponent)))
            {
                var ais = chunk.GetComponentArray<CombatAIComponent>();
                var buffers = chunk.GetComponentArray<CharacterIntentBuffer>();
                var states = chunk.GetComponentArray<StateComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var ai = ref ais[i];
                    if (!ai.Enabled) continue;

                    ref var buf = ref buffers[i];
                    var cur = states[i].Current;
                    if (cur.Control == ControlState.Dead) continue;
                    if (cur.Action == ActionState.Skill) continue;   // 필살기 시전 중

                    // 공격 한 사이클이 끝나는 순간부터 쿨다운
                    bool attacking = cur.Action == ActionState.Attack;
                    if (ai.WasAttacking && !attacking)
                        ai.NextAttackAt = ai.AttackInterrupted ? _time : _time + ai.AttackCooldown;   // 끊겼으면 쉬지 않고 바로
                    if (!attacking) ai.AttackInterrupted = false;
                    ai.WasAttacking = attacking;
                    if (attacking) continue;   // 휘두르는 중엔 건드리지 않는다

                    var self = transforms[i].Position;
                    bool hasTarget = TryFindNearest(entities[i], teams[i].TeamFlag, self, out var target, out float dist);
                    if (!hasTarget || dist > ai.AggroRange)
                    {
                        if (!TryFollowLeader(ref ai, ref buf, entities[i], teams[i].TeamFlag, self, states[i]))
                        {
                            StopIfMoving(ref buf, cur);
                            ai.State = CombatAIState.Idle;
                        }
                        continue;
                    }

                    float dx = target.Pos.X - self.X;
                    float dz = target.Pos.Y - self.Y;   // 깊이 차이
                    var facing = System.MathF.Abs(dx) > 0.05f ? Facing2DExt.FromX(dx) : cur.Facing;

                    // 공격 중이던 상태면 조금 더 벗어나야 다시 쫓는다(경계에서 떨지 않게)
                    float slack = ai.State == CombatAIState.Attack ? ai.ChaseResume : 0f;
                    float adx = System.MathF.Abs(dx);
                    bool inX = adx <= ai.AttackRange + slack;
                    bool inZ = System.MathF.Abs(dz) <= ai.DepthTolerance + slack * 0.5f;

                    // 원거리: 너무 붙으면 물러난다. 벽 등으로 1초 넘게 못 벗어나면 거리가 다시 벌어질 때까지 그 자리에서 쏜다
                    bool tooClose = false;
                    if (ai.MinRange > 0f)
                    {
                        if (ai.HoldGround && adx > ai.MinRange + 0.5f) ai.HoldGround = false;
                        tooClose = !ai.HoldGround && adx < ai.MinRange;
                        if (tooClose)
                        {
                            ai.RetreatTime += dt;
                            if (ai.RetreatTime > 1f) { ai.HoldGround = true; tooClose = false; }
                        }
                        else ai.RetreatTime = 0f;
                    }

                    if (!(inX && inZ) || tooClose)
                    {
                        // 가로가 멀면 가로로(너무 가까우면 반대로), 줄이 어긋나 있으면 깊이로(둘 다면 대각선)
                        float wantX = tooClose ? -System.MathF.Sign(dx) : inX ? 0f : System.MathF.Sign(dx);
                        var want = new Vec2(wantX, inZ ? 0f : System.MathF.Sign(dz)).Normalized;
                        MoveIfChanged(ref buf, states[i], want);
                        ai.State = CombatAIState.Chase;
                    }
                    else
                    {
                        ai.State = CombatAIState.Attack;
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

        // 파티 중심에서 멀어졌으면 모이러 간다. 가는 중이면 true
        bool TryFollowLeader(ref CombatAIComponent ai, ref CharacterIntentBuffer buf, Entity self, TeamFlag myTeam, Vec2 selfPos, in StateComponent state)
        {
            if (!ai.FollowLeader || !_hasPartyCenter || myTeam != Party.Team) return false;

            var to = _partyCenter - selfPos;
            float d = to.Magnitude;
            // 따라가는 중이면 FollowStop까지, 서 있으면 FollowDistance를 넘어야 출발(떨림 방지)
            float limit = ai.State == CombatAIState.Follow ? ai.FollowStop : ai.FollowDistance;
            if (d <= limit) return false;

            MoveIfChanged(ref buf, state, to.Normalized);
            ai.State = CombatAIState.Follow;
            return true;
        }

        static void MoveIfChanged(ref CharacterIntentBuffer buf, in StateComponent state, Vec2 want)
        {
            bool moving = state.Current.Movement != MovementState.Idle;
            // 리더 쪽 방향은 연속값이라 조금씩 바뀔 때마다 의도를 넣지 않게 허용 오차를 둔다
            if (!moving || !state.MoveDir.ApproximatelyEquals(want, 0.05f))
                buf.Add(CharacterIntent.Move(want));
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
