using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityRecipeFactory
    {
        RecipeId RecipeId { get; }
        IEntityRecipe Create(in SpawnSpec p);
        bool HasView { get; }
    }
}