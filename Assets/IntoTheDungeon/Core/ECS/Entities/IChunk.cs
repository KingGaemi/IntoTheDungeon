using System;
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Core.ECS.Entities
{ 
    public interface IChunk
    {
        int Count { get; }
        int Capacity { get; }
        int Index { get; }
        
        // Chunk 내 Entity 배열 반환
        IReadOnlyList<Entity> GetEntities();

        ref T GetComponent<T>(int indexInChunk) 
        where T : struct, IComponentData;
        // Generic Component 배열 접근
        T[] GetComponentArray<T>() where T : struct, IComponentData;

        // Component 배열 반환 (Reflection용)
        Array GetComponentArray(Type componentType);
    }
}