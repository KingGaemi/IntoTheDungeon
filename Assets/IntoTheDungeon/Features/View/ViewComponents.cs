using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.View
{
    struct ViewMarker : IComponentData
    {
        public int RecipeId;
        public int SortingLayerId;  // 0이면 기본
        public int OrderInLayer;
        BehaviourSpec[] Behaviours;
        byte[] Payload;
    }
    struct ViewSpawnedTag : IComponentData { }
    struct PendingDespawnTag : IComponentData { }
}
