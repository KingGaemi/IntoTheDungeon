using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.Abstractions.World
{
    public interface IGameplayAuthoring
    {
        bool TryGetRecipe(out RecipeId id);

    }
}