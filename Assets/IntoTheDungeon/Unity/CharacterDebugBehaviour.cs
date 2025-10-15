using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.Status;
using UnityEngine;

namespace IntoTheDungeon.Unity.Behaviour
{
    [DisallowMultipleComponent]
    public sealed class CharacterDebugBehaviour : MonoBehaviour, IWorldInjectable
    {
        public IWorld World { get; set; }
        [SerializeField] EntityRootBehaviour root;

        public void Init(IWorld world)
        {
            World = world;
        }

        public bool TrySnapshot(out StatusComponent s)
        {
            s = default;
            if (World == null || root == null || root.Entity == Entity.Null) return false;
            s = World.EntityManager.GetComponent<StatusComponent>(root.Entity);
            return true;
        }

        public void ApplyDamage(int amount)
        {
            if (World == null) return;
            var q = World.EntityManager.GetComponent<HpModificationQueue>(root.Entity);
            q.Enqueue(HpModification.Damage(amount, root.Entity));
            World.EntityManager.SetComponent(root.Entity, q);
        }

        public void ApplyHeal(int amount) { /* 동일 패턴 */ }
        public void AddAttackSpeed(float v) { /* StatusModificationQueue에 enqueue */ }
        public void AddMoveSpeed(float v) { /* 동일 */ }


    }
}