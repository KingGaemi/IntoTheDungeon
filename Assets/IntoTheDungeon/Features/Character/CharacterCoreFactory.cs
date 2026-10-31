using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Features.Character
{
    public sealed class CharacterCoreFactory : IEntityRecipeFactory
    {
        public bool HasView => true;
        public bool HasPhys => false;   // 2.5D: 캐릭터는 물리 엔진 대신 MotionSystem으로 이동

        [SerializeField] int defaultMaxHp = 100;
        [SerializeField] float defaultMoveSpd = 5f;
        [SerializeField] float defaultAtkSpd = 1.0f;
        [SerializeField] int defaultDamage = 10;

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
                damage: defaultDamage,
                isPlayer: _isPlayer
            );
        }
    }
}
