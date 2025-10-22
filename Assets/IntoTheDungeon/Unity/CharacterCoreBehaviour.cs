using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.World;
using UnityEngine;

namespace IntoTheDungeon.Unity.Behaviour
{
    [DisallowMultipleComponent]
    public sealed class CharacterCoreBehaviour : MonoBehaviour, IGameplayAuthoring
    {
        public bool TryGetRecipe(out RecipeId id)
        {
            id = RecipeIds.Character;
            return true;
        }
    }
}