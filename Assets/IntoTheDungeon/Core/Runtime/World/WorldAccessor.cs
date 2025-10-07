#if false
namespace IntoTheDungeon.Core.Runtime.World
{
    public static class WorldAccessor
    {
        public static GameWorld Current { get; private set; }
        public static void Set(GameWorld w) => Current = w;
    }
}
#endif