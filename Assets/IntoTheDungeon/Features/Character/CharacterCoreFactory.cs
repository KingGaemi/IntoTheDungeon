using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Features.Character
{
    [CreateAssetMenu(fileName = "CharacterCoreFactory", menuName = "IntoTheDungeon/Recipes/CharacterCore")]
    public sealed class CharacterCoreFactory : ScriptableObject, IEntityRecipeFactory
    {
        public RecipeId RecipeId { get; }
        [SerializeField] int defaultMaxHp = 100;
        [SerializeField] float defaultMoveSpd = 5f;
        [SerializeField] float defaultAtkSpd = 1.0f;

        public IEntityRecipe Create(in SpawnParams param)
        {
            return new CharacterCoreRecipe(
                id: RecipeId,
                physHandle: 0,
                name: param.Name,
                pos: param.Pos,
                dir: param.Dir,
                maxHp: defaultMaxHp,
                movSpd: defaultMoveSpd,
                atkSpd: defaultAtkSpd
            );
        }
    }
}
