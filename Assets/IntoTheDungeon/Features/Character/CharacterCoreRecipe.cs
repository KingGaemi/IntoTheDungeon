using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Spawning;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.Collections;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.AI;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Character
{
    public sealed class CharacterCoreRecipe : IEntityRecipe
    {
        public RecipeId Id { get; }
        readonly CharacterDefinition _def;
        static readonly System.Random s_rng = new();
        public CharacterCoreRecipe(RecipeId id, CharacterDefinition def) { Id = id; _def = def; }
        public void Apply(IEntityManager em, Entity e)
        {
            // 메인 캐릭터 없이 모두 자동전투(편은 TeamComponent로 갈림). 필살기는 파티원만 버튼으로 발동
            var ai = CombatAIComponent.Default;
            ai.AttackRange = _def.AttackRange;
            ai.MinRange = _def.MinRange;
            em.AddComponent(e, ai);
            em.AddComponent(e, _def.Skill switch
            {
                SkillPreset.PowerShot => SpecialSkillComponent.PowerShot,
                SkillPreset.TauntGuard => SpecialSkillComponent.TauntGuard,
                _ => SpecialSkillComponent.SwordWave
            });
            if (_def.BasicSkill != BasicSkillPreset.None) em.AddComponent(e, BasicSkills.Create(_def.BasicSkill));
            em.AddComponent(e, new StateComponent(ActionState.Idle));
            // 기본 스탯 → 체력·공격력·공속·이속·방어 등
            var status = new StatusComponent { IsAlive = true };
            var derived = new DerivedStatsComponent();
            StatFormula.Apply(_def.Stats, ref status, ref derived);
            if (!_def.Trait.IsNone)
            {
                _def.Trait.ApplyStats(ref status, ref derived);
                em.AddComponent(e, _def.Trait);
            }
            em.AddComponent(e, new PoisonComponent());
            em.AddComponent(e, new IntoTheDungeon.Features.Battle.FormationComponent());   // 파티원만 배치 때 켜진다
            if (_def.SlamRadius > 0f)
                em.AddComponent(e, new IntoTheDungeon.Features.Battle.BossSlamComponent
                {
                    Cooldown = _def.SlamCooldown,
                    Remaining = _def.SlamCooldown * (0.3f + 0.4f * (float)s_rng.NextDouble()),   // 여러 마리가 한꺼번에 쓰지 않게 첫 시점을 흩뿌림
                    ChargeTime = _def.SlamCharge,
                    RecoveryTime = 0.5f,
                    Radius = _def.SlamRadius,
                    DamageMultiplier = _def.SlamDamageMul,
                    StopRequired = _def.SlamStopRequired,
                    ImpactAge = -1f
                });
            if (_def.BossGem > 0 || _def.DropGem > 0)   // 번호 + 1로 들고 있다(0 = 없음)
                em.AddComponent(e, new IntoTheDungeon.Features.Battle.GemDropComponent { Gem = _def.DropGem - 1, BossGem = _def.BossGem - 1, Chance = _def.DropGemChance });
            if (_def.ReviveCharges > 0)
                em.AddComponent(e, IntoTheDungeon.Features.Battle.ReviveComponent.Create(_def.ReviveCharges, _def.ReviveStopRequired));
            em.AddComponent(e, new PrimaryStatsComponent { Base = _def.Stats, Growth = _def.Growth, Level = 1 });
            em.AddComponent(e, status);
            em.AddComponent(e, derived);
            em.AddComponent(e, new KinematicComponent());
            em.AddComponent(e, HeightMotionComponent.Default);   // 2.5D 높이(점프·띄우기)
            em.AddComponent(e, KnockbackComponent.Default);      // 피격 밀림
            em.AddComponent(e, new HitStunComponent { Resistance = derived.CcResist });   // 피격 경직(정신력 → CC 저항)
            em.AddComponent(e, BodyComponent.Create(_def.BodyRadius, _def.BodyMass, _def.Immovable, _def.HeadHeight));   // 몸 충돌(캐릭터끼리 겹치지 않게)
            em.AddComponent(e, new CharacterIntentBuffer());
            em.AddComponent(e, new ActionPhaseComponent { WindupDuration = _def.WindupDuration, RecoveryDuration = _def.RecoveryDuration });
            em.AddComponent(e, new StatusModificationQueue());
            em.AddComponent(e, new HpModificationQueue());
            em.AddComponent(e, new SpawnOutbox());
            em.AddComponent(e, new IntoTheDungeon.Core.ECS.Components.TeamComponent());
            // 판정 크기는 임시값. X = 가로 반폭, Y = 깊이 반두께(같은 줄에 있어야 맞음)
            em.AddComponent(e, new HurtboxComponent { Offset = new Vec2(0f, 0f), HalfExtents = new Vec2(0.3f, 0.25f) });
            if (_def.Ranged) em.AddComponent(e, RangedAttackComponent.Arrow);
            else
            {
                var melee = MeleeAttackComponent.Default;
                melee.CleaveBase = melee.Cleave = _def.Cleave;
                if (_def.MeleeKnockback > 0f) melee.KnockbackSpeed = _def.MeleeKnockback;
                if (_def.MeleeLaunch > 0f) melee.LaunchSpeed = _def.MeleeLaunch;
                melee.HitStun = _def.MeleeHitStun;
                em.AddComponent(e, melee);
            }
        }
    }
}
