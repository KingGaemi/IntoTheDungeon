namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public enum CollisionLayer
    {
        Player = 1 << 0,
        Enemy = 1 << 1,
        Projectile = 1 << 2,
        Wall = 1 << 3
    }
}