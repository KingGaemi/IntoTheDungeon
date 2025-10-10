

using UnityEngine;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.World.Abstractions;
using IntoTheDungeon.Runtime.Unity.World;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Core.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Physics.Abstractions;


[DisallowMultipleComponent]
public class CharacterCore : EntityBehaviour, IAuthoring
{
    private StatusComponent statusComponent;

    [Header("Initial Stats")]
    [SerializeField] private int maxHealth = 100; // init
    [SerializeField] private float movementSpeed = 5f; // init
    public IBaker CreateBaker() => new CharacterCoreBaker();

    new public bool IsValid => IsValid();

    class CharacterCoreBaker : UnityBaker<CharacterCore>
    {
        protected override void Bake(CharacterCore authoring)
        {
            var entityRoot = authoring.GetComponentInParent<EntityRootBehaviour>();
            if (entityRoot == null)
            {
                Debug.LogError($"[CharacterCoreBaker] EntityRoot not found for {authoring.name}!");
                return;
            }

            var entityTrans = entityRoot.Transform;
            var euler = entityTrans.rotation.eulerAngles.z * Mathf.Deg2Rad;
            if (!entityRoot.TryGetComponent<Rigidbody2D>(out var rb))
            {
                Debug.LogError($"[CharacterCoreBaker] Rigidbody2D not found on {entityRoot.name}"); 
                return;
            }

            // (2) PhysicsBodyStore 획득
            var store = World.Require<IPhysicsBodyStore>();

            // (3) Add() 호출 → int handle 자동 발급
            int handle = store.Add(new UnityPhysicsBody(rb));

            // (4) ECS 컴포넌트로 저장
            AddComponent(new PhysicsBodyRef { Handle = handle });
            AddComponent(new PhysicsCommand());
            AddComponent(new TransformComponent
            {
                Position = new Vec2(entityTrans.position.x, entityTrans.position.y),
                Direction = new Vec2(Mathf.Cos(euler), Mathf.Sin(euler))
            });
            AddComponent(new InformationComponent { DisplayName = entityRoot.name });
            AddComponent(new StateComponent());
            AddComponent(StatusComponent.Default);
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

            
            

            AddManagedComponent(new StatusModificationQueue());

        }
    }

    public bool AddIntent(CharacterIntent intent)
    {
        if (!IsValid()) return false;

        ref var buffer = ref World.EntityManager.GetComponent<CharacterIntentBuffer>(EntityRoot.Entity);
        buffer.Add(intent);
        return true;
    }
    
    public ref StatusComponent GetStatusComponent()
    {
        return ref statusComponent;
    }

    public void TestDamage(int amount)
    {
        if (IsValid())
        {
            var queue = World.ManagedStore.GetManagedComponent<StatusModificationQueue>(Entity);
            if (queue != null)
            {
                queue.Enqueue(StatusModification.Damage(amount));

            }
            else
            {
                Debug.LogError("StatusModificationQueue not found!");  //  이게 나오는지
            }
        }
    }
    public void TestHeal(int amount)
    {
        if (IsValid())
        {
            if (World.ManagedStore.TryGetManagedComponent<StatusModificationQueue>(EntityRoot.Entity, out var queue))
            {
                queue.Enqueue(StatusModification.Heal(amount));
            }
        }
    }
    public void TestAttackSpeed(float speed)
    {
        if (IsValid())
        {
            if (World.ManagedStore.TryGetManagedComponent<StatusModificationQueue>(Entity, out var queue))
            {
                queue.Enqueue(StatusModification.AddAttackSpeed(speed));

            }
        }
    }
    public void TestMovementSpeed(float speed)
    {
        if (IsValid())
        {
            if (World.ManagedStore.TryGetManagedComponent<StatusModificationQueue>(Entity, out var queue))
            {
                queue.Enqueue(StatusModification.AddMovementSpeed(speed));
            }
        }
    }
}
