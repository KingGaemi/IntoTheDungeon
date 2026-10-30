using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 공격자가 가진 근접 공격 정의. 공격 Active 시점에 이 값으로 히트박스를 만든다.
    /// </summary>
    public struct MeleeAttackComponent : IComponentData
    {
        public Vec2 Offset;       // 오른쪽 기준, 공격자 위치로부터
        public Vec2 HalfExtents;
        public float ActiveTime;  // 히트박스 유지 시간(초)

        public static readonly MeleeAttackComponent Default = new()
        {
            Offset = new Vec2(0.5f, 0f),
            HalfExtents = new Vec2(0.4f, 0.3f),
            ActiveTime = 0.1f
        };
    }
}
