using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.State
{
    public struct AnimationSyncComponent : IComponentData
    {
        public ActionPhase CurrentPhase;
        public float PhaseDuration;
        public float PhaseProgress;
        public float WholeDuration;
        public float WholeProgress;
        public byte DirtyFlag;  // 0 = clean, 1 = phase changed
    }
}