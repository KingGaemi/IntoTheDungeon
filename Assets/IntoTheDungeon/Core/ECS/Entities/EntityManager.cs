using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Debug;

namespace IntoTheDungeon.Core.ECS.Entities
{
    // ============================================
    // EntityManager - Entity 생성/제거/수정
    // ============================================

    /// <summary>
    /// Entity의 생명주기 및 Component 관리
    /// </summary>
    public sealed class EntityManager : IEntityManager, IManagedComponentStore, IEntityDebugView
    {
        private readonly List<EntityMetadata> _entityMetadata = new();
        private readonly Queue<int> _freeIndices = new();
        private readonly List<Archetype> _archetypes = new();
        private int _entityCount;

        // 빈 Archetype (Component 없는 Entity)
        private Archetype _emptyArchetype;

        private readonly Dictionary<Entity, Dictionary<Type, IManagedComponent>> _managedComponents
            = new();

        public int EntityCount => _entityCount;

        public int ArchetypeCount => _archetypes.Count;

        IViewOpQueue _viewOps;
        public IViewOpQueue ViewOps => _viewOps;

        public EntityManager()
        {
            _emptyArchetype = GetOrCreateArchetype(Array.Empty<Type>());
            _viewOps = new ViewOpQueue(512);
        }

        // ============================================
        // Entity 생성/제거
        // ============================================

        /// <summary>
        /// 새 Entity 생성 (Component 없음)
        /// </summary>
        public Entity CreateEntity()
        {
            return CreateEntity(_emptyArchetype);
        }

        /// <summary>
        /// 특정 Component들을 가진 Entity 생성
        /// </summary>
        public Entity CreateEntity(params Type[] componentTypes)
        {
            var archetype = GetOrCreateArchetype(componentTypes);
            return CreateEntity(archetype);
        }

        private Entity CreateEntity(Archetype archetype)
        {
            // Entity 메타데이터 할당
            int index;
            int version;

            if (_freeIndices.Count > 0)
            {
                index = _freeIndices.Dequeue();
                version = _entityMetadata[index].Version;
            }
            else
            {
                index = _entityMetadata.Count;
                _entityMetadata.Add(default);
                version = 1;
            }

            var entity = new Entity(index, version);

            // Chunk에 공간 할당
            var (chunk, indexInChunk) = archetype.AllocateEntity();
            chunk.SetEntity(indexInChunk, entity);

            // 메타데이터 업데이트
            _entityMetadata[index] = new EntityMetadata
            {
                Chunk = chunk,
                IndexInChunk = indexInChunk,
                Version = version
            };


            _entityCount++;
            return entity;
        }

        /// <summary>
        /// Entity 복사 (Instantiate)
        /// </summary>
        public Entity Instantiate(Entity source)
        {
            if (!Exists(source))
                throw new ArgumentException("Source entity does not exist");

            var srcMeta = _entityMetadata[source.Index];
            var archetype = srcMeta.Chunk.Archetype;

            // 새 Entity 생성
            var newEntity = CreateEntity(archetype);
            var dstMeta = _entityMetadata[newEntity.Index];

            // Component 복사
            foreach (var componentType in archetype.ComponentTypes)
            {
                var srcArray = srcMeta.Chunk.GetComponentArray(componentType);
                var dstArray = dstMeta.Chunk.GetComponentArray(componentType);

                var srcValue = srcArray.GetValue(srcMeta.IndexInChunk);
                dstArray.SetValue(srcValue, dstMeta.IndexInChunk);
            }

            if (_managedComponents.TryGetValue(source, out var srcManaged))
            {
                foreach (var kvp in srcManaged)
                {
                    // Deep Copy 여부는 Component 타입에 따라 결정
                    // 일반적으로 Managed Component는 참조 공유
                    if (!_managedComponents.ContainsKey(newEntity))
                        _managedComponents[newEntity] = new Dictionary<Type, IManagedComponent>();

                    _managedComponents[newEntity][kvp.Key] = kvp.Value;
                }
            }

            return newEntity;
        }

