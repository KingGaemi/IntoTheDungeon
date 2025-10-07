#if false
using IntoTheDungeon.Core.Runtime.Installation;
using IntoTheDungeon.Core.Runtime.Runner;
using IntoTheDungeon.Core.Runtime.World;



public sealed class PhycisInstaller : IGameInstaller
{
    public void Install(GameWorld world, SystemRunner runner)
    {
        world.Set<KinematicPlannerSystem>(new KinematicPlannerSystem());

    }
}
#endif
