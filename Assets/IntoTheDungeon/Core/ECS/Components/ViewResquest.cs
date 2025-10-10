using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Core.ECS.Components
{
    public struct SpawnViewRequest : IComponentData
    {
        public Entity Target;    // 뷰를 붙일 ECS 엔티티
        public int PrefabId;
    }

    public struct DespawnViewRequest : IComponentData
    {
        public Entity Target;
    }
}