        /// <summary>
        /// Entity 제거
        /// </summary>
        public void DestroyEntity(Entity entity)
        {
            if (!Exists(entity))
                return;


            // Managed Component 정리
            if (_managedComponents.TryGetValue(entity, out var components))
            {
                foreach (var kvp in components)
                {
                    if (kvp.Value is IDisposable disposable)
                        disposable.Dispose();
                }
                _managedComponents.Remove(entity);
            }


            var meta = _entityMetadata[entity.Index];
            var chunk = meta.Chunk;
            var archetype = chunk.Archetype;

            // Chunk에서 제거 (Swap-back)
            var movedEntity = archetype.RemoveEntity(chunk, meta.IndexInChunk);

            // Swap된 Entity의 메타데이터 업데이트
            if (movedEntity != Entity.Null)
            {
                var movedMeta = _entityMetadata[movedEntity.Index];
                movedMeta.IndexInChunk = meta.IndexInChunk;
                _entityMetadata[movedEntity.Index] = movedMeta;
            }

            // 현재 Entity 메타데이터 무효화
            _entityMetadata[entity.Index] = new EntityMetadata
            {
                Chunk = null,
                Version = meta.Version + 1
            };

            _freeIndices.Enqueue(entity.Index);
            _entityCount--;
        }

        /// <summary>
        /// Entity 존재 확인
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(Entity entity)
        {
            if (entity.Index < 0 || entity.Index >= _entityMetadata.Count)
                return false;

            var meta = _entityMetadata[entity.Index];
            return meta.IsAlive && meta.Version == entity.Version;
        }

        // ============================================
        // Component 추가/제거 (Structural Change)
        // ============================================

        /// <summary>
        /// Component 추가 (다른 Archetype으로 이동)
        /// </summary>
        public void AddComponent<T>(Entity entity) where T : struct, IComponentData
        {
            AddComponent(entity, default(T));
        }

        public void AddComponent<T>(Entity entity, T component) where T : struct, IComponentData
        {
            if (!Exists(entity))
                throw new ArgumentException("Entity does not exist");

            if (HasComponent<T>(entity))
                return;

            var meta = _entityMetadata[entity.Index];
            var oldArchetype = meta.Chunk.Archetype;

            // 새 Archetype 찾기/생성
            var newComponentTypes = new List<Type>(oldArchetype.ComponentTypes) { typeof(T) };
            var newArchetype = GetOrCreateArchetype(newComponentTypes.ToArray());

            // Entity 이동
            MoveEntity(entity, newArchetype);

            if (component is INeedInit needInit)
            {
                needInit.Initialize();
                component = (T)(object)needInit;  //  boxing → unboxing으로 다시 대입
            }

            // 새 Component 설정
            SetComponent(entity, component);
        }

        /// <summary>
        /// Component 제거 (다른 Archetype으로 이동)
        /// </summary>
        public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
        {
            if (!Exists(entity))
                throw new ArgumentException("Entity does not exist");

            if (!HasComponent<T>(entity))
                return;

            var meta = _entityMetadata[entity.Index];
            var oldArchetype = meta.Chunk.Archetype;

            // 새 Archetype 찾기/생성
            var newComponentTypes = new List<Type>(oldArchetype.ComponentTypes);
            newComponentTypes.Remove(typeof(T));
            var newArchetype = GetOrCreateArchetype(newComponentTypes.ToArray());

            // Entity 이동
            MoveEntity(entity, newArchetype);
        }


        // ManagedComponent
        public void AddManagedComponent<T>(Entity entity, T component) where T : class, IManagedComponent
        {

            if (!Exists(entity))
                throw new ArgumentException($"Entity {entity} does not exist");

            // Entity별 Dictionary 생성
            if (!_managedComponents.TryGetValue(entity, out var components))
            {
                components = new Dictionary<Type, IManagedComponent>();
                _managedComponents[entity] = components;
            }

            // Type을 Key로 저장
            var type = typeof(T);
            if (components.ContainsKey(type))
                throw new InvalidOperationException($"Entity {entity} already has {type.Name}");
            if (component is INeedInit needInit)
            {
                needInit.Initialize();
            }

            components[type] = component;
        }
        // Managed Component 조회
        public T GetManagedComponent<T>(Entity entity) where T : class, IManagedComponent
        {
            if (!_managedComponents.TryGetValue(entity, out var components))
                throw new InvalidOperationException($"Entity {entity} has no managed components");

            var type = typeof(T);
            if (!components.TryGetValue(type, out var component))
                throw new InvalidOperationException($"Entity {entity} does not have {type.Name}");

            return (T)component;
        }

