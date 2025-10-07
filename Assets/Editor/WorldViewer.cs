// Editor/WorldViewer.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS;


namespace IntoTheDungeon.Editor.ECS
{
    /// <summary>
    /// World를 관찰만 하는 읽기 전용 인터페이스
    /// Chunk 기반 메모리 레이아웃 완전 노출
    /// </summary>
    public class WorldViewer
    {
        readonly GameWorld _world;

        public WorldViewer(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        //  읽기 전용 API
        public int EntityCount => _world.EntityManager.EntityCount;
        public int ArchetypeCount => _world.EntityManager.ArchetypeCount;

        //  Chunk 기반 순회 (성능 최적화)
        public IEnumerable<ArchetypeView> GetArchetypeViews()
        {
            foreach (var archetype in _world.EntityManager.GetArchetypes())
            {
                yield return new ArchetypeView(archetype);
            }
        }

        //  Entity 조회도 Chunk 단위로
        public IEnumerable<EntityView> GetAllEntityViews()
        {
            foreach (var archetypeView in GetArchetypeViews())
            {
                foreach (var chunkView in archetypeView.Chunks)
                {
                    foreach (var entityView in chunkView.Entities)
                    {
                        yield return entityView;
                    }
                }
            }
        }

        //  특정 Entity만 조회 (빠른 접근)
        public EntityView? GetEntityView(Entity entity)
        {
            // if (!entity.IsValid()) return null;

            var archetype = _world.EntityManager.GetArchetype(entity);
            if (archetype == null) return null;

            return new EntityView(entity, _world);
        }

        public IEnumerable<SystemView> GetSystemViews()
        {
    
            foreach (var sys in _world.SystemManager.GetSystems())
            {
                yield return new SystemView(sys);
            }
        }

        //  Component 조회 (읽기)
        public T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            return _world.EntityManager.GetComponent<T>(entity);
        }

        public object GetComponentBoxed(Entity entity, Type componentType)
        {
            // EntityManager가 GetComponent<T> 메서드를 가져야 함
            var method = typeof(IntoTheDungeon.Core.Runtime.ECS.Manager.EntityManager)
                .GetMethod(nameof(IntoTheDungeon.Core.Runtime.ECS.Manager.EntityManager.GetComponent));

            if (method == null)
                throw new InvalidOperationException("EntityManager.GetComponent method not found");

            var generic = method.MakeGenericMethod(componentType);

            // ref 반환이라 Invoke는 boxing된 값 반환
            return generic.Invoke(_world.EntityManager, new object[] { entity });
        }
    }

    // ========================================
    // View Models - Chunk 구조 완전 노출
    // ========================================

    /// <summary>
    /// Archetype + Chunks 정보
    /// </summary>
    public class ArchetypeView
    {
        public IReadOnlyList<Type> ComponentTypes { get; }
        public int ChunkCount { get; }
        public ChunkView[] Chunks { get; }

        //  메모리 통계
        public int EstimatedMemoryBytes { get; }
        public float AverageChunkUtilization { get; }

        public ArchetypeView(IArchetype archetype)
        {
            ComponentTypes = archetype.ComponentTypes;

            //  Chunk 정보 추출
            Chunks = archetype.Chunks
                .Select(chunk => new ChunkView(chunk, archetype))
                .ToArray();

            ChunkCount = Chunks.Length;

            //  메모리 통계 계산
            EstimatedMemoryBytes = CalculateMemoryUsage(archetype);
            AverageChunkUtilization = Chunks.Length > 0
                ? Chunks.Average(c => c.UtilizationPercent)
                : 0f;
        }

        int CalculateMemoryUsage(IArchetype archetype)
        {
            const int CHUNK_SIZE = 16 * 1024; // 16KB
            return ChunkCount * CHUNK_SIZE;
        }

        public string GetSignature()
        {
            return string.Join(", ", ComponentTypes.Select(t => t.Name));
        }
    }

    /// <summary>
    /// Chunk 단위 정보
    /// </summary>
    public class ChunkView
    {
        public int EntityCount { get; }
        public int Capacity { get; }
        public float UtilizationPercent => (float)EntityCount / Capacity * 100f;

        public EntityView[] Entities { get; }
        public IReadOnlyList<Type> ComponentTypes { get; }

        //  Chunk 메모리 정보
        public int ChunkIndex { get; }
        public bool IsFull => EntityCount >= Capacity;

        public ChunkView(IChunk chunk, IArchetype archetype)
        {
            EntityCount = chunk.Count;
            Capacity = chunk.Capacity;
            ChunkIndex = chunk.Index; // Chunk에 Index 필드 필요
            ComponentTypes = archetype.ComponentTypes;

            //  Chunk 내 모든 Entity 조회
            Entities = new EntityView[EntityCount];
            var entities = chunk.GetEntities(); // IChunk.GetEntities() 필요

            for (int i = 0; i < EntityCount; i++)
            {
                Entities[i] = new EntityView(entities[i], archetype);
            }
        }
    }

    /// <summary>
    /// Entity 개별 정보
    /// </summary>
    public struct EntityView
    {
        public Entity Entity { get; }
        public int ComponentCount { get; }
        public IReadOnlyList<Type> ComponentTypes { get; }

        // Chunk 소속 정보
        public int ChunkIndex { get; }
        public int IndexInChunk { get; }

        public EntityView(Entity entity, GameWorld world)
        {
            Entity = entity;

            var archetype = world.EntityManager.GetArchetype(entity);
            ComponentTypes = archetype?.ComponentTypes ?? Array.Empty<Type>();
            ComponentCount = ComponentTypes.Count;

            // ✅ Chunk 위치 정보 (EntityManager 확장 필요)
            (ChunkIndex, IndexInChunk) = world.EntityManager.GetEntityLocation(entity);
        }

        public EntityView(Entity entity, IArchetype archetype)
        {
            Entity = entity;
            ComponentTypes = archetype?.ComponentTypes ?? Array.Empty<Type>();
            ComponentCount = ComponentTypes.Count;

            // Chunk 정보는 별도로 설정 필요
            ChunkIndex = -1;
            IndexInChunk = -1;
        }
    }
    
    public class SystemView
    {
        public string Name { get; }
        public string ShortName { get; }
        public int Priority { get; }
        
        public bool RunsInUpdate { get; }
        public bool RunsInFixedUpdate { get; }
        public bool RunsInLateUpdate { get; }
        
        public double LastExecutionTimeMs { get; }
        public double AverageExecutionTimeMs { get; }
        public long TotalExecutionCount { get; }
        
        public bool IsEnabled { get; }
        
        public SystemView(IGameSystem system)
        {
            Name = system.GetType().FullName;
            ShortName = system.GetType().Name;
            Priority = system.Priority;

        }
        
        public string GetExecutionPhase()
        {
            var phases = new List<string>();
            if (RunsInUpdate) phases.Add("Update");
            if (RunsInFixedUpdate) phases.Add("FixedUpdate");
            if (RunsInLateUpdate) phases.Add("LateUpdate");
            return string.Join(", ", phases);
        }
    }
}
#endif