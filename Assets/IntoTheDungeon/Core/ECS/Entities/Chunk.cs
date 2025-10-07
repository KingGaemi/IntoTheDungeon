using System.Collections.Generic;
using System;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Core.ECS.Entities
{
    // ============================================
    // Chunk - 16KB 메모리 블록
    // ============================================

    /// <summary>
    /// Entity들을 저장하는 16KB 메모리 블록 (최대 128개)
    /// </summary>
    public sealed class Chunk : IChunk
    {
        private readonly Archetype _archetype;
        private readonly Entity[] _entities;
        private readonly Dictionary<Type, Array> _componentArrays = new();

        public Archetype Archetype => _archetype;
        public int Capacity { get; }
        public int Count { get; internal set; }

        public int Index { get; }

        internal Chunk(Archetype archetype, int capacity)
        {
            _archetype = archetype;
            Capacity = capacity;
            _entities = new Entity[capacity];

            // Component 타입별 배열 생성
            foreach (var componentType in archetype.ComponentTypes)
            {
                var array = Array.CreateInstance(componentType, capacity);
                _componentArrays[componentType] = array;
            }
        }

        internal Entity GetEntity(int index) => _entities[index];
        public IReadOnlyList<Entity> GetEntities() => _entities;
        internal void SetEntity(int index, Entity entity) => _entities[index] = entity;

        /// <summary>
        /// Component 배열 가져오기
        /// </summary>

        ///  Generic 버전
        public T[] GetComponentArray<T>() where T : struct, IComponentData
        {
            if (!_componentArrays.TryGetValue(typeof(T), out var array))
                throw new InvalidOperationException(
                    $"Component type {typeof(T).Name} not found in chunk archetype");

            return (T[])array;

        }
        
        // Type 객체를 받는 오버로드
        public Array GetComponentArray(Type componentType)
        {
            return _componentArrays.TryGetValue(componentType, out var array)
                ? array
                : null;
        }


        /// <summary>
        /// 두 Entity의 위치를 Swap (Structural Change)
        /// </summary>
        internal void SwapEntities(int indexA, int indexB)
        {
            // Entity ID Swap
            (_entities[indexA], _entities[indexB]) = (_entities[indexB], _entities[indexA]);

            // 모든 Component Swap
            foreach (var kvp in _componentArrays)
            {
                var array = kvp.Value;
                var temp = array.GetValue(indexA);
                array.SetValue(array.GetValue(indexB), indexA);
                array.SetValue(temp, indexB);
            }
        }

        /// <summary>
        /// Component 설정
        /// </summary>
        internal void SetComponent<T>(int index, T component) where T : struct , IComponentData
        {
            var array = GetComponentArray<T>();
            array[index] = component;
        }

        /// <summary>
        /// Component 가져오기
        /// </summary>
        public ref T GetComponent<T>(int index) where T : struct , IComponentData
        {
            var array = GetComponentArray<T>();
            return ref array[index];
        }
    }

}