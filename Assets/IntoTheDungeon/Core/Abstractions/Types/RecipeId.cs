using System;

namespace IntoTheDungeon.Core.Abstractions.Types
{
    // 이름→ID 변환은 더 이상 이 타입의 책임이 아님. IRecipeTable(RecipeTable)이 유일한 발급처.
    public readonly struct RecipeId : IEquatable<RecipeId>
    {
        public readonly int Value;

        public RecipeId(int value) => Value = value;

        // 미해석 sentinel. RecipeTable이 발급하는 실제 id는 항상 1 이상이라 충돌하지 않는다.
        public static readonly RecipeId Default = new RecipeId(0);

        public bool Equals(RecipeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RecipeId id && Equals(id);
        public override int GetHashCode() => Value;

        public static bool operator ==(RecipeId left, RecipeId right) => left.Value == right.Value;
        public static bool operator !=(RecipeId left, RecipeId right) => left.Value != right.Value;

        public static implicit operator int(RecipeId id) => id.Value;
        public static implicit operator RecipeId(int value) => new RecipeId(value);

        public override string ToString() => $"RecipeId({Value})";
    }
}