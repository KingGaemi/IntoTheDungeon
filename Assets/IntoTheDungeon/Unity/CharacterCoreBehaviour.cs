using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;

namespace IntoTheDungeon.Unity.Behaviour
{
    [DisallowMultipleComponent]
    public sealed class CharacterCoreBehaviour : MonoBehaviour, IGameplayAuthoring
    {
        public bool TryGetRecipe(IWorld world, out RecipeId id)
        {
            if (!world.TryGet(out IRecipeTable recipeTable))
            {
                id = RecipeId.Default;
                return false;
            }

            id = recipeTable.GetId("Character");
            return true;
        }
    }
}