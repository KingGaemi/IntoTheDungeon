using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Components
{
    public struct SpriteComponent : IComponentData
    {
        public int SpriteId;        // 카탈로그 키
        public int SortingLayer;    // 정수 레이어
        public int OrderInLayer;    // 소트 순서
        public bool FlipX;
        public bool FlipY;
        public float Scale;
        public uint TintRGBA;       // 0xAARRGGBB
    }

}
