using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.Abstractions.World;

using IntoTheDungeon.Features.Physics.Components;


// KinematicComponent의 Velocity를 이용해 Tick당 Velocity를 IPhysicsBody에 부여


namespace IntoTheDungeon.Features.Physics.Systems
{
    public sealed class KinematicSystem : GameSystem, IFixedTick
    {
        IPhysicsOpQueue _physQueue;
        public KinematicSystem(int priority = 0) : base(priority) { }


        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _physQueue))
            {
                Enabled = false;
                return;
            }
        }
        public void FixedTick(float dt)
        {
            var chunks = _world.EntityManager.GetChunks(typeof(KinematicComponent),
                                                        typeof(PhysicsBodyRef));

            foreach (var chunk in chunks)
            {
                var kinematics = chunk.GetComponentArray<KinematicComponent>();
                var handles = chunk.GetComponentArray<PhysicsBodyRef>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref readonly var k = ref kinematics[i];
                    ref var h = ref handles[i];
                    var v = k.Direction * k.Magnitude;
                    _physQueue.EnqueueSetLinearVelocity(h.Handle, v);
                }
            }
        }

        override public void Shutdown() { }
    }
}

