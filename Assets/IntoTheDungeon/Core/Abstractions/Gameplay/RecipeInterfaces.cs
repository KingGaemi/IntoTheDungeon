using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Gameplay
{
    // ViewId → RecipeId 매핑. 예: ViewPrefabId 10 → RecipeIds.Character
    public interface IViewToRecipeResolver
    {
        bool TryResolve(ViewId viewId, out RecipeId recipeId);
    }

    // GameObject 이름 → RecipeId 매핑. 예: "Enemy_Goblin" → RecipeIds.Enemy
    public interface INameToRecipeRegistry
    {
        bool TryGet(string name, out RecipeId recipeId);
    }
}