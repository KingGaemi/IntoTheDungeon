using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 히트박스를 HitboxComponent.Owner 위치에 붙여 둔다.
    /// Offset은 오른쪽을 볼 때 기준이고, 왼쪽을 보면 X가 반전된다.
    /// </summary>
    public struct FollowOwnerComponent : IComponentData
    {
        public Vec2 Offset;
    }
}
