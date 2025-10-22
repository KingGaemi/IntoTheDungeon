using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.Runtime.ECS
{
    public sealed class NameToRecipeRegistry : INameToRecipeRegistry
    {
        readonly Dictionary<string, RecipeId> _map = new();

        public void Register(string name, RecipeId recipe)
            => _map[name] = recipe;

        public bool TryGet(string name, out RecipeId recipeId)
            => _map.TryGetValue(name, out recipeId);
    }
}