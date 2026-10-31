using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.State;
using IntoTheDungeon.Features.Status;


// 상태(이동 방향·속도 스탯)로 KinematicComponent의 속도를 정한다. 실제 이동은 MotionSystem.
namespace IntoTheDungeon.Features.Physics.Systems
{
    public sealed class KinematicPlannerSystem : GameSystem, IFixedTick
    {
        // 깊이(위/아래) 이동은 가로보다 느리게: 던파식 2.5D 감각
        public const float DepthSpeedScale = 0.7f;

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

                    if (state.IsMoving && state.CanMove)
                    {
                        // MoveDir가 비어 있으면(가로 전용 의도 등) 바라보는 방향으로
                        var dir = state.MoveDir == Vec2.Zero ? new Vec2(state.FacingToSign(), 0f) : state.MoveDir;
                        kineComp.Direction = new Vec2(dir.X, dir.Y * DepthSpeedScale);
                        kineComp.Magnitude = status.MovementSpeed;
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
