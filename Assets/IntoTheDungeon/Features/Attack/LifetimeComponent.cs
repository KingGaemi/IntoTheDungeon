using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 남은 시간이 0 이하가 되면 LifetimeSystem이 엔티티를 제거한다.
    /// </summary>
    public struct LifetimeComponent : IComponentData
    {
        public float Remaining;
    }
}