        public bool TryGetManagedComponent<T>(Entity entity, out T component) where T : class
        {
            component = null;

            if (!_managedComponents.TryGetValue(entity, out var components))
                return false;

            var type = typeof(T);
            if (!components.TryGetValue(type, out var obj))
                return false;

            component = (T)obj;
            return true;
        }


        // Managed Component 존재 확인
        public bool HasManagedComponent<T>(Entity entity) where T : class, IManagedComponent
        {
            if (!_managedComponents.TryGetValue(entity, out var components))
                return false;

            return components.ContainsKey(typeof(T));
        }

        // Managed Component 제거
        public void RemoveManagedComponent<T>(Entity entity) where T : class, IManagedComponent
        {
            if (!_managedComponents.TryGetValue(entity, out var components))
                return;

            var type = typeof(T);
            if (components.TryGetValue(type, out var component))
            {
                // IDisposable 처리
                if (component is IDisposable disposable)
                    disposable.Dispose();

                components.Remove(type);

                // 빈 Dictionary 정리
                if (components.Count == 0)
                    _managedComponents.Remove(entity);
            }
        }

        /// <summary>
        /// Entity를 다른 Archetype으로 이동 (Component 데이터 유지)
        /// </summary>
        private void MoveEntity(Entity entity, Archetype newArchetype)
        {
            var oldMeta = _entityMetadata[entity.Index];
            var oldChunk = oldMeta.Chunk;
            var oldArchetype = oldChunk.Archetype;

            // 새 Chunk에 공간 할당
            var (newChunk, newIndex) = newArchetype.AllocateEntity();
            newChunk.SetEntity(newIndex, entity);

            // 공통 Component 복사
            foreach (var componentType in newArchetype.ComponentTypes)
            {
                if (oldArchetype.HasComponent(componentType))
                {
                    var oldArray = oldChunk.GetComponentArray(componentType);
                    var newArray = newChunk.GetComponentArray(componentType);

                    var value = oldArray.GetValue(oldMeta.IndexInChunk);
                    newArray.SetValue(value, newIndex);
                }
            }

            // 이전 Chunk에서 제거
            var movedEntity = oldArchetype.RemoveEntity(oldChunk, oldMeta.IndexInChunk);

            // Swap된 Entity 메타데이터 업데이트
            if (movedEntity != Entity.Null)
            {
                var movedMeta = _entityMetadata[movedEntity.Index];
                movedMeta.IndexInChunk = oldMeta.IndexInChunk;
                _entityMetadata[movedEntity.Index] = movedMeta;
            }

            // 현재 Entity 메타데이터 업데이트
            _entityMetadata[entity.Index] = new EntityMetadata
            {
                Chunk = newChunk,
                IndexInChunk = newIndex,
                Version = oldMeta.Version
            };
        }

        // ============================================
        // Component 조회/수정
        // ============================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasComponent<T>(Entity entity) where T : struct, IComponentData
        {
            if (!Exists(entity))
                return false;

            var meta = _entityMetadata[entity.Index];
            return meta.Chunk.Archetype.HasComponent(typeof(T));
        }

        public ref T GetComponent<T>(Entity entity) where T : struct, IComponentData
        {
            if (!Exists(entity))
                throw new ArgumentException("Entity does not exist");

            var meta = _entityMetadata[entity.Index];

            if (!meta.Chunk.Archetype.HasComponent(typeof(T)))
                throw new InvalidOperationException($"Entity does not have component {typeof(T).Name}");

            return ref meta.Chunk.GetComponent<T>(meta.IndexInChunk);
        }

