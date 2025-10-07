using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Runtime.Services;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Core.Runtime.Installation.Installers
{
    public sealed class CoreGameInstaller : IGameInstaller
    {
        public void Install(GameWorld world)
        {
            // 서비스 등록
            world.Set<IClock>(new UnityClock());
            world.Set<IInputService>(new UnityInputService());

            world.SystemManager.Add(new KinematicSystem());
            world.SystemManager.Add(new KinematicPlannerSystem());
            world.SystemManager.Add(new StatusProcessingSystem());
            world.SystemManager.Add(new CharacterIntentApplySystem());
            world.SystemManager.Add(new AnimationSyncSystem());
            world.SystemManager.Add(new PhaseControlSystem());
            

        }
    }
}