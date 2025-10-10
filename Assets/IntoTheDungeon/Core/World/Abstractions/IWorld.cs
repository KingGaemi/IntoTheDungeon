#nullable enable

using IntoTheDungeon.Core.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.World.Abstractions
{
    public interface IWorld : IDisposable, IServiceRegistry
    {
        IEntityManager EntityManager { get; }
        ISystemManager SystemManager { get; }

        IManagedComponentStore? ManagedStore { get; }
    }

}
