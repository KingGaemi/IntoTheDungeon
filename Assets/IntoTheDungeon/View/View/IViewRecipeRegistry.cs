using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Unity.View;

namespace IntoTheDungeon.Features.View
{
    public interface IViewRecipeRegistry
    {
        bool TryGetRecipe(ViewId viewId, out IViewRecipe recipe);
        void Register(IViewRecipe recipe);
    }
}