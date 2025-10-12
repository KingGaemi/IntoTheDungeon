using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Status.Computed
{
    public struct ComputedStats : IComponentData
    {
        public float FinalMoveSpeed;
        public float FinalAttackSpeed;
        public float ProjectileSpeed;
        public float ProjectileLifeTime;
        public float AnimSpeed;
        public float AnimDuration;
    }
}