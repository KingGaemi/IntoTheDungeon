using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IRecipeTable
    {
        RecipeId GetId(string name);
        bool TryGetId(string name, out RecipeId id);
        string GetName(RecipeId id);
        int Count { get; }
    }
}
