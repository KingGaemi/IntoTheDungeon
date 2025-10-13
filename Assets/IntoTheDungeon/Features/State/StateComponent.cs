
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Abstractions;


namespace IntoTheDungeon.Features.State
{

    public struct StateComponent : IComponentData
    {
        public StateSnapshot Current;

        public StateComponent(ActionState actionState = ActionState.Idle)
        {
            Current.Action = actionState;
            Current.Movement = MovementState.Idle;
            Current.Control = ControlState.Normal;
            Current.Facing = Facing2D.Right;
            Current.Version = 0;
        }
        public readonly float FacingToSign() => Current.Facing.ToSign();
        public bool IsMoving => Current.Movement is MovementState.Move or MovementState.Walk or MovementState.Run;
        // 편의 계산 프로퍼티(읽기 전용)는 허용
        public bool CanMove => Current.Control is not (ControlState.Stunned or ControlState.Rooted or ControlState.Dead);
        public bool CanAttack => Current.Control is not (ControlState.Stunned or ControlState.Silenced or ControlState.Dead);

    }
}
