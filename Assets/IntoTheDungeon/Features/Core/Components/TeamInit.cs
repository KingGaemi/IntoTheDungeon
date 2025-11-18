using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Spawn;
using IntoTheDungeon.Core.ECS.Components;


namespace IntoTheDungeon.Features.Core
{
    public struct TeamInit : ISpawnInit
    {
        public TeamFlag Team;
        public void Apply(IWorld world, Entity e)
        {
            var em = world.EntityManager;
            var c = em.HasComponent<TeamComponent>(e)
                ? em.GetComponent<TeamComponent>(e)
                : default;
            c.TeamFlag = Team;
            em.SetComponent(e, c);
        }
    }
}