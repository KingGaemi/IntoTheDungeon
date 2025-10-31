using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Features.Character
{

    public sealed class CharacterCoreFactory : IEntityRecipeFactory
    {
        [SerializeField] RecipeId id = RecipeIds.Character; // 기본값
        public RecipeId RecipeId => id;
        public bool HasView => true;

        public bool HasPhys => true;

        [SerializeField] int defaultMaxHp = 100;
        [SerializeField] float defaultMoveSpd = 5f;
        [SerializeField] float defaultAtkSpd = 1.0f;
        public IEntityRecipe Create(in SpawnSpec spec)
        {
            return new CharacterCoreRecipe(
                id: RecipeId,
                maxHp: defaultMaxHp,
                movSpd: defaultMoveSpd,
                atkSpd: defaultAtkSpd
            );
        }
    }
}
