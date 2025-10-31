using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Physics.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View
{
    public class ViewSpawnSystem : GameSystem, ITick
    {

        IViewOpQueue _viewOpQueue;
        ILogger _log;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);

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
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(InformationComponent), typeof(ViewMarker), typeof(PhysicsBodyRef), typeof(TransformComponent)))
            {
                var entities = chunk.GetEntities();
                var informs = chunk.GetComponentArray<InformationComponent>();
                var markers = chunk.GetComponentArray<ViewMarker>();
                var bodyRefs = chunk.GetComponentArray<PhysicsBodyRef>();
                var transforms = chunk.GetComponentArray<TransformComponent>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    if (_world.EntityManager.HasComponent<ViewSpawnedTag>(entity))
                        continue; // 이미 뷰가 생성된 엔티티

                    ref var info = ref informs[i];
                    ref var marker = ref markers[i];
                    ref var bodyRef = ref bodyRefs[i];
                    ref var trans = ref transforms[i];

                    var recipeId = info.RecipeId;


                    var data = new ViewSpawnData
                    {
                        RecipeId = recipeId,
                        PhysicsHandle = bodyRef.Handle,
                        SceneLinkId = info.SceneLinkId,
                        SortingLayerId = marker.SortingLayerId,
                        OrderInLayer = marker.OrderInLayer,
                    };


                    _viewOpQueue.Enqueue(entity, data);

                    var transData = new TransformData { X = trans.Position.X, Y = trans.Position.Y, RotDeg = trans.Rotation };

                    _viewOpQueue.Enqueue(entity, transData);
                    // _log.Log("ViewOpsEnqueued");
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
