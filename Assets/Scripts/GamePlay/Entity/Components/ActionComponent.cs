using System;
using UnityEngine;
using MyGame.GamePlay.Entity.Characters.Abstractions;

public class ActionComponent : MonoBehaviour
{
    public ActionKind Kind = ActionKind.None;
    public ActionPhase CurrentPhase = ActionPhase.None;
    public float PhaseTimeRemaining = 0f;

    // per-action durations (set from config or defaults)
    public PhaseDurations Durations;

    // events others can subscribe to
    public event Action<ActionPhase> OnPhaseEntered;
    public event Action<ActionPhase> OnPhaseExited;

    public void StartAction(ActionKind kind, PhaseDurations durations)
    {
        Kind = kind;
        Durations = durations;
        EnterPhase(ActionPhase.Queued);
    }

    void EnterPhase(ActionPhase next)
    {
        OnPhaseExited?.Invoke(CurrentPhase);
        CurrentPhase = next;
        PhaseTimeRemaining = GetDurationForPhase(next);
        OnPhaseEntered?.Invoke(next);
    }

    float GetDurationForPhase(ActionPhase p)
    {
        return p switch {
            ActionPhase.Startup => Durations.Startup,
            ActionPhase.Active  => Durations.Active,
            ActionPhase.Recovery=> Durations.Recovery,
            ActionPhase.Cooldown=> Durations.Cooldown,
            ActionPhase.Queued  => 0f,
            _ => 0f
        };
    }

    // 외부에서 강제 페이즈 전환 필요시 호출
    public void ForceEnterPhase(ActionPhase p) => EnterPhase(p);
}
