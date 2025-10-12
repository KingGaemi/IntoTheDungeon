namespace IntoTheDungeon.Features.Status
{
    public struct StatusModification
    {
        public enum Type
        {
            AddAttackSpeed,
            AddMovementSpeed,
            AddArmor,
            AddDamage,
            SetDamage,
            SetArmor,
            SetAttackSpeed,
            SetMovementSpeed,
            Init
        }

        public Type ModType;
        public float Value;
        public bool IsAbsolute; // true: 절대값, false: 상대값

        // Factory methods

        public static StatusModification AddDamage(float amount)
            => new()
            { ModType = Type.AddDamage, Value = amount };

        public static StatusModification AddAttackSpeed(float amount)
            => new()
            { ModType = Type.AddAttackSpeed, Value = amount };
        public static StatusModification AddMovementSpeed(float amount)
            => new()
            { ModType = Type.AddMovementSpeed, Value = amount };

        public static StatusModification Init()
            => new()
            { ModType = Type.Init };
    }
}