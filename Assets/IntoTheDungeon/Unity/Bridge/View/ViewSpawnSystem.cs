using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Unity.Bridge.View
{
    public class ViewSpawnSystem : GameSystem, ITick
    {
        IEntityViewMapRegistry _entityViewRegistry;
        IViewOpQueue _viewOpQueue;
        ILogger _log;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);


            if (!world.TryGet(out _entityViewRegistry))
            {
                Enabled = false;
                return;
            }
            if (!world.TryGet(out _log))
            {
                Enabled = false;
                return;
            }
            Enabled = world.TryGet(out _viewOpQueue);

        }

        public void Tick(float dt)
        {
            var spawned = new List<Entity>(256);
            var removeMarker = new List<Entity>(256);
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(InformationComponent), typeof(ViewMarker)))
            {
                var entities = chunk.GetEntities();
                var informs = chunk.GetComponentArray<InformationComponent>();
                var markers = chunk.GetComponentArray<ViewMarker>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    if (_world.EntityManager.HasComponent<ViewSpawnedTag>(entity))
                        continue; // 이미 뷰가 생성된 엔티티



                    ref var info = ref informs[i];
                    ref var marker = ref markers[i];

                    var recipeId = info.RecipeId;


                    var data = new ViewSpawnData
                    {
                        RecipeId = recipeId,
                        SceneLinkId = info.SceneLinkId,
                        SortingLayerId = marker.SortingLayerId,
                        OrderInLayer = marker.OrderInLayer,
                    };


                    _viewOpQueue.Enqueue(entity, data);
                    _log.Log("ViewOpsEnqueued");
                    spawned.Add(entity);
                    removeMarker.Add(entity);


                }
            }
            foreach (var e in spawned)
                _world.EntityManager.AddComponent(e, new ViewSpawnedTag());

            foreach (var e in removeMarker)
                _world.EntityManager.RemoveComponent<ViewMarker>(e);
        }
    }

}
