using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Character
{
    /// <summary>캐릭터 종류별 정의(레시피 이름 기준). 지금은 몸 크기·무게만, 스탯 등은 차차 여기로 옮긴다.</summary>
    public struct CharacterDefinition
    {
        public float BodyRadius;
        public float BodyMass;
        public bool Immovable;

        public static readonly CharacterDefinition Default = new() { BodyRadius = 0.3f, BodyMass = 1f };
    }

    public sealed class CharacterCoreFactory : IEntityRecipeFactory
    {
        public bool HasView => true;
        public bool HasPhys => false;   // 2.5D: 캐릭터는 물리 엔진 대신 MotionSystem으로 이동

        const int DefaultMaxHp = 100;
        const float DefaultMoveSpd = 5f;
        const float DefaultAtkSpd = 1.0f;
        const int DefaultDamage = 10;

        readonly IReadOnlyDictionary<RecipeId, CharacterDefinition> _definitions;

        public CharacterCoreFactory(IReadOnlyDictionary<RecipeId, CharacterDefinition> definitions = null)
        {
            _definitions = definitions;
        }

        public IEntityRecipe Create(in SpawnSpec spec, RecipeId requestedId)
        {
            var def = _definitions != null && _definitions.TryGetValue(requestedId, out var d) ? d : CharacterDefinition.Default;
            return new CharacterCoreRecipe(
                id: requestedId,   // ← 요청받은 RecipeId를 그대로 사용
                maxHp: DefaultMaxHp,
                movSpd: DefaultMoveSpd,
                atkSpd: DefaultAtkSpd,
                damage: DefaultDamage,
                body: BodyComponent.Create(def.BodyRadius, def.BodyMass, def.Immovable)
            );
        }
    }
}
