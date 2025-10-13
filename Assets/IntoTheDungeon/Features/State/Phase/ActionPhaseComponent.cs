using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.State
{
    public struct ActionPhaseComponent : IComponentData
    {
        public ActionPhase ActionPhase;
        public float WindupDuration;
        public float ActiveDuration;
        public float RecoveryDuration;
        public float CooldownDuration;
        public bool ReadyToAct;
        public bool Activated;
        public readonly float WholeDuration => WindupDuration + ActiveDuration + RecoveryDuration;
        public float PhaseTimer;
        public bool DirtyPhase;
        public static readonly ActionPhaseComponent Default = new()
        {
            ActionPhase = ActionPhase.None,
            WindupDuration = 0.5f,
            ActiveDuration = 0f,
            RecoveryDuration = 0.5f,
            CooldownDuration = 0f,
            ReadyToAct = false,
            Activated = false,
            DirtyPhase = false,
            PhaseTimer = 0f
        };


        public void AdvancePhase()
        {

            ActionPhase = ActionPhase switch
            {
                ActionPhase.None => ActionPhase.Queued,
                ActionPhase.Queued => ActionPhase.Windup,
                ActionPhase.Windup => ActionPhase.Active,
                ActionPhase.Active => ActionPhase.Recovery,
                ActionPhase.Recovery => ActionPhase.Cooldown,
                ActionPhase.Cooldown => ActionPhase.None,
                _ => ActionPhase.None
            };

            PhaseTimer = 0f;  // 증가 방식이므로 0으로 리셋
            DirtyPhase = true;
            // UnityEngine.Debug.Log($"[Action] Phase Advanced: {before} => {ActionPhase}");
        }

        public readonly float GetCurrentPhaseDuration()
        {
            return ActionPhase switch
            {
                ActionPhase.Windup => WindupDuration,
                ActionPhase.Active => ActiveDuration,
                ActionPhase.Recovery => RecoveryDuration,
                ActionPhase.Cooldown => CooldownDuration,
                _ => 0f
            };
        }

        public readonly bool IsPhaseComplete()
        {
            return PhaseTimer >= GetCurrentPhaseDuration();
        }

        // 추가: 진행률 헬퍼
        public readonly float GetPhaseProgress()
        {
            float duration = GetCurrentPhaseDuration();
            return duration > 0f ? PhaseTimer / duration : 1f;
        }

        public readonly float GetWholeProgress()
        {
            float wholeDuration = WholeDuration;
            if (wholeDuration <= 0f) return 1f;

            // 이전 Phase들의 시간 + 현재 PhaseTimer
            float elapsedTotal = ActionPhase switch
            {
                ActionPhase.Windup => PhaseTimer,
                ActionPhase.Active => WindupDuration + PhaseTimer,
                ActionPhase.Recovery => WindupDuration + ActiveDuration + PhaseTimer,
                ActionPhase.Cooldown => wholeDuration,
                _ => 0f
            };

            return elapsedTotal / wholeDuration;
        }
    }
}