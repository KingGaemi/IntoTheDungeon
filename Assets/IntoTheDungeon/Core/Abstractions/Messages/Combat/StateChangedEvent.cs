using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    public readonly struct StateChangedEvent
    {
        public readonly Entity Entity;
        public readonly ChangeMask Mask;
        public readonly ActionState ActionState;      // Action → ActionState로 변경
        public readonly MovementState MovementState;  // Movement → MovementState로 변경
        public readonly ControlState ControlState;
        public readonly Facing2D Facing2D;
        public readonly uint Version;

        public StateChangedEvent(
            Entity entity,
            ChangeMask mask,
            ActionState action,
            MovementState movement,
            ControlState control,
            Facing2D facing,
            uint version)
        {
            Entity = entity;
            Mask = mask;
            ActionState = action;
            MovementState = movement;
            ControlState = control;
            Facing2D = facing;
            Version = version;
        }

        // Factory 메서드: 특정 상태만 변경
        public static StateChangedEvent Action(Entity e, ActionState action, uint ver)
            => new(e, ChangeMask.Action, action, default, default, default, ver);

        public static StateChangedEvent Movement(Entity e, MovementState movement, uint ver)
            => new(e, ChangeMask.Movement, default, movement, default, default, ver);

        public static StateChangedEvent Control(Entity e, ControlState control, uint ver)
            => new(e, ChangeMask.Control, default, default, control, default, ver);

        public static StateChangedEvent Facing(Entity e, Facing2D facing, uint ver)
            => new(e, ChangeMask.Facing, default, default, default, facing, ver);

        // 전체 상태 변경
        public static StateChangedEvent Full(
            Entity e,
            ActionState action,
            MovementState movement,
            ControlState control,
            Facing2D facing,
            uint ver)
            => new(e, ChangeMask.All, action, movement, control, facing, ver);

        // 스냅샷 기반 생성
        public static StateChangedEvent FromSnapshot(Entity e, in StateSnapshot snapshot, ChangeMask mask)
            => new(e, mask, snapshot.Action, snapshot.Movement, snapshot.Control, snapshot.Facing, snapshot.Version);

        // 유틸리티
        public bool HasAction => (Mask & ChangeMask.Action) != 0;
        public bool HasMovement => (Mask & ChangeMask.Movement) != 0;
        public bool HasControl => (Mask & ChangeMask.Control) != 0;
        public bool HasFacing => (Mask & ChangeMask.Facing) != 0;
    }

    // 상태 전환 이벤트 (Before/After 포함)
    public readonly struct StateTransitionEvent
    {
        public readonly Entity Entity;
        public readonly ChangeMask Mask;
        public readonly StateSnapshot Previous;
        public readonly StateSnapshot Current;
        public readonly DenyReason DenyReason; // 전환 실패시

        public StateTransitionEvent(
            Entity entity,
            ChangeMask mask,
            in StateSnapshot previous,
            in StateSnapshot current,
            DenyReason denyReason = DenyReason.None)
        {
            Entity = entity;
            Mask = mask;
            Previous = previous;
            Current = current;
            DenyReason = denyReason;
        }

        public bool IsSuccess => DenyReason == DenyReason.None;
        public bool IsActionChanged => (Mask & ChangeMask.Action) != 0 && Previous.Action != Current.Action;
        public bool IsMovementChanged => (Mask & ChangeMask.Movement) != 0 && Previous.Movement != Current.Movement;
        public bool IsControlChanged => (Mask & ChangeMask.Control) != 0 && Previous.Control != Current.Control;
        public bool IsFacingChanged => (Mask & ChangeMask.Facing) != 0 && !Previous.Facing.Equals(Current.Facing);
    }

    // 특정 상태 진입/이탈 이벤트
    public readonly struct ActionStateEvent
    {
        public readonly Entity Entity;
        public readonly ActionState State;
        public readonly ActionState PreviousState;
        public readonly bool IsEnter; // true: Enter, false: Exit
        public readonly uint Version;

        public ActionStateEvent(Entity entity, ActionState state, ActionState previous, bool isEnter, uint version)
        {
            Entity = entity;
            State = state;
            PreviousState = previous;
            IsEnter = isEnter;
            Version = version;
        }

        public bool IsExit => !IsEnter;
    }

    public readonly struct ControlStateEvent
    {
        public readonly Entity Entity;
        public readonly ControlState State;
        public readonly ControlState PreviousState;
        public readonly bool IsEnter;
        public readonly uint Version;
        public readonly float Duration; // CC 지속시간 (Stunned, Rooted 등)

        public ControlStateEvent(
            Entity entity,
            ControlState state,
            ControlState previous,
            bool isEnter,
            uint version,
            float duration = 0f)
        {
            Entity = entity;
            State = state;
            PreviousState = previous;
            IsEnter = isEnter;
            Version = version;
            Duration = duration;
        }

        public bool IsExit => !IsEnter;
        public bool IsCrowdControl => State is ControlState.Stunned or ControlState.Rooted or ControlState.Silenced;
    }
}