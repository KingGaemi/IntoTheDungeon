using System;
using System.Collections.Generic;

namespace IntoTheDungeon.Core.Abstractions.Types
{
    public readonly struct RecipeId : IEquatable<RecipeId>
    {
        public readonly int Value;

        public RecipeId(int value) => Value = value;

        public static readonly RecipeId Default = new RecipeId(0x1000);

        // 디버깅을 위해 해시값과 원본 문자열을 매핑해 두는 정적 딕셔너리
        private static readonly Dictionary<int, string> _debugNames = new Dictionary<int, string>();

        public static RecipeId FromString(string name)
        {
            if (string.IsNullOrEmpty(name)) return Default;

            int hash = ComputeHash(name);

            // 원본 이름 캐싱 (디버그 용도)
            lock (_debugNames)
            {
                if (!_debugNames.ContainsKey(hash))
                {
                    _debugNames[hash] = name;
                }
            }

            return new RecipeId(hash);
        }

        private static int ComputeHash(string name)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in name)
                {
                    hash = hash * 31 + c;
                }
                return hash;
            }
        }

        public bool Equals(RecipeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RecipeId id && Equals(id);
        public override int GetHashCode() => Value;

        public static bool operator ==(RecipeId left, RecipeId right) => left.Value == right.Value;
        public static bool operator !=(RecipeId left, RecipeId right) => left.Value != right.Value;

        public static implicit operator int(RecipeId id) => id.Value;
        public static implicit operator RecipeId(int value) => new RecipeId(value);

        public override string ToString()
        {
            // 등록된 이름이 있으면 이름과 해시값을 함께 표시
            if (_debugNames.TryGetValue(Value, out var name))
            {
                return $"RecipeId({name}:{Value})";
            }

            if (Value == Default.Value)
            {
                return "RecipeId(Default)";
            }

            return $"RecipeId({Value})";
        }
    }
}