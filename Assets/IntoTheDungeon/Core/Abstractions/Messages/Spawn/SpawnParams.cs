using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Abstractions.Messages.Spawn
{
    public struct SpawnParams
    {
        public int PhysHandle;
        public string Name;
        public Vec2 Pos, Dir;
        public int MaxHp;
        public float MovSpd, AtkSpd;
    }
}