using System.Collections.Concurrent;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;



namespace IntoTheDungeon.Core.Runtime.ECS
{
    public sealed class SpawnQueue : ISystemSpawnQueue
    {
        readonly ConcurrentQueue<SpawnOrder> _q = new();
        public void Enqueue(in SpawnOrder req) => _q.Enqueue(req);
        public bool TryDequeue(out SpawnOrder req) => _q.TryDequeue(out req);
    }
}