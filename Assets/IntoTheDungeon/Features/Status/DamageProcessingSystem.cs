using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.ECS.Abstractions;
using System;


namespace IntoTheDungeon.Features.Status
{
    public class DamageProcessingSystem : GameSystem, ITick
    {
        public DamageProcessingSystem(int priority = 0) : base(priority) { }
        private IEventHub _hub;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!_world.TryGet(out _hub))
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

                    while (q.TryDequeue(out var m))
                    {
                        int before = s.CurrentHp;
                        ApplyModification(ref s, in m);

                        if (m.Kind == HpModification.ModKind.Damage)
                            _hub.Publish(new DamagedEvent(entities[i], m.Source, before - s.CurrentHp, s.CurrentHp, s.MaxHp));

                        if (s.CurrentHp <= 0)
                        {
                            s.IsAlive = false;
                            _hub.Publish(new KillEvent(m.Source, entities[i], m.Value, DamageType.Attack));
                            _hub.Publish(new DeathEvent(entities[i], m.Source));
                            q.Clear();   // 죽은 뒤 들어온 나머지는 버린다
                            break;
                        }
                    }
                }
            }
        }

        static void ApplyModification(ref StatusComponent s, in HpModification m)
        {
            switch (m.Kind)
            {
                case HpModification.ModKind.Damage: s.CurrentHp = Math.Max(0, s.CurrentHp - m.Value); break;
                case HpModification.ModKind.Heal: s.CurrentHp = Math.Min(s.MaxHp, s.CurrentHp + m.Value); break;
                case HpModification.ModKind.Set: s.CurrentHp = Math.Clamp(m.Value, 0, s.MaxHp); break;
            }
        }

    }
}
