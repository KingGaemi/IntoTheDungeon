using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Runtime.Services;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Runtime.Abstractions;

using IntoTheDungeon.Features.Physics.Systems;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.State;

using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Physics.Implementation;
using IntoTheDungeon.Features.Command;


namespace IntoTheDungeon.Runtime.Installation.Installers
{
    public sealed class CoreGameInstaller : IGameInstaller
    {
        public void Install(GameWorld world)
        {
            // 서비스 등록
            world.Set<IClock>(new UnityClock());
            world.Set<IInputService>(new UnityInputService());
            // world.Set<ICollisionEvents>(new CollisionEvents());
            world.Set<IPhysicsBodyStore>(new PhysicsBodyStore());

            world.SystemManager.Add(new KinematicPlannerSystem());
            world.SystemManager.Add(new KinematicSystem());
            world.SystemManager.Add(new StatusProcessingSystem());
            world.SystemManager.Add(new CharacterIntentApplySystem());
            world.SystemManager.Add(new PhaseControlSystem());


        }
    }
}