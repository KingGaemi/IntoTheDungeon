using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 맞는 판정 영역. 중심 = TransformComponent.Position + Offset
    /// </summary>
    public struct HurtboxComponent : IComponentData
    {
        public Vec2 Offset;
        public Vec2 HalfExtents;
    }
}
