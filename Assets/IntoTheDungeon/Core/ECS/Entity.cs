namespace IntoTheDungeon.Core.ECS
{
    public readonly struct Entity
    {
        public readonly int Id;
        public Entity(int id) => Id = id;
        public bool IsValid => Id > 0;
    }
}