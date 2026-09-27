
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;
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
        private readonly IRecipeTable _recipeTable;

        public EntityFactory(IWorld w, IEntityRecipeRegistry r)
        {
            _world = w; _registry = r;
            if (!w.TryGet(out _nameTable))
                throw new System.InvalidOperationException(
                    "INameTable service not registered. Register it before creating EntityFactory.");
            if (!w.TryGet(out _recipeTable))
                throw new System.InvalidOperationException(
                    "IRecipeTable service not registered. Register it before creating EntityFactory.");
        }

        public Entity Spawn(RecipeId id, in SpawnSpec p)
        {
            if (!_registry.TryGetFactory(id, out var f))
                throw new KeyNotFoundException($"Recipe not found: {id.Value}");

            var recipe = f.Create(in p, id);   // ← id 전달
            var e = _world.EntityManager.CreateEntity();
            recipe.Apply(_world.EntityManager, e);
            return e;
        }

        public bool TrySpawn(RecipeId id, in SpawnSpec spec, out Entity e)
        {
            e = Entity.Null;
            if (!_registry.TryGetFactory(id, out var f))
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogError($"[TrySpawn 실패] 팩토리를 찾을 수 없습니다! RecipeId: {id}({_recipeTable.GetName(id)}), Name: {spec.Name}");
#endif
                return false;
            }

            var em = _world.EntityManager;
            e = em.CreateEntity();

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[TrySpawn 진행] 1. 엔티티 발급: [{e.Index}] | RecipeId: {id}({_recipeTable.GetName(id)}) | 팩토리: {f.GetType().Name} | Name: {spec.Name}");
#endif

            // 공통 기본
            em.AddComponent(e, new InformationComponent { NameId = _nameTable.GetId(spec.Name), RecipeId = id, SceneLinkId = spec.SceneLinkId });
            em.AddComponent(e, new TransformComponent { Position = spec.Pos, Direction = spec.Dir });

            // 물리 컴포넌트
            if (spec.PhysHandle.Index >= 0)
            {
                em.AddComponent(e, new PhysicsBodyRef { Handle = spec.PhysHandle, Initialized = true });
#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[TrySpawn 진행] 2. 물리 적용: 재활용 Handle({spec.PhysHandle.Index}) 할당");
#endif
            }
            else if (f.HasPhys)
            {
                em.AddComponent(e, new PhysicsBodyRef { Handle = spec.PhysHandle });
#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[TrySpawn 진행] 2. 물리 적용: 신규 Handle 필요 (-1 초기화)");
#endif
            }

            // 레시피 기본 컴포넌트
            var recipe = f.Create(in spec, id);   // ← id 전달
            recipe.Apply(em, e);
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[TrySpawn 진행] 3. 레시피 적용: {recipe.GetType().Name} 컴포넌트 구성 완료");
#endif

            // 초기화 페이로드 적용
            if (spec.Inits != null)
            {
                int initCount = 0;
                foreach (var init in spec.Inits)
                {
                    init.Apply(_world, e);
                    initCount++;
                }
#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[TrySpawn 진행] 4. 페이로드 적용: {initCount}개의 Init 덮어쓰기 완료");
#endif
            }

            // 뷰 마커(필요시)
            if (f.HasView)
            {
                var ov = spec.ViewOverride;
                em.AddComponent(e, new ViewMarker
                {
                    SortingLayerId = ov?.SortingLayerId ?? -1,
                    OrderInLayer = ov?.OrderInLayer ?? -1
                });
#if UNITY_EDITOR
                UnityEngine.Debug.Log($"[TrySpawn 진행] 5. 뷰 마커 적용: SortingLayer({ov?.SortingLayerId ?? -1}), Order({ov?.OrderInLayer ?? -1})");
#endif
            }

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"<color=green>[TrySpawn 완료]</color> 엔티티 [{e.Index}] ({spec.Name}) 최종 스폰 성공!");
#endif
            return true;
        }
    }
}