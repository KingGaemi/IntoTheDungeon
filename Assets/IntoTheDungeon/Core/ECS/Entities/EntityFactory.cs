
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Physics.Abstractions;

namespace IntoTheDungeon.Core.ECS.Entities
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

        public bool TrySpawn(RecipeId id, in SpawnSpec p, out Entity e)
        {
            e = Entity.Null;
            if (!_registry.TryGetFactory(id, out var f)) return false;

            var em = _world.EntityManager;
            e = em.CreateEntity();

            // 공통 기본
            em.AddComponent(e, new TransformComponent { Position = p.Pos, Direction = p.Dir });
            if (p.PhysHandle >= 0) em.AddComponent(e, new PhysicsBodyRef { Handle = p.PhysHandle });
            em.AddComponent(e, new InformationComponent { NameId = _nameTable.GetId(p.Name), RecipeId = id });

            // 레시피 기본 컴포넌트
            var recipe = f.Create(in p);
            recipe.Apply(em, e);

            // 초기화 페이로드 적용
            if (p.Inits != null)
                foreach (var init in p.Inits)
                    init.Apply(_world, e);

            // 뷰 마커(필요시)
            if (f.HasView)
            {
                var ov = p.ViewOverride;
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