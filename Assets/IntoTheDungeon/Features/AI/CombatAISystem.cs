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
using IntoTheDungeon.Features.Attack;

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
            public bool Pile;   // 무너진 해골(뼈 무더기): 살아 있는 적이 없을 때만 그 곁으로 간다(필살기로 부수라고)
        }

        readonly List<Target> _targets = new(32);
        float _time;

        bool _hasPartyCenter;
        Vec2 _partyCenter;

        BattleProgress _progress;
        IWalkArea _area;
        const float DoorArriveRadius = 1.1f, DoorStayRadius = 2.2f;   // 문 앞 도착 판정 / 멈춘 뒤 다시 걷기 시작하는 거리

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            world.TryGet(out _progress);
        }

        public void Tick(float dt)
        {
            _time += dt;
            if (_progress != null && _progress.Phase is BattlePhase.Map or BattlePhase.Deploy) return;   // 지도·배치 중엔 가만히
            var em = _world.EntityManager;
            // 웨이브 진행 중엔 방이 넓어도(마지막 웨이브는 문 앞에서 나옴) 거리와 상관없이 가장 가까운 적과 붙는다
            bool fighting = _progress == null || _progress.Phase == BattlePhase.Fighting;
            CollectTargets(em);
            // 방을 깨면 파티는 문으로 달려간다(문이 있는 방만)
            if (_area == null) _world.TryGet(out _area);
            bool toDoor = _progress != null && _progress.Phase == BattlePhase.Victory && _area is RectWalkArea;
            var door = toDoor ? BattleFlowSystem.DoorPosition((RectWalkArea)_area) : default;
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
                    if (toDoor && teams[i].TeamFlag == Party.Team)
                    {
                        // 문 앞 일정 범위 안에 들어오면 그 자리에 멈춘다(정확한 점을 맞추려 서로 밀며 뱅글뱅글 돌지 않게).
                        // 멈춘 뒤엔 꽤 밀려나야 다시 걷는다
                        int k = entities[i].Index & 3;
                        var spot = new Vec2(door.X - 0.9f - (k & 1) * 0.9f, door.Y - 0.3f - (k >> 1) * 0.7f);
                        float reach = ai.State == CombatAIState.Idle ? DoorStayRadius : DoorArriveRadius;
                        if ((spot - self).Magnitude > reach)
                        {
                            MoveIfChanged(ref buf, states[i], (spot - self).Normalized);
                            ai.State = CombatAIState.Follow;
                        }
                        else
                        {
                            if (cur.Facing != Facing2D.Right) { buf.Add(CharacterIntent.Move(Facing2D.Right)); buf.Add(CharacterIntent.Stop()); }
                            else StopIfMoving(ref buf, cur);
                            ai.State = CombatAIState.Idle;
                        }
                        continue;
                    }
                    bool hasTarget = TryTaunter(em, entities[i], self, out var target, out float dist)
                                  || TryFindNearest(entities[i], teams[i].TeamFlag, self, out target, out dist);

                    // 적이 남아 있으면 거리와 상관없이 찾아가 싸우고(맵 전체), 없을 때만 배치 칸으로 돌아가 선다
                    if (!hasTarget && em.TryGetComponent<FormationComponent>(entities[i], out var form) && form.Active)
                    {
                        HoldAnchor(ref ai, ref buf, states[i], self, form.Anchor);
                        continue;
                    }

                    if (!hasTarget || (!fighting && dist > ai.AggroRange))
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

        // 배치 칸으로 돌아가 적 쪽(오른쪽)을 보고 선다
        static void HoldAnchor(ref CombatAIComponent ai, ref CharacterIntentBuffer buf, in StateComponent state, Vec2 self, Vec2 anchor)
        {
            var to = anchor - self;
            float limit = ai.State == CombatAIState.Follow ? 0.1f : 0.35f;   // 가는 중이면 거의 닿을 때까지, 서 있으면 조금 밀려도 그대로
            if (to.Magnitude > limit)
            {
                MoveIfChanged(ref buf, state, to.Normalized);
                ai.State = CombatAIState.Follow;
                return;
            }
            var cur = state.Current;
            if (cur.Facing != Facing2D.Right)
            {
                buf.Add(CharacterIntent.Move(Facing2D.Right));
                buf.Add(CharacterIntent.Stop());
            }
            else StopIfMoving(ref buf, cur);
            ai.State = CombatAIState.Idle;
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
                    bool pile = !statuses[i].IsAlive && Revive.IsDowned(em, entities[i]);
                    if (!statuses[i].IsAlive && !pile) continue;
                    _targets.Add(new Target { E = entities[i], Team = teams[i].TeamFlag, Pos = transforms[i].Position, Pile = pile });
                }
            }
        }

        // 도발당한 동안은 도발한 쪽(살아 있으면)을 노린다
        bool TryTaunter(IEntityManager em, Entity self, Vec2 selfPos, out Target best, out float dist)
        {
            best = default;
            dist = 0f;
            if (!em.TryGetComponent<TauntedComponent>(self, out var taunt) || taunt.Remaining <= 0f) return false;
            foreach (var t in _targets)
            {
                if (t.E != taunt.Taunter) continue;
                best = t;
                dist = (t.Pos - selfPos).Magnitude;
                return true;
            }
            return false;
        }

        bool TryFindNearest(Entity self, TeamFlag myTeam, Vec2 selfPos, out Target best, out float bestDist)
        {
            best = default;
            bestDist = float.MaxValue;
            bool found = false, foundPile = false;
            foreach (var t in _targets)
            {
                if (t.E == self || t.Team == myTeam || t.Team == TeamFlag.None) continue;
                float d = (t.Pos - selfPos).Magnitude;   // 바닥 평면 거리
                // 살아 있는 적이 뼈 무더기보다 먼저
                if (found && !foundPile && t.Pile) continue;
                if ((foundPile && !t.Pile) || d < bestDist) { bestDist = d; best = t; found = true; foundPile = t.Pile; }
            }
            return found;
        }
    }
}
