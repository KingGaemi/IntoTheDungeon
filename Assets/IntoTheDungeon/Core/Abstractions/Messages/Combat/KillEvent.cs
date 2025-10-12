using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    public enum DamageType : byte { Unknown, Attack, Spell, DoT, Environment }

    public readonly struct KillEvent
    {
        public readonly Entity Killer;    // 없으면 Entity.Null
        public readonly Entity Victim;
        public readonly int     Damage;   // 마지막 일격 피해량(옵션)
        public readonly DamageType Kind;  // 원인
        // public readonly int     SkillId;  // 0=없음
        // public readonly int     Frame;    // IClock.FrameIndex 등

        public KillEvent(Entity killer, Entity victim, int dmg, DamageType kind)
        { Killer=killer; Victim=victim; Damage=dmg; Kind = kind; }
    }

}
