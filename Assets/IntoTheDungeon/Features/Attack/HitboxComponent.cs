using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 때리는 판정 영역. 근접/투사체/장판 모두 이 컴포넌트 하나로 판정하고,
    /// 차이는 함께 붙는 컴포넌트(FollowOwner, Kinematic, Lifetime 등)로 표현한다.
    /// </summary>
    public struct HitboxComponent : IComponentData
    {
        public Entity Owner;
        public TeamFlag Team;
        public int Damage;      // 생성 시점 공격자 스탯으로 고정
        public Vec2 Center;     // 월드 좌표. FollowOwnerSystem 등이 갱신
        public Vec2 HalfExtents;
    }
}
