
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Spawning
{
    public struct SpawnBuffer : IComponentData
    {
        public SpawnRequest Value;
        public bool HasValue;

        public void Set(in SpawnRequest request)
        {
            Value = request;
            HasValue = true;
        }

        public void Clear()
        {
            Value = default;
            HasValue = false;
        }
    }
}