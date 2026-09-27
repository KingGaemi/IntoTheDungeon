using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityRecipeFactory
    {
        RecipeId RecipeId { get; }
        IEntityRecipe Create(in SpawnSpec spec, RecipeId requestedId);  // ← requestedId 매개변수 추가
        bool HasView { get; }
        bool HasPhys { get; }
    }
}