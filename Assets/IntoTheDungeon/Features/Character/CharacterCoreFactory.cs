using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Character
{
    public enum SkillPreset { SwordWave, PowerShot, TauntGuard }

    /// <summary>캐릭터 종류별 정의(레시피 이름 기준): 기본 스탯 4종, 몸 크기·무게, 공격 방식(근접/원거리)·사거리, 동작 시간, 필살기.</summary>
    public struct CharacterDefinition
    {
        public string DisplayName;      // 화면에 보이는 이름(정비 화면 등)
        public float BodyRadius;
        public float BodyMass;
        public bool Immovable;
        public float HeadHeight;        // 발에서 머리 꼭대기까지(체력바 위치)

        public bool Ranged;             // true면 화살(RangedAttackComponent), false면 근접(MeleeAttackComponent)
        public float AttackRange;       // AI가 이 가로 거리 안이면 공격
        public float MinRange;          // 원거리: 이보다 가까우면 물러남
        public float WindupDuration;    // 공격 준비(이 끝에 판정·화살이 나감)
        public float RecoveryDuration;  // 공격 마무리
        public SkillPreset Skill;
        public IntoTheDungeon.Features.Attack.BasicSkillPreset BasicSkill;   // 자동으로 쓰는 기본 스킬(없으면 None)
        public bool Cleave;             // 근접 평타가 범위 공격(아니면 한 명만)
        public PrimaryStats Stats;      // 레벨 1의 힘·민첩·지능·정신력 → 체력·공격력·공속·이속 등은 StatFormula가 계산
        public PrimaryStats Growth;     // 레벨당 오르는 스탯
        public IntoTheDungeon.Features.Attack.TraitComponent Trait;   // 종류별 특성(배율·흡혈·가시·독)
        public string TraitText;        // 특성 한 줄 설명(화면 표시용)
        public float MeleeKnockback;    // 근접 평타 넉백(0이면 기본)
        public float MeleeLaunch;       // 근접 평타 띄우기(0이면 기본)
        public float MeleeHitStun;      // 근접 평타 경직(0이면 없음)
        // 강타(예고 후 범위 공격, BossSlamComponent): 힘 모으는 동안 도발로 끊을 수 있다. 반지름 0이면 없음
        public float SlamRadius, SlamCharge, SlamCooldown, SlamDamageMul;
        public int SlamStopRequired;    // 강타 요구 저지력
        // 되살아남(해골 기사, ReviveComponent): 쓰러지면 뼈 무더기가 됐다가 다시 일어난다. 무더기일 때 이 저지력 이상 필살기면 부서짐
        public int ReviveCharges, ReviveStopRequired;
        // 이 몹만 주는 잼(GemPool 번호 + 1, 0 = 없음): 보스로 나오면 BossGem 확정, 아니면 DropGem을 DropGemChance 확률로
        public int BossGem, DropGem;
        public float DropGemChance;

        public static readonly CharacterDefinition Default = new()
        {
            Trait = IntoTheDungeon.Features.Attack.TraitComponent.None,
            Stats = PrimaryStats.Default,
            Growth = PrimaryStats.DefaultGrowth,
            BodyRadius = 0.3f, BodyMass = 1f, HeadHeight = 1.2f,
            Ranged = false, AttackRange = 1f, MinRange = 0f,
            WindupDuration = 0.5f, RecoveryDuration = 0.7f,
            Skill = SkillPreset.SwordWave
        };
    }

    public sealed class CharacterCoreFactory : IEntityRecipeFactory
    {
        public bool HasView => true;
        public bool HasPhys => false;   // 2.5D: 캐릭터는 물리 엔진 대신 MotionSystem으로 이동

        readonly IReadOnlyDictionary<RecipeId, CharacterDefinition> _definitions;

        public CharacterCoreFactory(IReadOnlyDictionary<RecipeId, CharacterDefinition> definitions = null)
        {
            _definitions = definitions;
        }

        public IEntityRecipe Create(in SpawnSpec spec, RecipeId requestedId)
        {
            var def = _definitions != null && _definitions.TryGetValue(requestedId, out var d) ? d : CharacterDefinition.Default;
            return new CharacterCoreRecipe(requestedId, def);   // 요청받은 RecipeId를 그대로 사용
        }
    }
}
