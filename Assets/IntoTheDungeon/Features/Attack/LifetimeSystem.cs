using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;

namespace IntoTheDungeon.Features.Attack
{
    public sealed class LifetimeSystem : GameSystem, ITick
    {
        readonly List<Entity> _expired = new(16);

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _expired.Clear();

            foreach (var chunk in em.GetChunks(typeof(LifetimeComponent)))
            {
                var lifetimes = chunk.GetComponentArray<LifetimeComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var lt = ref lifetimes[i];
                    lt.Remaining -= dt;
                    if (lt.Remaining <= 0f)
                        _expired.Add(entities[i]);
                }
            }

            foreach (var e in _expired)
                em.DestroyEntity(e);
        }
    }
}
