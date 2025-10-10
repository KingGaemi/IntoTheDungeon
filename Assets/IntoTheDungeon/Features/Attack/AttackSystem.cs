using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;

using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.Attack
{
    public class AttackSystem : GameSystem, ITick
    {

        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(ActionPhaseComponent),
                                                                typeof(StateComponent),
                                                                typeof(StatusComponent),
                                                                typeof(TransformComponent)))
            {
                var actions = chunk.GetComponentArray<ActionPhaseComponent>();
                var states = chunk.GetComponentArray<StateComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var action = ref actions[i];
                    var status = statuses[i];
                    if (action.ReadyToAct)
                    {
                        ProjectileFactory.CreateProjectile(_world.EntityManager, entities[i], transforms[i].Position,  transforms[i].Direction);
                    }
                    
                }
            }
        }
    }
}
