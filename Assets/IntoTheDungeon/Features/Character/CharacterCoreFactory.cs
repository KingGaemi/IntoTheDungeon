using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Features.Character
{
    public enum SkillPreset { SwordWave, PowerShot }

    /// <summary>캐릭터 종류별 정의(레시피 이름 기준): 몸 크기·무게, 공격 방식(근접/원거리)·사거리, 동작 시간, 필살기. 스탯은 차차 여기로 옮긴다.</summary>
    public struct CharacterDefinition
    {
        public float BodyRadius;
        public float BodyMass;
        public bool Immovable;

        public bool Ranged;             // true면 화살(RangedAttackComponent), false면 근접(MeleeAttackComponent)
        public float AttackRange;       // AI가 이 가로 거리 안이면 공격
        public float MinRange;          // 원거리: 이보다 가까우면 물러남
        public float WindupDuration;    // 공격 준비(이 끝에 판정·화살이 나감)
        public float RecoveryDuration;  // 공격 마무리
        public SkillPreset Skill;

        public static readonly CharacterDefinition Default = new()
        {
            BodyRadius = 0.3f, BodyMass = 1f,
            Ranged = false, AttackRange = 1f, MinRange = 0f,
            WindupDuration = 0.5f, RecoveryDuration = 0.7f,
            Skill = SkillPreset.SwordWave
        };
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
                def: def
            );
        }
    }
}
