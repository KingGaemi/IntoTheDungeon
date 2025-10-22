
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Spawning
{
    public struct SpawnOutbox : IComponentData
    {
        public bool HasValue;
        public SpawnOrder Value;
        public void Clear() => HasValue = false;
        public void Set(in SpawnOrder order)
        {
            Value = order;
            HasValue = true;
        }
    }
}