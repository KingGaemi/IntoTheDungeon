using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Physics.Components
{
    /// <summary>
    /// 캐릭터 몸통의 바닥 평면 충돌(서로 겹치지 않게 밀어내기). 판정용 허트박스와는 별개.
    /// 바닥 평면에서 타원: 가로 반지름 = Radius, 깊이 반지름 = Radius × DepthRatio(화면에서 깊이는 납작하게 보이므로).
    ///
    /// 크기·무게는 "기본값 + 보정"으로 나눠 둔다. 아이템·버프·변신 같은 효과는 기본값을 건드리지 말고
    /// RadiusAdd/RadiusMul/MassMul만 바꾸면 되고, 효과가 끝나면 보정만 되돌리면 된다.
    /// </summary>
    public struct BodyComponent : IComponentData
    {
        // 기본값(캐릭터 정의에서 옴)
        public float BaseRadius;
        public float BaseMass;      // 무거울수록 덜 밀린다
        public bool Immovable;      // 보스·구조물: 남을 밀기만 하고 자기는 안 밀림
        public float HeadHeight;    // 발에서 머리 꼭대기까지 높이(체력바 등 머리 위 표시 위치)

        // 보정(아이템·버프 등이 쓴다). 기본은 Add 0, Mul 1
        public float RadiusAdd;
        public float RadiusMul;
        public float MassMul;

        public readonly float Radius => System.MathF.Max(0f, (BaseRadius + RadiusAdd) * RadiusMul);
        public readonly float Mass => System.MathF.Max(0.01f, BaseMass * MassMul);

        public const float DepthRatio = 0.5f;

        public static BodyComponent Create(float radius, float mass, bool immovable = false, float headHeight = 1.2f) => new()
        {
            BaseRadius = radius,
            BaseMass = mass,
            Immovable = immovable,
            HeadHeight = headHeight,
            RadiusAdd = 0f,
            RadiusMul = 1f,
            MassMul = 1f
        };

        public static readonly BodyComponent Default = Create(0.3f, 1f);
    }
}
