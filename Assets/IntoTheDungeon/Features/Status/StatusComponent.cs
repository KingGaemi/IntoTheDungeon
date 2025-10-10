using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Status
{
    public struct StatusComponent : IComponentData
    {
        public int MaxHp;
        public int CurrentHp;
        public int Armor;
        public int Damage;
        public float AttackSpeed;
        public float MovementSpeed;
        public bool IsAlive;

        public float ProjectileAcceleration;
        public float ProjectileLifeTime;

        public readonly float HpRatio => MaxHp > 0 ? (float)CurrentHp / MaxHp : 0f;

        public static readonly StatusComponent Default = new()
        {
            MaxHp = 100,
            CurrentHp = 100,
            Damage = 1,
            AttackSpeed = 1f,
            MovementSpeed = 1f,
            IsAlive = true,
            ProjectileAcceleration = 5f,
            ProjectileLifeTime = 1f
        };
    }
}
