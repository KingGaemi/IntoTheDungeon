using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.ECS.Components.Physics;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Core;

// KinematicComponent의 Velocity를 이용해 Tick당 Velocity를 IPhysicsBody에 부여
namespace IntoTheDungeon.Core.ECS.Systems
{
    public sealed class KinematicPlannerSystem : GameSystem, ITick
    {
        override public int Priority { get; } = 1;
        public void Tick(float dt)
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
                        kineComp.Magnitude = status.MovementSpeed;
                        kineComp.Direction.X = state.Current.Facing.ToSign();
                    }
                    else
                    {
                        kineComp.Magnitude = 0f;
                    }
                }
            }

        }
    }
}
