using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.Attack
{
    public struct AOEComponent : IComponentData
    {
        public Entity Owner;
        public int Damage;

        public float Radius;

        // 생명주기
        public float Duration;
        public float ElapsedTime;

        // 틱 데미지: 0이면 즉발 한 번, >0이면 매 틱마다 적용
        public float TickInterval;
        public float TickAccum;

        // 생성 직후 1회 적용 여부(즉발+틱 겸용 가능)
        public bool ApplyOnSpawn;
    }
}
