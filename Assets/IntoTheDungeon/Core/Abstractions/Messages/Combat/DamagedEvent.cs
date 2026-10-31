using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    public readonly struct DamagedEvent
    {
        public readonly Entity Victim;
        public readonly Entity Source;     // 없으면 Entity.Null
        public readonly int Amount;
        public readonly int RemainingHp;
        public readonly int MaxHp;

        public float Ratio => MaxHp > 0 ? (float)Amount / MaxHp : 0f;   // 최대 HP 대비 피해 비율

        public DamagedEvent(Entity victim, Entity source, int amount, int remainingHp, int maxHp)
        { Victim = victim; Source = source; Amount = amount; RemainingHp = remainingHp; MaxHp = maxHp; }
    }
}
