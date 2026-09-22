using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityRecipeFactory
    {
        RecipeId RecipeId { get; }
        IEntityRecipe Create(in SpawnSpec p);
        bool HasView { get; }
        bool HasPhys { get; }
    }
}