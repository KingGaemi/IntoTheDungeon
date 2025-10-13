#if false
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages.Animation;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Animation
{
    public class AnimationSyncSystem : GameSystem, ITick
    {
        IEventHub _hub;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!_world.TryGet(out _hub))
            {
                Enabled = false;
            }
        }

        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(AnimationSyncComponent),
                typeof(ActionPhaseComponent)))
            {
                var syncs = chunk.GetComponentArray<AnimationSyncComponent>();
                var phases = chunk.GetComponentArray<ActionPhaseComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var sync = ref syncs[i];
                    ref var phase = ref phases[i];

                    // Phase 진행
                    phase.PhaseTimer += dt;

                    // Phase 전환 체크
                    bool phaseChanged = UpdatePhase(ref phase, ref sync);

                    if (phaseChanged)
                    {
                        // 이벤트 발행 (Phase 변경시에만)
                        _hub.Publish(new AnimationPhaseChangedEvent(
                            entities[i],
                            sync.CurrentPhase,
                            sync.PhaseDuration,
                            sync.PhaseProgress,
                            sync.WholeProgress
                        ));

                        sync.DirtyFlag = 1;  // View용 폴백
                    }
                    else
                    {
                        sync.DirtyFlag = 0;
                    }

                    // Progress 업데이트 (매 프레임)
                    UpdateProgress(ref phase, ref sync, dt);
                }
            }
        }

        bool UpdatePhase(ref ActionPhaseComponent phase, ref AnimationSyncComponent sync)
        {
            var prevPhase = sync.CurrentPhase;

            // Phase 전환 로직
            switch (phase.ActionPhase)
            {
                case ActionPhase.Windup:
                    if (phase.PhaseTimer >= phase.WindupDuration)
                    {
                        phase.ActionPhase = ActionPhase.Active;
                        phase.PhaseTimer = 0f;
                    }
                    break;

                case ActionPhase.Active:
                    if (phase.PhaseTimer >= phase.ActiveDuration)
                    {
                        phase.ActionPhase = ActionPhase.Recovery;
                        phase.PhaseTimer = 0f;
                    }
                    break;

                case ActionPhase.Recovery:
                    if (phase.PhaseTimer >= phase.RecoveryDuration)
                    {
                        phase.ActionPhase = ActionPhase.Cooldown;
                        phase.PhaseTimer = 0f;
                    }
                    break;

                case ActionPhase.Cooldown:
                    if (phase.PhaseTimer >= phase.CooldownDuration)
                    {
                        phase.ActionPhase = ActionPhase.None;
                        phase.PhaseTimer = 0f;
                    }
                    break;
            }

            sync.CurrentPhase = phase.ActionPhase;

            return prevPhase != sync.CurrentPhase;
        }

        void UpdateProgress(ref ActionPhaseComponent phase, ref AnimationSyncComponent sync, float dt)
        {
            // Phase 진행도
            sync.PhaseDuration = phase.ActionPhase switch
            {
                ActionPhase.Windup => phase.WindupDuration,
                ActionPhase.Active => phase.ActiveDuration,
                ActionPhase.Recovery => phase.RecoveryDuration,
                ActionPhase.Cooldown => phase.CooldownDuration,
                _ => 1f
            };

            sync.PhaseProgress = sync.PhaseDuration > 0.001f
                ? Mathx.Clamp01(phase.PhaseTimer / sync.PhaseDuration)
                : 1f;

            // 전체 진행도
            sync.WholeDuration = phase.WindupDuration + phase.ActiveDuration
                + phase.RecoveryDuration + phase.CooldownDuration;

            float elapsed = phase.ActionPhase switch
            {
                ActionPhase.Windup => phase.PhaseTimer,
                ActionPhase.Active => phase.WindupDuration + phase.PhaseTimer,
                ActionPhase.Recovery => phase.WindupDuration + phase.ActiveDuration + phase.PhaseTimer,
                ActionPhase.Cooldown => phase.WindupDuration + phase.ActiveDuration + phase.RecoveryDuration + phase.PhaseTimer,
                _ => sync.WholeDuration
            };

            sync.WholeProgress = sync.WholeDuration > 0.001f
                ? Mathx.Clamp01(elapsed / sync.WholeDuration)
                : 1f;
        }
    }
}
#endif