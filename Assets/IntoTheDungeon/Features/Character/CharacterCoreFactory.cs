using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Features.Character
{
    public sealed class CharacterCoreFactory : IEntityRecipeFactory
    {
        [SerializeField] RecipeId id = RecipeId.Default; // 레지스트리 등록/기본값 용도로만 유지
        public RecipeId RecipeId => id;
        public bool HasView => true;
        public bool HasPhys => true;

        [SerializeField] int defaultMaxHp = 100;
        [SerializeField] float defaultMoveSpd = 5f;
        [SerializeField] float defaultAtkSpd = 1.0f;

        // 이 팩토리로 만들어지는 엔티티가 플레이어 입력을 받아야 하는지.
        // false면 PlayerTag를 붙이지 않아 PlayerInputSystem 쿼리에 걸리지 않는다.
        readonly bool _isPlayer;

        public CharacterCoreFactory(bool isPlayer = false)
        {
            _isPlayer = isPlayer;
        }

        public IEntityRecipe Create(in SpawnSpec spec, RecipeId requestedId)
        {
            return new CharacterCoreRecipe(
                id: requestedId,   // ← 요청받은 RecipeId를 그대로 사용
                maxHp: defaultMaxHp,
                movSpd: defaultMoveSpd,
                atkSpd: defaultAtkSpd,
                isPlayer: _isPlayer
            );
        }
    }
}
