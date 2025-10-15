using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    /// <summary>
    /// 상태 변경 이벤트 (View 동기화용)
    /// </summary>
    public readonly struct StateChangedEvent
    {
        public readonly Entity Entity;
        public readonly ChangeMask Mask;
        public readonly StateSnapshot Snapshot;

        public StateChangedEvent(Entity entity, ChangeMask mask, in StateSnapshot snapshot)
        {
            Entity = entity;
            Mask = mask;
            Snapshot = snapshot;
        }

        // Factory 메서드
        public static StateChangedEvent FromSnapshot(Entity e, in StateSnapshot snapshot, ChangeMask mask)
            => new(e, mask, snapshot);

        // 편의 접근자
        public ActionState ActionState => Snapshot.Action;
        public MovementState MovementState => Snapshot.Movement;
        public ControlState ControlState => Snapshot.Control;
        public Facing2D Facing => Snapshot.Facing;
        public uint Version => Snapshot.Version;

        // 변경 플래그
        public bool HasAction => (Mask & ChangeMask.Action) != 0;
        public bool HasMovement => (Mask & ChangeMask.Movement) != 0;
        public bool HasControl => (Mask & ChangeMask.Control) != 0;
        public bool HasFacing => (Mask & ChangeMask.Facing) != 0;
    }

    /// <summary>
    /// 상태 전환 이벤트 (로깅/분석용)
    /// </summary>
    public readonly struct StateTransitionEvent
    {
        public readonly Entity Entity;
        public readonly StateSnapshot Previous;
        public readonly StateSnapshot Current;
        public readonly ChangeMask Mask;
        public readonly DenyReason DenyReason;

        public StateTransitionEvent(
            Entity entity,
            in StateSnapshot previous,
            in StateSnapshot current,
            ChangeMask mask,
            DenyReason denyReason = DenyReason.None)
        {
            Entity = entity;
            Previous = previous;
            Current = current;
            Mask = mask;
            DenyReason = denyReason;
        }

        public bool IsSuccess => DenyReason == DenyReason.None;
        public bool IsDenied => DenyReason != DenyReason.None;

        public bool ActionChanged => (Mask & ChangeMask.Action) != 0;
        public bool MovementChanged => (Mask & ChangeMask.Movement) != 0;
        public bool ControlChanged => (Mask & ChangeMask.Control) != 0;
        public bool FacingChanged => (Mask & ChangeMask.Facing) != 0;
    }

    /// <summary>
    /// Action 상태 진입/이탈 (애니메이션/사운드용)
    /// </summary>
    public readonly struct ActionStateEvent
    {
        public readonly Entity Entity;
        public readonly ActionState State;
        public readonly ActionState Previous;
        public readonly uint Version;

        public ActionStateEvent(Entity entity, ActionState state, ActionState previous, uint version)
        {
            Entity = entity;
            State = state;
            Previous = previous;
            Version = version;
        }

        public bool IsIdle => State == ActionState.Idle;
        public bool IsAttack => State == ActionState.Attack;
        public bool WasIdle => Previous == ActionState.Idle;
    }

    /// <summary>
    /// Control 상태 진입/이탈 (CC 효과용)
    /// </summary>
    public readonly struct ControlStateEvent
    {
        public readonly Entity Entity;
        public readonly ControlState State;
        public readonly ControlState Previous;
        public readonly uint Version;

        public ControlStateEvent(Entity entity, ControlState state, ControlState previous, uint version)
        {
            Entity = entity;
            State = state;
            Previous = previous;
            Version = version;
        }

        public bool IsNormal => State == ControlState.Normal;
        public bool IsCrowdControl => State is ControlState.Stunned or ControlState.Rooted or ControlState.Silenced;
        public bool IsDead => State == ControlState.Dead;

        public bool EnteredCC => !WasCC && IsCrowdControl;
        public bool ExitedCC => WasCC && !IsCrowdControl;

        private bool WasCC => Previous is ControlState.Stunned or ControlState.Rooted or ControlState.Silenced;
    }

    /// <summary>
    /// Movement 상태 변경 (물리/이동용)
    /// </summary>
    public readonly struct MovementStateEvent
    {
        public readonly Entity Entity;
        public readonly MovementState State;
        public readonly MovementState Previous;
        public readonly uint Version;

        public MovementStateEvent(Entity entity, MovementState state, MovementState previous, uint version)
        {
            Entity = entity;
            State = state;
            Previous = previous;
            Version = version;
        }

        public bool IsIdle => State == MovementState.Idle;
        public bool IsMoving => State == MovementState.Move;
        public bool WasMoving => Previous == MovementState.Move;

        public bool StartedMoving => IsMoving && !WasMoving;
        public bool StoppedMoving => !IsMoving && WasMoving;
    }
}