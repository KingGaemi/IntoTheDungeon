
using System;

namespace IntoTheDungeon.Core.Abstractions.Messages.Spawn
{
    public struct RecipeId : IEquatable<RecipeId>
    {
        public int Value;
        public RecipeId(int value) => Value = value;

        public bool Equals(RecipeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RecipeId id && Equals(id);
        public override int GetHashCode() => Value;

        public static implicit operator int(RecipeId id) => id.Value;
        public static implicit operator RecipeId(int value) => new RecipeId(value);

        public override string ToString() => $"RecipeId({Value})";
    }
}
