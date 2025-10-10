using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Status
{
    public sealed class AnimationSyncSystem : GameSystem, ITick
{
    public void Tick(float dt)
    {
        foreach (var ch in _world.EntityManager.GetChunks(
            typeof(ActionPhaseComponent), typeof(AnimationSyncComponent)))
        {
            var act  = ch.GetComponentArray<ActionPhaseComponent>();
            var sync = ch.GetComponentArray<AnimationSyncComponent>();

            for (int i = 0; i < ch.Count; i++)
            {
                ref var a = ref act[i];
                ref var s = ref sync[i];

                // 페이즈 변경 감지
                if (s.CurrentPhase != a.ActionPhase)
                {
                    s.CurrentPhase  = a.ActionPhase;
                    s.PhaseDuration = a.GetCurrentPhaseDuration();
                    s.DirtyFlag     = 1;
                }
                else
                {
                    // 동일 페이즈인데 길이 변경(속도/버프 등) 되었을 때도 알림
                    var newDur = a.GetCurrentPhaseDuration();
                    if (newDur != s.PhaseDuration) { s.PhaseDuration = newDur; s.DirtyFlag = 1; }
                }

                // 진행도 업데이트
                s.PhaseProgress = Mathx.Min(1f, Mathx.Max(0f, a.GetPhaseProgress()));
                s.WholeProgress = Mathx.Min(1f, Mathx.Max(0f, a.GetWholeProgress()));
                s.WholeDuration = a.WholeDuration;
            }
        }
    }
}

}
