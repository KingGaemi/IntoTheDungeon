using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Components
{
    public struct SpriteAnimComponent : IComponentData
    {
        public int AnimId;          // 애니메이션 키
        public float Time;          // 누적 시간
        public float Speed;         // 배속
        public bool Loop;
    }
}