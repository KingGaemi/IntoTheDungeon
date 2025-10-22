using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Entities
{
    public sealed class EntityRecipeRegistry : IEntityRecipeRegistry
    {
        readonly Dictionary<RecipeId, IEntityRecipeFactory> _map = new();

        public void Register(RecipeId id, IEntityRecipeFactory factory)
            => _map[id] = factory;

        public IEntityRecipeFactory GetFactory(RecipeId id)
            => _map.TryGetValue(id, out var f) ? f :
               throw new KeyNotFoundException($"Recipe {id.Value} not registered");

        public bool TryGetFactory(RecipeId id, out IEntityRecipeFactory f)
            => _map.TryGetValue(id, out f);

        public IEnumerable<RecipeId> Ids => _map.Keys;
    }
}
