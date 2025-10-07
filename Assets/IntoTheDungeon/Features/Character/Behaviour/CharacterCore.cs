

using UnityEngine;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.ECS.Components.Physics;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Core.Runtime.Physics;


[DisallowMultipleComponent]
public class CharacterCore : EntityBehaviour, IAuthoring
{
    private StatusComponent statusComponent;

    [Header("Initial Stats")]
    [SerializeField] private int maxHealth = 100; // init
    [SerializeField] private float movementSpeed = 5f; // init
    public IBaker CreateBaker() => new CharacterCoreBaker();

    new public bool IsValid => IsValid();

    class CharacterCoreBaker : Baker<CharacterCore>
    {
        public override void Bake(CharacterCore authoring)
        {
            
            AddComponent(new StateComponent());
            AddComponent(new StatusComponent
            {
                CurrentHp = authoring.maxHealth,
                MaxHp = authoring.maxHealth,
                MovementSpeed = authoring.movementSpeed,
                AttackSpeed = 1.0f,
                IsAlive = true
            });
            AddComponent(new KinematicComponent());
            AddComponent(new CharacterIntentBuffer());
            AddComponent(new AnimationSyncComponent());
            AddComponent(new ActionPhaseComponent
            {
                WindupDuration = 0.5f,
                ActiveDuration = 0f,
                RecoveryDuration = 0.5f,
                CooldownDuration = 0f,
                ActionPhase = ActionPhase.None,
                PhaseTimer = 0f
            });
            
            var entityRoot = authoring.GetComponentInParent<EntityRootBehaviour>();        
            if (entityRoot == null)
            {
                Debug.LogError($"[CharacterCoreBaker] EntityRoot not found for {authoring.name}!");
                return;
            }
            
            //  EntityRoot에서 Rigidbody2D 찾기
            if (!entityRoot.TryGetComponent<Rigidbody2D>(out var rb))
            {
                Debug.LogError($"[CharacterCoreBaker] Rigidbody2D not found on {entityRoot.name}!");
                return;
            }
            AddManagedComponent(new UnityPhysicsBody(rb));

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
            var queue = World.EntityManager.GetManagedComponent<StatusModificationQueue>(Entity);
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
            if (World.EntityManager.TryGetManagedComponent<StatusModificationQueue>(EntityRoot.Entity, out var queue))
            {
                queue.Enqueue(StatusModification.Heal(amount));
            }
        }
    }
    public void TestAttackSpeed(float speed)
    {
        if (IsValid())
        {
            if (World.EntityManager.TryGetManagedComponent<StatusModificationQueue>(Entity, out var queue))
            {
                queue.Enqueue(StatusModification.AddAttackSpeed(speed));

            }
        }
    }
    public void TestMovementSpeed(float speed)
    {
        if (IsValid())
        {
            if (World.EntityManager.TryGetManagedComponent<StatusModificationQueue>(Entity, out var queue))
            {
                queue.Enqueue(StatusModification.AddMovementSpeed(speed));
            }
        }
    }
}
