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

namespace IntoTheDungeon.Features.Character
{
    public sealed class CharacterCoreRecipe : IEntityRecipe
    {
        public RecipeId Id { get; }
        readonly int _maxHp, _damage; readonly float _movSpd, _atkSpd;
        readonly bool _isPlayer;
        public CharacterCoreRecipe(RecipeId id,
            int maxHp, float movSpd, float atkSpd, int damage, bool isPlayer = false)
        { Id = id; _maxHp = maxHp; _movSpd = movSpd; _atkSpd = atkSpd; _damage = damage; _isPlayer = isPlayer; }
        public void Apply(IEntityManager em, Entity e)
        {
            if (_isPlayer)
                em.AddComponent(e, new PlayerTag());
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
            em.AddComponent(e, new CharacterIntentBuffer());
            em.AddComponent(e, new ActionPhaseComponent { WindupDuration = 0.5f, RecoveryDuration = 0.7f });
            em.AddComponent(e, new StatusModificationQueue());
            em.AddComponent(e, new HpModificationQueue());
            em.AddComponent(e, new SpawnOutbox());
            em.AddComponent(e, new IntoTheDungeon.Core.ECS.Components.TeamComponent());
            // 판정 크기는 임시값. HitDetectSystem의 디버그 박스를 보며 조정
            em.AddComponent(e, new HurtboxComponent { Offset = new Vec2(0f, 0f), HalfExtents = new Vec2(0.3f, 0.4f) });
            em.AddComponent(e, MeleeAttackComponent.Default);
        }
    }
}
