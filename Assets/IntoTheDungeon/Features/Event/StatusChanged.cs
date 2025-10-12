using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Event
{
    public readonly struct StatusChanged
    {
        public readonly Entity Entity;
        public readonly int PrevHp, CurrHp;
        public readonly bool Died;
        public readonly float AttackSpeed, MovementSpeed;
        public StatusChanged(Entity e, int prev, int curr, bool died, float aspd, float mspd)
        { Entity = e; PrevHp = prev; CurrHp = curr; Died = died; AttackSpeed = aspd; MovementSpeed = mspd; }
    }
}