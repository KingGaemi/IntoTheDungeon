/// <summary>
/// 특정 Component 조합을 가진 Entity들의 저장소
/// </summary>
using System;
using System.Collections.Generic;


namespace IntoTheDungeon.Core.ECS.Entities
{
    public sealed class Archetype : IArchetype
    {
        private readonly Type[] _componentTypes;
        private readonly List<Chunk> _chunks = new();
        private readonly int _entityCapacityPerChunk;

        public IReadOnlyList<Type> ComponentTypes => _componentTypes;
        public IReadOnlyList<IChunk> Chunks => _chunks;

        internal Archetype(Type[] componentTypes, int entityCapacity = 128)
        {
            _componentTypes = componentTypes;
            _entityCapacityPerChunk = entityCapacity;
            Array.Sort(_componentTypes, (a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
        }

        /// <summary>
        /// 이 Archetype이 특정 Component 타입을 포함하는지
        /// </summary>
        public bool HasComponent(Type type)
        {
            return Array.IndexOf(_componentTypes, type) >= 0;
        }

        /// <summary>
        /// 새 Entity를 위한 공간 할당
        /// </summary>
        internal (Chunk chunk, int index) AllocateEntity()
        {
            // 여유 공간이 있는 Chunk 찾기
            foreach (var chunk in _chunks)
            {
                if (chunk.Count < chunk.Capacity)
                {
                    int index = chunk.Count;
                    chunk.Count++;
                    return (chunk, index);
                }
            }

            // 모든 Chunk가 가득 참 → 새 Chunk 생성
            var newChunk = new Chunk(this, _entityCapacityPerChunk);
            _chunks.Add(newChunk);
            newChunk.Count = 1;
            return (newChunk, 0);
        }

        /// <summary>
        /// Entity 제거 (Chunk에서 마지막 Entity를 옮겨서 빈 공간 채움)
        /// </summary>
        internal Entity RemoveEntity(Chunk chunk, int indexInChunk)
        {
            int lastIndex = chunk.Count - 1;
            Entity movedEntity = Entity.Null;

            // 마지막이 아니면 Swap-back
            if (indexInChunk < lastIndex)
            {
                chunk.SwapEntities(indexInChunk, lastIndex);
                movedEntity = chunk.GetEntity(indexInChunk);
            }

            chunk.Count--;

            // Chunk가 비었으면 제거
            if (chunk.Count == 0)
            {
                _chunks.Remove(chunk);
            }

            return movedEntity; // Swap된 Entity (메타데이터 업데이트용)
        }

        /// <summary>
        /// Archetype 매칭 (정확히 동일한 Component 조합)
        /// </summary>
        internal bool Matches(Type[] types)
        {
            if (_componentTypes.Length != types.Length)
                return false;

            Array.Sort(types, (a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));

            for (int i = 0; i < _componentTypes.Length; i++)
                if (_componentTypes[i] != types[i])
                    return false;

            return true;
        }
    }
}