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
        // 씬에 배치된 오브젝트를 어떤 레시피로 스폰할지. "Character"는 플레이어 입력을 받는다.
        [SerializeField] string recipeName = "Character";

        public bool TryGetRecipe(IWorld world, out RecipeId id)
        {
            if (!world.TryGet(out IRecipeTable recipeTable) || !recipeTable.TryGetId(recipeName, out id))
            {
                Debug.LogWarning($"[CharacterCoreBehaviour] {name}: 레시피 '{recipeName}'를 찾지 못함");
                id = RecipeId.Default;
                return false;
            }
            return true;
        }
    }
}