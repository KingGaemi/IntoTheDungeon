using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.Abstractions.World;

using IntoTheDungeon.Features.Physics.Components;


// KinematicComponent의 Velocity를 이용해 Tick당 Velocity를 IPhysicsBody에 부여


namespace IntoTheDungeon.Features.Physics.Systems
{
    public sealed class KinematicSystem : GameSystem, ILateTick
    {
        public KinematicSystem(int priority = 0) : base(priority) { }
        override public void Initialize(IWorld world)
        {
            _world = world;
        }
        public void LateTick(float dt)
        {
            var chunks = _world.EntityManager.GetChunks(typeof(KinematicComponent),
                                                        typeof(PhysicsCommand));

            foreach (var chunk in chunks)
            {
                var kinematics = chunk.GetComponentArray<KinematicComponent>();
                var physicsCommands = chunk.GetComponentArray<PhysicsCommand>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref readonly var k = ref kinematics[i];
                    ref var c = ref physicsCommands[i];
                    var v = k.Direction * k.Magnitude; 

                    c.V = v;
                    c.Axes = VelAxes.X;      // 중력 사용이면 X만: VelAxes.X
                    c.Mode = VelMode.Set;
                    c.HasVel = true;
                }
            }
        }

        override public void Shutdown() { }
    }
}

