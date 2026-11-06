using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 원거리 평타 정의. 이 컴포넌트가 있으면 AttackSystem이 근접 히트박스 대신 화살(날아가는 히트박스)을 쏜다.
    /// </summary>
    public struct RangedAttackComponent : IComponentData
    {
        public float SpawnOffsetX;      // 몸 앞쪽 이만큼에서 출발(오른쪽 기준)
        public float Speed;             // 유닛/초
        public float Lifetime;          // 초. 사거리 ≈ 속도 × 시간
        public Vec2 HalfExtents;        // 판정(X 가로 반폭, Y 깊이 반두께)
        public float KnockbackSpeed;
        public bool Pierce;             // true면 관통, false면 첫 대상에 맞고 사라짐
        public float HitStun;           // 맞은 쪽 경직(초). 0이면 경직 없음
        public ProjectileVisual Visual;

        public static readonly RangedAttackComponent Arrow = new()
        {
            SpawnOffsetX = 0.4f,
            Speed = 12f,
            Lifetime = 0.7f,                // 사거리 약 8
            HalfExtents = new Vec2(0.25f, 0.2f),
            KnockbackSpeed = 0.5f,          // 평타 화살은 거의 안 밀림(약 0.05)
            Pierce = false,
            HitStun = 0f,                   // 평타 화살은 경직 없음
            Visual = ProjectileVisual.Arrow
        };

        // 주술사: 느리게 날아가는 초록 구슬(보고 피할 만큼 느림)
        public static readonly RangedAttackComponent Orb = new()
        {
            SpawnOffsetX = 0.5f,
            Speed = 7f,
            Lifetime = 1f,                  // 사거리 약 7
            HalfExtents = new Vec2(0.25f, 0.25f),
            KnockbackSpeed = 1f,
            Pierce = false,
            HitStun = 0.15f,
            Visual = ProjectileVisual.Orb
        };
    }
}
