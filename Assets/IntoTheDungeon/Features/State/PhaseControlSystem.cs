using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.Util.Maths;
using IntoTheDungeon.Features.State;

namespace IntoTheDungeon.Features.Status
{
    public class PhaseControlSystem : GameSystem, ITick
    {
        public override int Priority => 600; // 다른 시스템들 이후 실행
        
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(ActionPhaseComponent), typeof(StateComponent)))
            {
                var actions = chunk.GetComponentArray<ActionPhaseComponent>();
                var states = chunk.GetComponentArray<StateComponent>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var state = ref states[i];
                    ref var action = ref actions[i];

                    if (state.Current.Action == ActionState.Attack && action.ActionPhase == ActionPhase.None)
                    {
                        // Attack 시작
                        action.AdvancePhase();
                        action.PhaseTimer = 0f;
                    }
                    if (action.ActionPhase != ActionPhase.None)
                    {
                        action.PhaseTimer += dt;

                        if (action.IsPhaseComplete())
                        {
                            action.AdvancePhase();
                            if (action.ActionPhase == ActionPhase.None)
                            {
                                state.Current.Action = ActionState.Idle;
                            }
                        }

                    }                   
                }
            }
        }
        

        private void NotifyPhaseChange(Entity entity, ActionPhaseComponent action, float phaseTime)
        {
            var receiver = _world.EntityManager.GetManagedComponent<EventReceiver>(entity);
            if (receiver == null)
                return;

            switch (action.ActionPhase)
            {     
                case ActionPhase.None:
                    break;
                case ActionPhase.Startup:
                    break;
                case ActionPhase.Active:
                    break;
                case ActionPhase.Recovery:
                    break;
            }
            
        }
    }
}