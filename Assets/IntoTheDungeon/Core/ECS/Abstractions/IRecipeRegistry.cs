using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityRecipeRegistry
    {
        void Register(RecipeId id, IEntityRecipeFactory factory);
        IEntityRecipeFactory GetFactory(RecipeId id); // 없으면 예외 또는 TryGet
        bool TryGetFactory(RecipeId id, out IEntityRecipeFactory f);
        IEnumerable<RecipeId> Ids { get; }
    }
}