using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Runtime.ECS
{
    public class TransformProjectionSystem : GameSystem, ITick
    {
        IViewOpQueue _viewOpQueue;
        ILogger _log;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _viewOpQueue) && world.TryGet(out _log);
        }
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(TransformComponent)))
            {
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var entity = entities[i];

                    ref var transform = ref transforms[i];
                    // 2.5D: 위치는 ECS가 직접 정하므로 매 프레임 View에 보낸다
                    // (스폰 직후 첫 배치가 Spawn 처리 순서에 따라 빠지는 일이 없도록 Moved로 거르지 않음)
                    var transData = new TransformData
                    {
                        X = transform.Position.X,
                        Y = transform.Position.Y,
                        Height = transform.Height,
                        RotDeg = transform.Rotation * Mathx.Rad2Deg
                    };
                    _viewOpQueue.Enqueue(entity, transData);
                    transform.Moved = false;
                }
            }
        }
    }
}