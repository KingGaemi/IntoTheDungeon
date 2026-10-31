using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Runtime.Event;

namespace IntoTheDungeon.Features.Command
{
    public class CharacterIntentApplySystem : GameSystem, ITick
    {
        private IEventHub _hub;
        ILogger _logger;
        int Id(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _hub))
            {
                Enabled = false;
                return;
            }
            if (!world.TryGet(out _logger))
            {
                Enabled = false;
                return;

            }
            _logger.Log($"Hub(PUB)={Id(_hub)}");
            _logger.Log($"StateChangedEvent AQN: {typeof(StateChangedEvent).AssemblyQualifiedName}");

        }
        public void Tick(float dt)
        {

            int processedCount = 0;
            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(CharacterIntentBuffer),
                typeof(StateComponent))
                )
            {
                var buffers = chunk.GetComponentArray<CharacterIntentBuffer>();
                var states = chunk.GetComponentArray<StateComponent>();
                var entities = chunk.GetEntities();
                // for 루프 + ref 사용
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var buf = ref buffers[i];
                    ref var state = ref states[i];
                    var e = entities[i];

                    // Intent가 없으면 스킵
                    while (buf.HasIntent)
                    {
                        var intent = buf.Consume();
                        var prev = state.Current;

                        if (!TryResolveNext(prev, intent, out var next, out var deny))
                        {
                            PublishTransitionDenied(e, prev, next, deny);
                            continue;
                        }

                        // 이동 방향은 스냅샷 밖에 둔다(방향만 바뀌면 이벤트 없음)
                        if (intent.Kind == IntentKind.Move) state.MoveDir = intent.Dir.Normalized;
                        else if (intent.Kind is IntentKind.Stop or IntentKind.None) state.MoveDir = Vec2.Zero;

                        // 점프: 땅에 있을 때만 높이 속도를 준다(상태 스냅샷은 바뀌지 않음)
                        if (intent.Kind == IntentKind.Jump)
                        {
                            TryJump(e);
                            continue;
                        }

                        var mask = Apply(ref state, next);
                        if (mask == ChangeMask.None) continue;

                        PublishStateChanged(e, prev, state.Current, mask);
                        PublishDerived(e, prev, state.Current, mask); // 추가
                        processedCount++;
                    }

                    buf.Clear();
                    // 여기서 버퍼 비우기


                    // 구조체니까 커밋 필요하면 SetComponent 호출
                    _world.EntityManager.SetComponent(entities[i], buf);

                }
            }
            // if (processedCount > 0)
            // _logger.Log($"[IntentApply] Processed {processedCount} intents this frame");

        }
        void TryJump(Entity e)
        {
            var em = _world.EntityManager;
            if (!em.HasComponent<HeightMotionComponent>(e) || !em.TryGetComponent<TransformComponent>(e, out var t)) return;
            ref var m = ref em.GetComponent<HeightMotionComponent>(e);
            if (m.IsAirborne(t.Height)) return;   // 공중 점프 없음
            m.VelocityZ = m.JumpSpeed;
        }

        // ============================================
        // Try: Intent → Next State 검증
        // ============================================

        private bool TryResolveNext(
            in StateSnapshot current,
            in CharacterIntent intent,
            out StateSnapshot next,
            out DenyReason reason)
        {
            next = current;
            reason = DenyReason.None;

            // 1. Control State 검증
            switch (current.Control)
            {
                case ControlState.Dead:
                    reason = DenyReason.Dead;
                    return false;

                case ControlState.Stunned:
                    reason = DenyReason.Stunned;
                    return false;

                case ControlState.Rooted:
                    if (intent.Kind is IntentKind.Move or IntentKind.Jump)
                    {
                        reason = DenyReason.Rooted;
                        return false;
                    }
                    break;

                case ControlState.Silenced:
                    if (intent.Kind == IntentKind.Attack)
                    {
                        reason = DenyReason.Silenced;
                        return false;
                    }
                    break;
            }

            // 2. Action State 검증
            // 필살기 시전 중엔 아무 행동도 받지 않는다(끝내는 건 SpecialSkillSystem)
            if (current.Action == ActionState.Skill && intent.Kind is not IntentKind.None)
            {
                reason = DenyReason.InRecovery;
                return false;
            }
            if (current.Action == ActionState.Attack)
            {
                // 공격 중에는 다른 행동 불가
                if (intent.Kind is IntentKind.Attack or IntentKind.Jump)
                {
                    reason = DenyReason.InRecovery;
                    return false;
                }
            }

            // 3. 검증 통과 → Next State 작성
            switch (intent.Kind)
            {
                case IntentKind.Move:
                    next.Movement = MovementState.Move;
                    // 위/아래로만 움직이면(가로 성분 0) 바라보는 방향은 그대로
                    if (intent.Dir.X != 0f) next.Facing = intent.Facing;
                    break;

                case IntentKind.Attack:
                    next.Action = ActionState.Attack;
                    break;

                case IntentKind.Stop:
                    next.Action = ActionState.Idle;
                    next.Movement = MovementState.Idle;
                    break;

                case IntentKind.None:
                    next.Movement = MovementState.Idle;
                    break;
            }

            return true;
        }

        // ============================================
        // Apply: State 적용 + 이벤트
        // ============================================

        private ChangeMask Apply(ref StateComponent state, StateSnapshot next)
        {
            ref StateSnapshot current = ref state.Current;

            // 1️ EnforceControlGate 먼저 (next 강제 수정)
            EnforceControlGate(ref next);

            // 2️ 수정된 next와 current 비교해서 변경사항 계산
            ChangeMask mask = CalculateMask(current, next);

            if (mask == ChangeMask.None)
                return ChangeMask.None;

            // 3️ Version 증가
            next.Version = current.Version + 1;

            // 4️ 적용
            current = next;

            return mask;
        }

        ChangeMask CalculateMask(in StateSnapshot current, in StateSnapshot next)
        {
            var mask = ChangeMask.None;

            if (current.Control != next.Control)
                mask |= ChangeMask.Control;
            if (current.Action != next.Action)
                mask |= ChangeMask.Action;
            if (current.Movement != next.Movement)
                mask |= ChangeMask.Movement;
            if (!current.Facing.Equals(next.Facing))
                mask |= ChangeMask.Facing;

            return mask;
        }

        private bool EnforceControlGate(ref StateSnapshot next)
        {
            bool changed = false;

            switch (next.Control)
            {
                case ControlState.Dead:
                case ControlState.Stunned:
                    if (next.Action != ActionState.Idle)
                    {
                        next.Action = ActionState.Idle;
                        changed = true;
                    }
                    if (next.Movement != MovementState.Idle)
                    {
                        next.Movement = MovementState.Idle;
                        changed = true;
                    }
                    break;

                case ControlState.Rooted:
                    if (next.Movement != MovementState.Idle)
                    {
                        next.Movement = MovementState.Idle;
                        changed = true;
                    }
                    break;
            }

            return changed;
        }


        void PublishStateChanged(Entity e, in StateSnapshot prev, in StateSnapshot cur, ChangeMask mask)
        {
            _hub.Publish(StateChangedEvent.FromSnapshot(e, cur, mask));
            // _logger.Log($"StateChanged PUB e={e.Index} mask={mask}");
            // _logger.Log($"PUB count={_hub.Count<StateChangedEvent>()}");

        }

        void PublishDerived(Entity e, in StateSnapshot prev, in StateSnapshot cur, ChangeMask mask)
        {
            if (mask.Has(ChangeMask.Action) && prev.Action != cur.Action)
                _hub.Publish(new ActionStateEvent(e, cur.Action, prev.Action, cur.Version));

            if (mask.Has(ChangeMask.Control) && prev.Control != cur.Control)
                _hub.Publish(new ControlStateEvent(e, cur.Control, prev.Control, cur.Version));

            if (mask.Has(ChangeMask.Movement) && prev.Movement != cur.Movement)
                _hub.Publish(new MovementStateEvent(e, cur.Movement, prev.Movement, cur.Version));
        }

        void PublishTransitionDenied(Entity entity, in StateSnapshot previous, in StateSnapshot attempted, DenyReason reason)
        {
            // 실패한 전환 이벤트 (UI 피드백용: "스턴 상태라 이동 불가" 등)
            // _hub.Publish(new StateTransitionEvent(
            //     entity,
            //     ChangeMask.None,
            //     previous,
            //     attempted,
            //     reason
            // ));
        }

    }
}
