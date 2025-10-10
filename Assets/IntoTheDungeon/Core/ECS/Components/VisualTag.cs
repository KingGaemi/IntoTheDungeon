using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Core.ECS.Components
{
    public struct VisualTag : IComponentData
    {
        public int PrefabId;     // 프리팹 카탈로그 키
    }
}