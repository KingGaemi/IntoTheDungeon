using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Physics.Implementation;
using IntoTheDungeon.Core.Physics.Runtime;
using IntoTheDungeon.Features.Physics.Systems;

namespace IntoTheDungeon.Unity.Physics
{
    public sealed class UnityPhysicsInstaller : MonoGameInstaller
    {
        const int ORDER_Planner  = 10;
        const int ORDER_Kinematic= 20;
        const int ORDER_Apply    = 30;

        public override void Install(GameWorld world)
        {
            // 서비스 단일 등록
            if (!world.TryGet(out ICollisionEvents _))
                world.Set<ICollisionEvents>(new CollisionEvents());

            if (!world.TryGet(out IPhysicsBodyStore _))
                world.Set<IPhysicsBodyStore>(new PhysicsBodyStore());

            // 고정틱 파이프라인에 순서대로 추가
            world.SystemManager.Add(new KinematicPlannerSystem(ORDER_Planner));
            world.SystemManager.Add(new KinematicSystem(ORDER_Kinematic));
            world.SystemManager.Add(new PhysicsApplySystem(ORDER_Apply));
        }
    }
}
