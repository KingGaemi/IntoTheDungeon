namespace IntoTheDungeon.Core.Abstractions.Gameplay
{
    public struct UnitTypeId
    {
        public int Value;
        public UnitTypeId(int v) => Value = v;
        public static implicit operator int(UnitTypeId id) => id.Value;
        public static implicit operator UnitTypeId(int v) => new(v);
    }
}