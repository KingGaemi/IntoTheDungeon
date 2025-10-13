

using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Core.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Unity.World;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.Gameplay;



namespace IntoTheDungeon.Unity.Behaviour
{
    [DisallowMultipleComponent]
    public class CharacterCoreAuthoring : IAuthoring
    {
        public int maxHp;
        public float movementSpeed;
        public float attackSpeed;

        // 베이크용 입력 데이터
        public Rigidbody2D rb;
        public Vector2 pos;
        public Vector2 dir;
        public string displayName;

        sealed class Baker : Baker<CharacterCoreAuthoring>
        {
            protected override void Bake(CharacterCoreAuthoring a)
            {
                var store = RequireService<IPhysicsBodyStore>();
                int handle = store.Add(new UnityPhysicsBody(a.rb));

                AddComponent(new PlayerTag());
                AddComponent(new PhysicsBodyRef { Handle = handle });
                AddComponent(new TransformComponent
                {
                    Position = new Vec2(a.pos.x, a.pos.y),
                    Direction = new Vec2(a.dir.x, a.dir.y)
                });
                AddComponent(new InformationComponent { DisplayName = a.displayName });
                AddComponent(new StateComponent(ActionState.Idle));
                AddComponent(new StatusComponent
                {
                    MaxHp = a.maxHp,
                    CurrentHp = a.maxHp,
                    MovementSpeed = a.movementSpeed,
                    AttackSpeed = a.attackSpeed,
                    Damage = 10
                });
                AddComponent(new KinematicComponent());
                AddComponent(new CharacterIntentBuffer());
                AddComponent(new AnimationSyncComponent());
                AddComponent(new ActionPhaseComponent
                {
                    WindupDuration = 0.5f,
                    ActiveDuration = 0f,
                    RecoveryDuration = 0.7f,
                    CooldownDuration = 0f,
                    ActionPhase = ActionPhase.None,
                    PhaseTimer = 0f
                });
                AddComponent(new StatusModificationQueue());
                AddComponent(new HpModificationQueue());
            }
        }
        public IBaker CreateBaker() => new Baker();


    }

}
