using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;


// KinematicComponent의 Velocity를 이용해 Tick당 Velocity를 IPhysicsBody에 부여
namespace IntoTheDungeon.Features.Physics.Systems
{
    public sealed class KinematicPlannerSystem : GameSystem, IFixedTick
    {
        ILogger _log;
        public KinematicPlannerSystem(int priority = 0) : base(priority) { }

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            _world.TryGet(out _log);
        }
        public void FixedTick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(StateComponent), typeof(StatusComponent), typeof(KinematicComponent)))
            {
                var stateArr = chunk.GetComponentArray<StateComponent>();
                var statusArr = chunk.GetComponentArray<StatusComponent>();
                var kineCompArr = chunk.GetComponentArray<KinematicComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    var state = stateArr[i];
                    var status = statusArr[i];
                    ref var kineComp = ref kineCompArr[i];

                    if (state.IsMoving)
                    {
                        float s = status.MovementSpeed;
                        float sx = state.FacingToSign();   // -1 or +1
                        kineComp.Direction = new Vec2(sx, 0f);
                        kineComp.Magnitude = s;
                        kineComp.Magnitude = status.MovementSpeed;
                        kineComp.Direction.X = state.FacingToSign();

                    }
                    else
                    {
                        kineComp.Direction = Vec2.Zero;
                        kineComp.Magnitude = 0f;
                    }
                }
            }

        }
    }
}
