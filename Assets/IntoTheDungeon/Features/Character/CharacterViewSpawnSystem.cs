using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.State;
using System.Collections.Generic;

namespace IntoTheDungeon.Features.View
{
    public class CharacterViewSpawnSystem : GameSystem, ITick
    {

        public void Tick(float dt)
        {
            var toTag = new List<Entity>(256);
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(StateComponent)))
            {
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];
                    if (_world.EntityManager.HasComponent<ViewSpawnedTag>(entity))
                        continue; // 이미 뷰가 생성된 엔티티

                    // 뷰 스폰 스펙 구성
                    var data = new SpawnData
                    {
                        PrefabId = 0,
                        SortingLayerId = 0,
                        OrderInLayer = 0,
                        Behaviours = new BehaviourSpec[]
                        {
                            new BehaviourSpec
                            {
                                // TypeId = 123123,
                                // Payload = null
                            }
                        }
                    };
                    _world.EntityManager.ViewOps.Enqueue(entity, data);
                    toTag.Add(entity);
                }
            }
            foreach (var e in toTag)
                _world.EntityManager.AddComponent(e, new ViewSpawnedTag());
        }
    }

}
