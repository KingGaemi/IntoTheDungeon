using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Attack
{
    public struct ProjectileComponent : IComponentData
    {
        public Entity Owner;
        public int Damage;
        public Vec2 Direction;
        public float Speed;
        public float LifeTime;
        public float ElapsedTime;
        public float Rotation;
        public int Penetration;
        public bool Enable;
        public ProjectileType Type;
    }
}