using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Spawning;
using IntoTheDungeon.Core.ECS.Systems;

namespace IntoTheDungeon.Core.Runtime.ECS
{
    public class SpawnSystem : GameSystem, ITick
    {

        IEntityFactory _factory;
        ILogger _log;
        ISystemSpawnQueue _sysQueue;
        readonly List<SpawnOrder> _inbox = new(128);
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled =
            world.TryGet(out _factory) &&
            world.TryGet(out _log) &&
            world.TryGet(out _sysQueue);
        }
        public void Tick(float dt)
        {
            _inbox.Clear();
            // 1) 시스템 큐 먼저 처리
            int budget = 512; // 프레임당 생성 한도
            while (budget-- > 0 && _sysQueue.TryDequeue(out var order))
                _inbox.Add(order);

            // 2) 엔티티 Outbox 처리
            var chunks = _world.EntityManager.GetChunks(typeof(SpawnOutbox));
            foreach (var chunk in chunks)
            {
                var boxes = chunk.GetComponentArray<SpawnOutbox>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var box = ref boxes[i];
                    if (!box.HasValue) continue;

                    _inbox.Add(box.Value);
                    box.Clear();
                }
            }

            for (int i = 0; i < _inbox.Count; i++)
            {
                var order = _inbox[i];
                if (!_factory.TrySpawn(order.RecipeId, order.Spec, out _))
                {
                    _log.Warn($"Spawn failed: id={order.RecipeId.Value}");
                }
            }
        }

    }
}