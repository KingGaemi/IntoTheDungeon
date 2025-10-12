using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Features.Status
{
    public enum StatusDirty : byte { None=0, Damage=1, Armor=2, AtkSpd=4, MovSpd=8, Hp=16 }

    public readonly struct StatusChanged
    {
        public readonly Entity E;
        public readonly StatusDirty Dirty;
        public readonly int Damage;
        public readonly int Armor;
        public readonly float AttackSpeed;
        public readonly float MovementSpeed;
        public StatusChanged(Entity e, StatusDirty d, int dmg, int arm, float aspd, float mspd)
        { E = e; Dirty = d; Damage = dmg; Armor = arm; AttackSpeed = aspd; MovementSpeed = mspd;}
    }
}