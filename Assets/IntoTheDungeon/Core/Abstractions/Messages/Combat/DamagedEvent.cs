using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    public readonly struct DamagedEvent
    {
        public readonly Entity Victim;
        public readonly Entity Source;     // 없으면 Entity.Null
        public readonly int Amount;
        public readonly int RemainingHp;

        public DamagedEvent(Entity victim, Entity source, int amount, int remainingHp)
        { Victim = victim; Source = source; Amount = amount; RemainingHp = remainingHp; }
    }
}
