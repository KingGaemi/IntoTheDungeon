namespace IntoTheDungeon.Features.Status
{
    public struct StatusModification
    {
        public enum Type
        {
            Damage,
            Heal,
            SetHp,
            AttackSpeed,
            MovementSpeed,
            Armor,
            ModifyDamage,
            ModifyArmor,
            ModifyAttackSpeed,
            ModifyMovementSpeed,
            Init
        }

        public Type ModType;
        public float Value;
        public bool IsAbsolute; // true: 절대값, false: 상대값

        // Factory methods
        public static StatusModification Damage(float amount)
            => new()
            { ModType = Type.Damage, Value = amount };

        public static StatusModification Heal(float amount)
            => new()
            { ModType = Type.Heal, Value = amount };

        public static StatusModification SetHp(float value)
            => new()
            { ModType = Type.SetHp, Value = value, IsAbsolute = true };

        public static StatusModification AddDamage(float amount)
            => new()
            { ModType = Type.ModifyDamage, Value = amount };

        public static StatusModification AddAttackSpeed(float amount)
            => new()
            { ModType = Type.AttackSpeed, Value = amount };
        public static StatusModification AddMovementSpeed(float amount)
            => new()
            { ModType = Type.MovementSpeed, Value = amount };

        public static StatusModification Init()
            => new()
            { ModType = Type.Init };
    }
}