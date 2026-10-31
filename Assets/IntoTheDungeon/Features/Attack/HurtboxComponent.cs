using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 맞는 판정 영역(바닥 평면). 중심 = TransformComponent.Position + Offset, HalfExtents.Y는 깊이 반두께
    /// </summary>
    public struct HurtboxComponent : IComponentData
    {
        public Vec2 Offset;
        public Vec2 HalfExtents;
    }
}
