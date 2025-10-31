
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.ECS.Components;


namespace IntoTheDungeon.Core.Physics.Implementation
{
    public class PhysicsSpawnSystem : GameSystem, ITick
    {

        IPhysicsBodyStore _physStore;
        IBodyCreateQueue _bodyQueue;

        IHandleOwnerIndex _index;


        ILogger _log;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);

            if (!world.TryGet(out _log))
            {
                Enabled = false;
                return;
            }
            Enabled = world.TryGet(out _physStore) && world.TryGet(out _bodyQueue) && world.TryGet(out _index);
        }

        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(PhysicsBodyRef), typeof(TransformComponent)))
            {
                var physRefs = chunk.GetComponentArray<PhysicsBodyRef>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var pref = ref physRefs[i];
                    ref var trans = ref transforms[i];
                    if (pref.Initialized) continue;                // 이미 있음


                    // ECS에서 Handle생성
                    var h = _physStore.Reserve();
                    pref.Handle = h;
                    _index.Register(h, entities[i]);




                    _bodyQueue.Enqueue(new BodyCreateSpec
                    {
                        Handle = h,
                        BodyType = BodyType.Dynamic,
                        Mass = 1.0f,
                        ColliderType = ColliderType.Capsule,
                        GravityScale = 1.0f,
                        Position = trans.Position,
                        Rotation = trans.Rotation
                    });
                    // _log.Log($"BodyCreation Enqueued {h.Index}");
                    pref.Initialized = true;
                }
            }

        }
    }

}
