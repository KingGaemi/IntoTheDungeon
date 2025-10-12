using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.ECS.Abstractions;


namespace IntoTheDungeon.Features.Status
{
    public class DamageProcessingSystem : GameSystem, ITick
    {
        public DamageProcessingSystem(int priority = 0) : base(priority) { }
        private IEventHub _hub;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if(!_world.TryGet(out _hub))
            {
                Enabled = false;
            } 
        }
        
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(StatusComponent),
                typeof(HpModificationQueue)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var queues = chunk.GetComponentArray<HpModificationQueue>();
                var entities = chunk.GetEntities();

               
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var s = ref statuses[i];
                    ref var q = ref queues[i];

                    if (!s.IsAlive) { q.Clear(); continue; }

                    int prevHp = s.CurrentHp;
                    Entity lastSrc = Entity.Null;
                    int lastDmg = 0;

                    while (q.TryDequeue(out var m))
                    {
                        if (m.Kind == HpModification.ModKind.Damage)
                        { lastSrc = m.Source; lastDmg = m.Value; }

                        ApplyModification(ref s, in m);
                         if (prevHp > 0 && s.CurrentHp <= 0)
                        {
                            s.IsAlive = false;
                            _hub.Publish(new KillEvent(lastSrc, entities[i], lastDmg, DamageType.Attack));
                            _hub.Publish(new DeathEvent(entities[i], lastSrc));
                        }
                    }

                   
                    
                }
            }
        }
        
        static void ApplyModification(ref StatusComponent s, in HpModification m)
        {
            switch (m.Kind)
            {
                case HpModification.ModKind.Damage: s.CurrentHp = Mathx.Max(0, s.CurrentHp - m.Value); break;
                case HpModification.ModKind.Heal:   s.CurrentHp = Mathx.Min(s.MaxHp, s.CurrentHp + m.Value); break;
                case HpModification.ModKind.Set:    s.CurrentHp = Mathx.Clamp(m.Value, 0, s.MaxHp); break;
            }
        }

    }
}
