using IntoTheDungeon.Unity.Bridge.View.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View
{
    public interface IRecipeBindable
    {
        void Bind(IViewRecipe recipe);
    }
}