using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Runtime.Services;
using IntoTheDungeon.Core.Runtime.Runner;
using IntoTheDungeon.Core.Runtime.World;

namespace IntoTheDungeon.Core.Runtime.Installation.Installers
{
    public sealed class CoreGameInstaller : IGameInstaller
    {
        public void Install(GameWorld world, SystemRunner runner)
        {
            // 서비스 등록
            world.Set<IClock>(new UnityClock());
            world.Set<IInputService>(new UnityInputService());

        }
    }
}