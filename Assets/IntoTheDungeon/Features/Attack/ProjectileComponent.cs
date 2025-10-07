using IntoTheDungeon.Core.Util.Physics;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Entities;

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

        public ProjectileType Type;
        

    }
}