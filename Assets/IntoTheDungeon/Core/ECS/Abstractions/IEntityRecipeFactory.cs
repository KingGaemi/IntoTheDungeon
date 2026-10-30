using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityRecipeFactory
    {
        IEntityRecipe Create(in SpawnSpec spec, RecipeId requestedId);
        bool HasView { get; }
        bool HasPhys { get; }
    }
}