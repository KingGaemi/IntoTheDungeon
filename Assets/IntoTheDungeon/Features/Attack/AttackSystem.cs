using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Core.ECS.Entities;

using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.Attack
{
    public class AttackSystem : GameSystem, ITick
    {
        public override int Priority => 150;



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
                    ref var action = ref actions[i];
                    var status = statuses[i];
                    if (action.ReadyToAct)
                    {
                        
                    }
                    
                }
            }
        }
    }
}