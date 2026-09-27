using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.Runtime.ECS
{
    // 이름 기반 순번 매핑 - 해시 충돌 없음 보장 (NameTable과 동일 패턴)
    public sealed class RecipeTable : IRecipeTable
    {
        private readonly Dictionary<string, RecipeId> _nameToId = new();
        private readonly List<string> _idToName = new() { null }; // 0번 인덱스는 예약(= 미해석 sentinel과 구분)

        public RecipeId GetId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return RecipeId.Default;

            if (_nameToId.TryGetValue(name, out var existingId))
                return existingId;

            var newId = new RecipeId(_idToName.Count);
            _idToName.Add(name);
            _nameToId[name] = newId;

            return newId;
        }

        public bool TryGetId(string name, out RecipeId id)
            => _nameToId.TryGetValue(name, out id);

        public string GetName(RecipeId id)
        {
            if (id.Value <= 0 || id.Value >= _idToName.Count)
                return $"Unknown_{id.Value}";

            return _idToName[id.Value];
        }

        public int Count => _idToName.Count - 1;
    }
}
