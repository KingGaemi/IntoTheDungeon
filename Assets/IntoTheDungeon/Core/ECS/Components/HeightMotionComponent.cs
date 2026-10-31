using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Components
{
    /// <summary>
    /// 2.5D 높이(Z) 운동. TransformComponent.Height를 HeightMotionSystem이 이 값으로 갱신한다.
    /// </summary>
    public struct HeightMotionComponent : IComponentData
    {
        public float VelocityZ;   // 위로 +
        public float Gravity;     // 아래로 당기는 가속도(양수)
        public float JumpSpeed;   // 점프 시작 속도

        public readonly bool IsAirborne(float height) => height > 0f || VelocityZ > 0f;

        // 최고 높이 = JumpSpeed² / (2·Gravity) ≈ 1.35, 체공 = 2·JumpSpeed / Gravity = 0.6초
        public static readonly HeightMotionComponent Default = new()
        {
            VelocityZ = 0f,
            Gravity = 30f,
            JumpSpeed = 9f
        };
    }
}
