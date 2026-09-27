using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.Abstractions.World
{
    public interface IGameplayAuthoring
    {
        bool TryGetRecipe(IWorld world, out RecipeId id);
    }
}