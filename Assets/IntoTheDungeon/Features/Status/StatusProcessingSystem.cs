using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;


namespace IntoTheDungeon.Features.Status
{
    public class StatusProcessingSystem : GameSystem, ITick
    {
        private IEventHub _hub;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!_world.TryGet(out _hub)) Enabled = false;
        }
        public StatusProcessingSystem(int priority = 0) : base(priority) { }
        
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(StatusComponent),
                typeof(StatusModificationQueue)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var queues   = chunk.GetComponentArray<StatusModificationQueue>();
                var entities = chunk.GetEntities();

                
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var s = ref statuses[i];
                    ref var queue = ref queues[i];
                    StatusDirty dirty = StatusDirty.None;
                    int applied = 0;

                    while (queue.TryDequeue(out var m))
                    {
                        ApplyModification(ref s, m, ref dirty);
                        applied++;
                    }
                    
                    // 이벤트 발행
                     if (applied > 0 && dirty != StatusDirty.None)
                        _hub.Publish(new StatusChanged(entities[i], dirty, s.Damage, s.Armor, s.AttackSpeed, s.MovementSpeed));
                    // 큐 비우기
                    queue.Clear();
                }
            }
        }
        
        static void ApplyModification(ref StatusComponent s, in StatusModification mod, ref StatusDirty dirty)
        {
            switch (mod.ModType)
            {
                case StatusModification.Type.AddDamage:
                    s.Damage = Mathx.Max(0, s.Damage + (int)mod.Value);
                    dirty |= StatusDirty.Damage;
                    break;

                case StatusModification.Type.AddArmor:
                    s.Armor = Mathx.Max(0, s.Armor + (int)mod.Value);
                    dirty |= StatusDirty.Armor;
                    break;

                case StatusModification.Type.AddAttackSpeed:
                    s.AttackSpeed = Mathx.Max(0.1f, s.AttackSpeed + mod.Value);
                    dirty |= StatusDirty.AtkSpd;
                    break;

                case StatusModification.Type.AddMovementSpeed:
                    s.MovementSpeed = Mathx.Max(0, s.MovementSpeed + mod.Value);
                    dirty |= StatusDirty.MovSpd;
                    break;

                case StatusModification.Type.SetDamage:
                    s.Damage = Mathx.Max(0, (int)mod.Value);
                    dirty |= StatusDirty.Damage;
                    break;

                case StatusModification.Type.SetArmor:
                    s.Armor = Mathx.Max(0, (int)mod.Value);
                    dirty |= StatusDirty.Armor;
                    break;

                case StatusModification.Type.SetAttackSpeed:
                    s.AttackSpeed = Mathx.Max(0.1f, mod.Value);
                    dirty |= StatusDirty.AtkSpd;
                    break;

                case StatusModification.Type.SetMovementSpeed:
                    s.MovementSpeed = Mathx.Max(0, mod.Value);
                    dirty |= StatusDirty.MovSpd;
                    break;

                case StatusModification.Type.Init:
                    break;
            }
        }

    }
}
