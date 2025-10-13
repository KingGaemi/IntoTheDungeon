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
                    var spec = new ViewSpawnSpec
                    {
                        PrefabId = 0,
                        SortingLayerId = 0,
                        OrderInLayer = 0,
                        Behaviours = new BehaviourSpec[]
                        {
                            new BehaviourSpec
                            {
                                TypeName = "IntoTheDungeon.Features.Character.CharacterAnimator",
                                Payload = null
                            }
                        }
                    };

                    _world.EntityManager.ViewOps.EnqueueSpawn(entity, spec);
                    toTag.Add(entity);
                }
            }
            foreach (var e in toTag)
                _world.EntityManager.AddComponent(e, new ViewSpawnedTag());
        }
    }

}
