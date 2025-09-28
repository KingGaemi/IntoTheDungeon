
namespace MyGame.GamePlay.Entity.Abstractions
{
    public enum Facing2D { Left, Right }

    public static class Facing2DExt
    {
        public static Facing2D FromX(float x) => x < 0 ? Facing2D.Left : Facing2D.Right;
        public static int ToSign(this Facing2D f) => f == Facing2D.Left ? -1 : 1;
        public static UnityEngine.Vector2 ToVector2(this Facing2D f)
        => new UnityEngine.Vector2(f.ToSign(), 0f);
    }
}
