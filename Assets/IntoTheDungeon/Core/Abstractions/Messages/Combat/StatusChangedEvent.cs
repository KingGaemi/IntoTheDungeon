using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Core.Abstractions.Messages.Combat
{
    public enum StatusDirty : byte { None = 0, All = 1, Damage = 2, Armor = 4, AtkSpd = 8, MovSpd = 16, Hp = 32 }

    public readonly struct StatusChangedEvent
    {
        public readonly Entity E;
        public readonly StatusDirty Dirty;
        public readonly int Damage;
        public readonly int Armor;
        public readonly float AttackSpeed;
        public readonly float MovementSpeed;
        public StatusChangedEvent(Entity e, StatusDirty d, int dmg, int arm, float aspd, float mspd)
        { E = e; Dirty = d; Damage = dmg; Armor = arm; AttackSpeed = aspd; MovementSpeed = mspd; }
    }
}