using System;
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Core.ECS.Entities
{
    public class EntityQuery
    {
        private readonly EntityManager _entityManager;
        private readonly Type[] _componentTypes;
        private IEnumerable<IChunk> _cachedChunks;
        private int _lastArchetypeCount;

        public EntityQuery(EntityManager em, params Type[] types)
        {
            _entityManager = em;
            _componentTypes = types;
        }

        public IEnumerable<IChunk> GetChunks()
        {
            // Archetype 개수가 바뀌면 재캐싱
            int currentCount = _entityManager.ArchetypeCount; // 추가 필요
            if (_cachedChunks == null || _lastArchetypeCount != currentCount)
            {
                _cachedChunks = _entityManager.GetChunks(_componentTypes);
                _lastArchetypeCount = currentCount;
            }
            return _cachedChunks;
        }
    }
}