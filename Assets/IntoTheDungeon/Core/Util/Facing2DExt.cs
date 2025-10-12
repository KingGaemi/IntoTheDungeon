using IntoTheDungeon.Core.Abstractions.Types;
namespace IntoTheDungeon.Core.Util
{
    public static class Facing2DExt
    {
        public static Facing2D FromX(float x) => x < 0 ? Facing2D.Left : Facing2D.Right;
        public static float ToSign(this Facing2D f) => f == Facing2D.Left ? -1f : 1f;
        public static Vec2 ToVector2(this Facing2D f)
        => new(f.ToSign(), 0f);
    }

}
