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

namespace IntoTheDungeon.Features.Character
{
    public sealed class CharacterCoreRecipe : IEntityRecipe
    {
        public RecipeId Id { get; }
        readonly int _maxHp, _damage; readonly float _movSpd, _atkSpd;
        readonly CharacterDefinition _def;
        public CharacterCoreRecipe(RecipeId id,
            int maxHp, float movSpd, float atkSpd, int damage, CharacterDefinition def)
        { Id = id; _maxHp = maxHp; _movSpd = movSpd; _atkSpd = atkSpd; _damage = damage; _def = def; }
        public void Apply(IEntityManager em, Entity e)
        {
            // 메인 캐릭터 없이 모두 자동전투(편은 TeamComponent로 갈림). 필살기는 파티원만 버튼으로 발동
            var ai = CombatAIComponent.Default;
            ai.AttackRange = _def.AttackRange;
            ai.MinRange = _def.MinRange;
            em.AddComponent(e, ai);
            em.AddComponent(e, _def.Skill == SkillPreset.PowerShot ? SpecialSkillComponent.PowerShot : SpecialSkillComponent.SwordWave);
            em.AddComponent(e, new StateComponent(ActionState.Idle));
            em.AddComponent(e, new StatusComponent
            {
                MaxHp = _maxHp,
                CurrentHp = _maxHp,
                MovementSpeed = _movSpd,
                AttackSpeed = _atkSpd,
                Damage = _damage,
                IsAlive = true
            });
            em.AddComponent(e, new KinematicComponent());
            em.AddComponent(e, HeightMotionComponent.Default);   // 2.5D 높이(점프·띄우기)
            em.AddComponent(e, KnockbackComponent.Default);      // 피격 밀림
            em.AddComponent(e, HitStunComponent.Default);        // 피격 경직
            em.AddComponent(e, BodyComponent.Create(_def.BodyRadius, _def.BodyMass, _def.Immovable));   // 몸 충돌(캐릭터끼리 겹치지 않게)
            em.AddComponent(e, new CharacterIntentBuffer());
            em.AddComponent(e, new ActionPhaseComponent { WindupDuration = _def.WindupDuration, RecoveryDuration = _def.RecoveryDuration });
            em.AddComponent(e, new StatusModificationQueue());
            em.AddComponent(e, new HpModificationQueue());
            em.AddComponent(e, new SpawnOutbox());
            em.AddComponent(e, new IntoTheDungeon.Core.ECS.Components.TeamComponent());
            // 판정 크기는 임시값. X = 가로 반폭, Y = 깊이 반두께(같은 줄에 있어야 맞음)
            em.AddComponent(e, new HurtboxComponent { Offset = new Vec2(0f, 0f), HalfExtents = new Vec2(0.3f, 0.25f) });
            if (_def.Ranged) em.AddComponent(e, RangedAttackComponent.Arrow);
            else em.AddComponent(e, MeleeAttackComponent.Default);
        }
    }
}
