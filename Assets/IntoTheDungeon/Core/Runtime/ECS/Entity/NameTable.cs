using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Runtime.ECS
{
    public sealed class NameTable : INameTable
    {
        private readonly Dictionary<string, NameId> _nameToId = new();
        private readonly List<string> _idToName = new();

        public NameId GetId(string name)
        {
            if (string.IsNullOrEmpty(name))
                return default;

            // 이미 등록된 경우 즉시 반환
            if (_nameToId.TryGetValue(name, out var existingId))
                return existingId;

            // 신규 등록
            var newId = new NameId(_idToName.Count);
            _idToName.Add(name);
            _nameToId[name] = newId;

            return newId;
        }

        public string GetName(NameId id)
        {
            if (id.Value < 0 || id.Value >= _idToName.Count)
                return $"Invalid_{id.Value}";

            return _idToName[id.Value];
        }

        public bool TryGetId(string name, out NameId id)
            => _nameToId.TryGetValue(name, out id);

        public void Clear()
        {
            _nameToId.Clear();
            _idToName.Clear();
        }

        // 통계 (디버깅용)
        public int Count => _idToName.Count;
    }
}