using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View.Abstractions
{
    public interface IViewRecipeRegistry
    {
        bool TryGetRecipe(ViewId viewId, out IViewRecipe recipe);
        void Register(IViewRecipe recipe);
    }
}