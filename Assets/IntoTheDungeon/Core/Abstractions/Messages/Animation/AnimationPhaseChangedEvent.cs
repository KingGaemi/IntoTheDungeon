using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.Abstractions.Messages.Animation
{
    public readonly struct AnimationPhaseChangedEvent
    {
        public readonly Entity Entity;
        public readonly ActionPhase Phase;
        public readonly float PhaseDuration;
        public readonly float PhaseProgress;
        public readonly float WholeProgress;

        public AnimationPhaseChangedEvent(
            Entity entity,
            ActionPhase phase,
            float phaseDuration,
            float phaseProgress,
            float wholeProgress)
        {
            Entity = entity;
            Phase = phase;
            PhaseDuration = phaseDuration;
            PhaseProgress = phaseProgress;
            WholeProgress = wholeProgress;
        }
    }
}