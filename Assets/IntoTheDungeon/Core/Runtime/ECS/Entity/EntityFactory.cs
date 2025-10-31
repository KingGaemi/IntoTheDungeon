
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Physics.Abstractions;

namespace IntoTheDungeon.Core.Runtime.ECS
{
    public sealed class EntityFactory : IEntityFactory
    {
        private readonly IWorld _world;
        private readonly IEntityRecipeRegistry _registry;

        private readonly INameTable _nameTable;

        public EntityFactory(IWorld w, IEntityRecipeRegistry r)
        {
            _world = w; _registry = r;
            if (!w.TryGet(out _nameTable))
                throw new System.InvalidOperationException(
                    "INameTable service not registered. Register it before creating EntityFactory.");

        }

        public Entity Spawn(RecipeId id, in SpawnSpec p)
        {
            if (!_registry.TryGetFactory(id, out var f))
                throw new KeyNotFoundException($"Recipe not found: {id.Value}");

            var recipe = f.Create(in p);
            var e = _world.EntityManager.CreateEntity();
            recipe.Apply(_world.EntityManager, e);
            return e;
        }

        public bool TrySpawn(RecipeId id, in SpawnSpec spec, out Entity e)
        {
            e = Entity.Null;
            if (!_registry.TryGetFactory(id, out var f)) return false;

            var em = _world.EntityManager;
            e = em.CreateEntity();

            // 공통 기본
            em.AddComponent(e, new InformationComponent { NameId = _nameTable.GetId(spec.Name), RecipeId = id, SceneLinkId = spec.SceneLinkId });
            em.AddComponent(e, new TransformComponent { Position = spec.Pos, Direction = spec.Dir });
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"{spec.Pos}, {spec.Dir}");
#endif
            if (spec.PhysHandle.Index >= 0)
            {
                em.AddComponent(e, new PhysicsBodyRef { Handle = spec.PhysHandle, Initialized = true });

            }
            else if (f.HasPhys)
            {
                // 스폰을 통해서 PhysHandle이 들어올 경우 재활용을 통해 할당하면됨, 만약 -1이거나 펙토리에서 Phys를 요구할 경우 -1로 초기화 된 핸들을 부여
                em.AddComponent(e, new PhysicsBodyRef { Handle = spec.PhysHandle });


#if UNITY_EDITOR
                // UnityEngine.Debug.Log($"{e.Index}, spec.PhysHandle = {spec.PhysHandle}");
#endif
            }

            // 레시피 기본 컴포넌트
            var recipe = f.Create(in spec);
            recipe.Apply(em, e);

            // 초기화 페이로드 적용
            if (spec.Inits != null)
                foreach (var init in spec.Inits)
                    init.Apply(_world, e);

            // 뷰 마커(필요시)
            if (f.HasView)
            {
                var ov = spec.ViewOverride;
                em.AddComponent(e, new ViewMarker
                {
                    SortingLayerId = ov?.SortingLayerId ?? -1,
                    OrderInLayer = ov?.OrderInLayer ?? -1
                });
            }


            return true;
        }
    }
}