        public void SetComponent<T>(Entity entity, T component) where T : struct, IComponentData
        {
            GetComponent<T>(entity) = component;
        }

        public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponentData
        {
            if (Exists(entity) && HasComponent<T>(entity))
            {
                component = GetComponent<T>(entity);
                return true;
            }
            component = default;
            return false;
        }

        // ============================================
        // Archetype 관리
        // ============================================

        private Archetype GetOrCreateArchetype(Type[] componentTypes)
        {
            // 기존 Archetype 찾기
            foreach (var archetype in _archetypes)
            {
                if (archetype.Matches(componentTypes))
                    return archetype;
            }

            // 새 Archetype 생성
            var newArchetype = new Archetype(componentTypes);
            _archetypes.Add(newArchetype);
            return newArchetype;
        }
        public IArchetype GetArchetype(Entity entity)
        {
            var meta = _entityMetadata[entity.Index];

            return meta.Chunk.Archetype;
        }

        public IReadOnlyList<IArchetype> GetArchetypes()
        {
            return _archetypes;
        }


        // ============================================
        // Query (다음 단계에서 구현)
        // ============================================

        /// <summary>
        /// 특정 Component를 가진 모든 Archetype의 Chunk 수집
        /// </summary>
        public IEnumerable<IChunk> GetChunks(params Type[] componentTypes)
        {
            foreach (var archetype in _archetypes)
            {
                bool matches = true;
                foreach (var type in componentTypes)
                {
                    if (!archetype.HasComponent(type))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    foreach (var chunk in archetype.Chunks)
                    {
                        if (chunk.Count > 0)
                            yield return chunk;
                    }
                }
            }
        }

        public IEnumerable<IChunk> GetChunks(IArchetype archetype)
        {
            foreach (var chunk in archetype.Chunks)
            {
                if (chunk.Count > 0)
                    yield return chunk;
            }
        }

        /// <summary>
        /// 디버그: 모든 Archetype 정보
        /// </summary>
        public void DebugPrintArchetypes()
        {
            UnityEngine.Debug.Log($"=== EntityManager: {_entityCount} entities, {_archetypes.Count} archetypes ===");
            foreach (var archetype in _archetypes)
            {
                var types = string.Join(", ", Array.ConvertAll(archetype.ComponentTypes.ToArray(), t => t.Name));
                UnityEngine.Debug.Log($"  Archetype [{types}]: {archetype.Chunks.Count} chunks");
            }
        }


        public void Dispose()
        {
            UnityEngine.Debug.Log($"[EntityManager] Disposing... {_entityCount} entities");

            // 1. 모든 살아있는 Entity 제거
            // 역순으로 처리하여 Index 이슈 방지
            var entitiesToDestroy = new List<Entity>(_entityCount);

            for (int i = 0; i < _entityMetadata.Count; i++)
            {
                var meta = _entityMetadata[i];
                if (meta.IsAlive)
                {
                    entitiesToDestroy.Add(new Entity(i, meta.Version));
                }
            }

            foreach (var entity in entitiesToDestroy)
            {
                DestroyEntity(entity);
            }

            // Just in case
            foreach (var kvp in _managedComponents.Values)
            {
                foreach (var component in kvp.Values)
                {
                    if (component is IDisposable disposable)
                        disposable.Dispose();
                }
            }
            _managedComponents.Clear();


            // 2. Archetype/Chunk 정리
            // (이미 DestroyEntity에서 Chunk가 비워졌지만 명시적 정리)
            _archetypes.Clear();

            // 3. 메타데이터 정리
            _entityMetadata.Clear();
            _freeIndices.Clear();

            // 4. 빈 Archetype 무효화
            _emptyArchetype = null;
            _entityCount = 0;


            UnityEngine.Debug.Log("[EntityManager] Disposed");

        }
        public (int, int) GetEntityLocation(Entity entity)
        {
            var meta = _entityMetadata[entity.Index];
            return (meta.Chunk.Index, meta.IndexInChunk);
        }
    }
}


