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

namespace IntoTheDungeon.Features.Character
{
    public sealed class CharacterCoreRecipe : IEntityRecipe
    {
        public RecipeId Id { get; }

        readonly int _maxHp; readonly float _movSpd, _atkSpd;

        public CharacterCoreRecipe(RecipeId id,
            int maxHp, float movSpd, float atkSpd)
        { Id = id; _maxHp = maxHp; _movSpd = movSpd; _atkSpd = atkSpd; }



        public void Apply(IEntityManager em, Entity e)
        {
            em.AddComponent(e, new PlayerTag());
            em.AddComponent(e, new StateComponent(ActionState.Idle));
            em.AddComponent(e, new StatusComponent
            {
                MaxHp = _maxHp,
                CurrentHp = _maxHp,
                MovementSpeed = _movSpd,
                AttackSpeed = _atkSpd,
                Damage = 1
            });
            em.AddComponent(e, new KinematicComponent());
            em.AddComponent(e, new PhysicsCommand());               // 누락 방지
            em.AddComponent(e, new CharacterIntentBuffer());
            em.AddComponent(e, new AnimationSyncComponent());
            em.AddComponent(e, new ActionPhaseComponent { WindupDuration = 0.5f, RecoveryDuration = 0.7f });
            em.AddComponent(e, new StatusModificationQueue());
            em.AddComponent(e, new HpModificationQueue());
            em.AddComponent(e, new SpawnOutbox());
        }

    }
}
