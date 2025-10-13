using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Animation;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;

namespace IntoTheDungeon.Features.State
{
    public class PhaseControlSystem : GameSystem, ITick
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
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(ActionPhaseComponent),
                                                                 typeof(StateComponent),
                                                                 typeof(StatusComponent)))
            {
                var actions = chunk.GetComponentArray<ActionPhaseComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var state = ref states[i];
                    ref var action = ref actions[i];
                    var status = statuses[i];
                    if (state.Current.Action == ActionState.Attack && action.ActionPhase == ActionPhase.None)
                    {
                        // Attack 시작
                        action.AdvancePhase();
                        action.AdvancePhase();
                    }
                    if (action.ActionPhase != ActionPhase.None)
                    {
                        action.PhaseTimer += dt * status.AttackSpeed;

                        if (action.IsPhaseComplete())
                        {
                            action.AdvancePhase();

                            if (action.ActionPhase == ActionPhase.None)
                            {
                                state.Current.Action = ActionState.Idle;
                                action.ReadyToAct = false;
                            }
                            else if (action.ActionPhase == ActionPhase.Active)
                            {
                                action.ReadyToAct = true;
                            }
                        }
                        if (action.DirtyPhase)
                        {
                            PublishPhaseEvent(entities[i], ref action);
                            action.DirtyPhase = false; // 플래그 클리어
                        }
                    }
                }
            }
        }
        void PublishPhaseEvent(Entity entity, ref ActionPhaseComponent action)
        {
            _hub.Publish(new AnimationPhaseChangedEvent(
                entity,
                action.ActionPhase,
                action.GetCurrentPhaseDuration(),
                action.GetPhaseProgress(),
                action.GetWholeProgress()
            ));
        }

    }
}
