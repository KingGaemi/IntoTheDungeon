using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Command;
public struct StateChangedEvent
{
    public IntoTheDungeon.Core.ECS.Entities.Entity Entity;
    public StateSnapshot Previous;
    public StateSnapshot Current;
    public ChangeMask Mask;
}

namespace IntoTheDungeon.Features.Character
{
    public class CharacterIntentApplySystem : GameSystem, ITick
    {
        override public int Priority => 100;
        public void Tick(float dt)
        {
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
                    ref var buffer = ref buffers[i];
                    ref var state = ref states[i];

                    // Intent가 없으면 스킵
                    if (!buffer.HasIntent)
                        continue;

                    // Intent 소비
                    var intent = buffer.Consume();

                    if (TryResolveNext(state.Current, intent, out StateSnapshot next, out DenyReason denyReason))
                    {
                        var mask = Apply(ref state, in next);

                        if (_world.EntityManager.HasManagedComponent<EventReceiver>(entities[i]))
                        {
                            var reciver = _world.EntityManager.GetManagedComponent<EventReceiver>(entities[i]);
                            reciver?.NotifyStateChange(state.Current, mask);
                        }
                    }
                }
            }

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

        private ChangeMask Apply(ref StateComponent state, in StateSnapshot next)
        {
            ref StateSnapshot current = ref state.Current;
            var mask = ChangeMask.None;

            // 변경 사항 계산
            if (current.Control != next.Control)
                mask |= ChangeMask.Control;
            if (current.Action != next.Action)
                mask |= ChangeMask.Action;
            if (current.Movement != next.Movement)
                mask |= ChangeMask.Movement;
            if (current.Facing != next.Facing)
                mask |= ChangeMask.Facing;

            if (mask == ChangeMask.None)
                return ChangeMask.None;

            // Control State 강제 규칙 적용
            var enforced = next;
            if (EnforceControlGate(ref enforced))
            {
                mask |= ChangeMask.All;
            }

            // 적용
            current = enforced;


            if (mask == ChangeMask.None)
                return ChangeMask.None;

            current = enforced;

        
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


    }
}