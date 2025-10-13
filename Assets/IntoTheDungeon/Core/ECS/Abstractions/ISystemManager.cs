// Core.ECS.Abstractions
using System.Collections.Generic;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface ISystemManager : Core.Abstractions.IDisposable
    {
        IReadOnlyList<IGameSystem> Systems { get; }

        void Add<T>(T system) where T : IGameSystem;
        T Get<T>() where T : IGameSystem;
        bool Has<T>() where T : IGameSystem;

        void ExecuteUpdate(float dt);
        void ExecuteFixed(float dt);
        void ExecuteLate(float dt);
#if DEBUG
        void DebugPrint();
#endif
    }
}