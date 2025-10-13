using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Runtime.Event;

namespace IntoTheDungeon.Features.Command
{
    public class CharacterIntentApplySystem : GameSystem, ITick
    {
        private IEventHub _hub;
        ILogger _logger;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _hub))
            {
                Enabled = false;
            }
            if (!world.TryGet(out _logger))
            {
                Enabled = false;

            }
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

                        var mask = Apply(ref state, next);
                        if (mask != ChangeMask.None)
                            PublishStateChanged(e, prev, state.Current, mask);
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
                    if (intent.Kind == IntentKind.Move)
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
            if (current.Action == ActionState.Attack)
            {
                // 공격 중에는 다른 행동 불가
                if (intent.Kind is IntentKind.Attack)
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
                    next.Facing = intent.Facing;
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

        void PublishStateChanged(Entity entity, in StateSnapshot previous, in StateSnapshot current, ChangeMask mask)
        {
            // 1. 간단한 변경 이벤트 (View 동기화용)
            _logger.Log($"[IntentApply] Publishing StateChangedEvent: Entity={entity.Index}, Mask={mask}");
            var evt = StateChangedEvent.FromSnapshot(entity, current, mask);
            _logger.Log($"[IntentApply] Event created: Movement={evt.MovementState}, HasMovement={evt.HasMovement}");

            _hub.Publish(evt);
            // 2. 상세 전환 이벤트 (로깅/분석용)
            _hub.Publish(new StateTransitionEvent(
                entity,
                mask,
                previous,
                current
            ));

            // 3. 특정 상태 진입 이벤트 (사운드/VFX용)
            if ((mask & ChangeMask.Action) != 0 && previous.Action != current.Action)
            {
                _hub.Publish(new ActionStateEvent(
                    entity,
                    current.Action,
                    previous.Action,
                    isEnter: true,
                    current.Version
                ));
            }

            if ((mask & ChangeMask.Control) != 0 && previous.Control != current.Control)
            {
                _hub.Publish(new ControlStateEvent(
                    entity,
                    current.Control,
                    previous.Control,
                    isEnter: true,
                    current.Version
                // Duration은 별도 컴포넌트에서 가져올 수 있음
                ));
            }
        }

        void PublishTransitionDenied(Entity entity, in StateSnapshot previous, in StateSnapshot attempted, DenyReason reason)
        {
            // 실패한 전환 이벤트 (UI 피드백용: "스턴 상태라 이동 불가" 등)
            _hub.Publish(new StateTransitionEvent(
                entity,
                ChangeMask.None,
                previous,
                attempted,
                reason
            ));
        }

    }
}
