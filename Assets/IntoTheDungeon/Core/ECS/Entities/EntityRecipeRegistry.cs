using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Entities
{
    public sealed class EntityRecipeRegistry : IEntityRecipeRegistry
    {
        readonly Dictionary<RecipeId, IEntityRecipeFactory> _map = new();

        public void Register(RecipeId id, IEntityRecipeFactory factory)
        {
            if (_map.TryGetValue(id, out var existing) && existing != factory)
            {
#if UNITY_EDITOR
                UnityEngine.Debug.LogError($"[EntityRecipeRegistry] RecipeId {id.Value}가 이미 {existing.GetType().Name}에 등록되어 있는데 {factory.GetType().Name}(으)로 덮어씁니다. 등록 이름이 중복되지 않았는지 확인하세요.");
#endif
            }
            _map[id] = factory;
        }

        public IEntityRecipeFactory GetFactory(RecipeId id)
            => _map.TryGetValue(id, out var f) ? f :
               throw new KeyNotFoundException($"Recipe {id.Value} not registered");

        public bool TryGetFactory(RecipeId id, out IEntityRecipeFactory f)
            => _map.TryGetValue(id, out f);

        public IEnumerable<RecipeId> Ids => _map.Keys;
    }
}
