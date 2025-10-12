#nullable enable
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Services;

namespace IntoTheDungeon.Core.Abstractions.World
{
    public interface IWorld : IDisposable, IServiceRegistry
    {
        IEntityManager EntityManager { get; }
        ISystemManager SystemManager { get; }

        IManagedComponentStore? ManagedStore { get; }
    }

}
#nullable disable