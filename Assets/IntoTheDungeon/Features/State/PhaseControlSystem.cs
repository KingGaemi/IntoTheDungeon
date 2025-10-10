using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;

namespace IntoTheDungeon.Features.Status
{
    public class PhaseControlSystem : GameSystem, ITick
    {
      
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(ActionPhaseComponent),
                                                                 typeof(StateComponent),
                                                                 typeof(StatusComponent)))
            {
                var actions = chunk.GetComponentArray<ActionPhaseComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var state = ref states[i];
                    ref var action = ref actions[i];
                    var status = statuses[i];
                    if (state.Current.Action == ActionState.Attack && action.ActionPhase == ActionPhase.None)
                    {
                        // Attack 시작
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
                    }                   
                }
            }
        }
        

        private void NotifyPhaseChange(Entity entity, ActionPhaseComponent action, float phaseTime)
        {
            var receiver = _world.ManagedStore.GetManagedComponent<EventReceiver>(entity);
            if (receiver == null)
                return;

            switch (action.ActionPhase)
            {     
                case ActionPhase.None:
                    break;
                case ActionPhase.Windup:
                    break;
                case ActionPhase.Active:
                    break;
                case ActionPhase.Recovery:
                    break;
            }
            
        }
    }
}
