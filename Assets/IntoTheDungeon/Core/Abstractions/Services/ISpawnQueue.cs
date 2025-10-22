using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface ISystemSpawnQueue
    {
        void Enqueue(in SpawnOrder req);
        bool TryDequeue(out SpawnOrder req);
    }
}