using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.Abstractions.World
{
    public interface IGameplayAuthoring
    {
        bool TryGetRecipe(out RecipeId id);

    }
}