using IntoTheDungeon.Core.ECS.Components;

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

        public readonly float HpRatio => MaxHp > 0 ? (float)CurrentHp / MaxHp : 0f;
    }
}
