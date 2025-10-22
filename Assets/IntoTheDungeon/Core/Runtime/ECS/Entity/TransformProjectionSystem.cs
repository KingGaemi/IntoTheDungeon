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
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _viewOpQueue);
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
                    if (transform.Moved)
                    {
                        var transData = new TransformData
                        {
                            X = transform.Position.X,
                            Y = transform.Position.Y,
                            RotDeg = transform.Rotation * Mathx.Rad2Deg
                        };
                        _viewOpQueue.Enqueue(entity, transData);
                    }
                }
            }
        }
    }
}