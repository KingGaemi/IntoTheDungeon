using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Components
{
    public struct ViewMarker : IComponentData
    {
        public short SortingLayerId;  // 0이면 기본
        public short OrderInLayer;


    }
    public struct ViewSpawnedTag : IComponentData { }

    public struct PhysReservedTag : IComponentData { }
    public struct PendingDespawnTag : IComponentData { }
}
