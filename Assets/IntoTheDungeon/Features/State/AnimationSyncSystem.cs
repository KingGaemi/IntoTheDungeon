using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Features.State;

namespace IntoTheDungeon.Features.Status
{
    public class AnimationSyncSystem : GameSystem, ITick
    {
        public override int Priority => 650;  // PhaseControlSystem 이후

        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(ActionPhaseComponent),
                typeof(AnimationSyncComponent)))
            {
                var actions = chunk.GetComponentArray<ActionPhaseComponent>();
                var syncs = chunk.GetComponentArray<AnimationSyncComponent>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var action = ref actions[i];
                    ref var sync = ref syncs[i];

                    // Phase 변경 감지
                    if (sync.CurrentPhase != action.ActionPhase)
                    {
                        sync.CurrentPhase = action.ActionPhase;
                        sync.PhaseDuration = action.GetCurrentPhaseDuration();
                        sync.DirtyFlag = 1;
                    }
            

                    sync.PhaseProgress = action.GetPhaseProgress();
                    sync.WholeProgress = action.GetWholeProgress();
                    sync.WholeDuration = action.WholeDuration;
                }
            }
        }
    }
}
