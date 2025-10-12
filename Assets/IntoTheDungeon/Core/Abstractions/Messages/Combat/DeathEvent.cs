using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    public readonly struct DeathEvent
    {
        public readonly Entity Victim;
        public readonly Entity Killer; // 편의상 함께 포함
        public DeathEvent(Entity victim, Entity killer)
        { Victim=victim; Killer=killer; }
    }
}
