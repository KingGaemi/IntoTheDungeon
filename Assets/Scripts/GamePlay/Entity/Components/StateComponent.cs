using System;
using UnityEngine;

using MyGame.GamePlay.Entity.Characters.Abstractions;
using MyGame.GamePlay.Entity.Abstractions;

public class StateComponent : MonoBehaviour
{
    public ControlState Control => Current.Control;
    public Facing2D FacingDir => Current.Facing;
    public ActionState Action => Current.Action;
    public MovementState Movement => Current.Movement;

    // 코드용 경량 이벤트
    public event Action<ActionState> OnActionChanged;
    public event Action<MovementState> OnMovementChanged;
    public event Action<ControlState> OnControlChanged;
    public event Action<Facing2D> OnFacingChanged;

    public bool CanMove => Current.Control is not (ControlState.Stunned or ControlState.Rooted or ControlState.Dead);
    public bool CanAttack => Current.Control is not (ControlState.Stunned or ControlState.Silenced or ControlState.Dead);

    public StateSnapshot Current { get; private set; } = new()
    {
        Action = ActionState.Idle,
        Movement = MovementState.Idle,
        Control = ControlState.Normal,
        Facing = Facing2D.Right,
        Version = 0
    };

    public ChangeMask Apply(in StateSnapshot nextSnapshot)
    => Apply(nextSnapshot.Action, nextSnapshot.Movement, nextSnapshot.Control, nextSnapshot.Facing);

    public ChangeMask Apply(
        ActionState? action = null,
        MovementState? movement = null,
        ControlState? control = null,
        Facing2D? facing = null)
    {
        var prev = Current;
        var next = prev;
        var mask = ChangeMask.None;

        // 1) 제안값 반영
        if (control.HasValue && control.Value != next.Control)
        { next.Control = control.Value; mask |= ChangeMask.Control; }

        if (action.HasValue && action.Value != next.Action)
        { next.Action = action.Value; mask |= ChangeMask.Action; }

        if (movement.HasValue && movement.Value != next.Movement)
        { next.Movement = movement.Value; mask |= ChangeMask.Movement; }

        if (facing.HasValue && facing.Value != next.Facing)
        { next.Facing = facing.Value; mask |= ChangeMask.Facing; }

        if (mask == ChangeMask.None) return ChangeMask.None;

        // 2) CC 규칙 강제(상위 게이트)
        if (EnforceControlGate(ref next))
        {
            // 무엇이 바뀌었는지 다시 계산하기보단 보수적으로 모두 변경 처리
            mask |= ChangeMask.All;
        }

        // 3) 변경 없으면 종료
        if (next.Action == prev.Action &&
            next.Movement == prev.Movement &&
            next.Control == prev.Control &&
            next.Facing == prev.Facing) return ChangeMask.None;

        // 4) 커밋
        next.Version = prev.Version + 1;
        Current = next;

        // 5) 이벤트(변경 슬라이스만)
        if (prev.Control != next.Control) OnControlChanged?.Invoke(next.Control);
        if (prev.Action != next.Action) OnActionChanged?.Invoke(next.Action);
        if (prev.Movement != next.Movement) OnMovementChanged?.Invoke(next.Movement);
        if (prev.Facing != next.Facing) OnFacingChanged?.Invoke(next.Facing);

        return mask;
    }

    public bool TryResolveNext(in StateSnapshot cur, in CharacterIntent it,
                            out StateSnapshot next, out float lockSec, out DenyReason reason)
    {
        next = cur; lockSec = 0f; reason = DenyReason.None;

        // 1) Control
        switch (cur.Control)
        {
            case ControlState.Dead: reason = DenyReason.Dead; return false;
            case ControlState.Stunned: reason = DenyReason.Stunned; return false;
            case ControlState.Rooted:
                if (it.Kind == IntentKind.Move) { reason = DenyReason.Rooted; return false; }
                break;
            case ControlState.Silenced:
                if (it.Kind is IntentKind.Attack or IntentKind.UseSkill)
                { reason = DenyReason.Silenced; return false; }
                break;
        }

        // 2) Action/Movement 규칙
        if (cur.Action == ActionState.Attack)
        {
            if (it.Kind is IntentKind.Attack) { reason = DenyReason.InRecovery; return false; }
            if (it.Kind is IntentKind.Move) { reason = DenyReason.InRecovery; return false; }
        }


        // 3) 통과 → next 작성
        switch (it.Kind)
        {
            case IntentKind.Move:
                next.Movement = MovementState.Move;
                if(it.Facing.HasValue)
                    next.Facing = it.Facing.Value;
                break;
            case IntentKind.Turn:
                if(it.Facing.HasValue)
                    next.Facing = it.Facing.Value;;
                break;
            case IntentKind.Stop:
                next.Movement = MovementState.Idle;  // 또는 None
                next.Action = ActionState.Idle;
                // Facing은 유지
                break;

            case IntentKind.Attack:
                next.Action = ActionState.Attack;
                break;
        }

        next.Version = cur.Version + 1;
        return true;
    }

    bool EnforceControlGate(ref StateSnapshot s)
    {
        bool changed = false;
        switch (s.Control)
        {
            case ControlState.Dead:
                if (s.Action != ActionState.Idle) { s.Action = ActionState.Idle; changed = true; }
                if (s.Movement != MovementState.Idle) { s.Movement = MovementState.Idle; changed = true; }
                break;

            case ControlState.Stunned:
                if (s.Action != ActionState.Idle) { s.Action = ActionState.Idle; changed = true; }
                if (s.Movement != MovementState.Idle) { s.Movement = MovementState.Idle; changed = true; }
                break;

            case ControlState.Rooted:
                if (s.Movement != MovementState.Idle) { s.Movement = MovementState.Idle; changed = true; }
                break;

            case ControlState.Silenced:
                // 새 공격 시작 금지는 컨트롤러에서 Intent 거부로 처리 권장.
                break;
        }
        return changed;
    }
}
