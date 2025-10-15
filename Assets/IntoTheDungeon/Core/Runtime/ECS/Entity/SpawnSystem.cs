using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Collections;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.ECS.Spawning;
using IntoTheDungeon.Core.ECS.Systems;

namespace IntoTheDungeon.Core.Runtime.ECS
{
    public class SpawnSystem : GameSystem, ITick
    {
        IRecipeRegistry _recipes;
        IViewOpQueue _view;
        IEventHub _hub;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _recipes))
            {
                Enabled = false;
                return;
            }
            // if (!world.TryGet(out _recipes))
            // {
            //     Enabled = false;
            //     return;
            // }
            // if (!world.TryGet(out _recipes))
            // {
            //     Enabled = false;
            //     return;
            // }

        }


        public void Tick(float dt)
        {
            var toCreate = new List<IEntityRecipe>(128);
            var chunks = _world.EntityManager.GetChunks(typeof(SpawnBuffer));
            foreach (var chunk in chunks)
            {
                var buffers = chunk.GetComponentArray<SpawnBuffer>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var buf = ref buffers[i];
                    if (!buf.HasValue) continue;

                    if (_recipes.TryGetFactory(buf.Value.RecipeId, out var factory))
                        toCreate.Add(factory.Create(in buf.Value.Params));


                    buf.Clear();

                }
            }

            for (int i = 0; i < toCreate.Count; i++)
                _world.EntityManager.CreateEntity(toCreate[i]);
        }

    }
}