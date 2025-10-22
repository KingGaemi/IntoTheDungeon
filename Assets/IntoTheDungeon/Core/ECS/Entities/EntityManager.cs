using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.Debug;


namespace IntoTheDungeon.Core.ECS.Entities
{
    /// <summary>
    /// Entity의 생명주기 및 Component 관리
    /// </summary>
    public sealed class EntityManager : IEntityManager, IEntityDebugView
    {
        #region Fields

        private readonly List<EntityMetadata> _entityMetadata = new();
        private readonly Queue<int> _freeIndices = new();
        private readonly List<Archetype> _archetypes = new();
        private readonly Dictionary<Entity, Dictionary<Type, IManagedComponent>> _managedComponents = new();

        private Archetype _emptyArchetype;
        private int _entityCount;

        #endregion

        #region Properties

        public int EntityCount => _entityCount;
        public int ArchetypeCount => _archetypes.Count;


        #endregion

        #region Constructor

        public EntityManager()
        {
            _emptyArchetype = GetOrCreateArchetype(Type.EmptyTypes);
        }

        #endregion

        #region Entity Lifecycle

        public Entity CreateEntity()
        {
            return CreateEntity(_emptyArchetype);
        }

        public Entity CreateEntity(params Type[] componentTypes)
        {
            var archetype = GetOrCreateArchetype(componentTypes);
            return CreateEntity(archetype);
        }

        private Entity CreateEntity(Archetype archetype)
        {
            if (archetype == null)
                throw new ArgumentNullException(nameof(archetype));

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
            var (chunk, indexInChunk) = archetype.AllocateEntity();
            if (chunk == null)
                throw new InvalidOperationException($"Archetype failed to allocate entity (null chunk)");

            chunk.SetEntity(indexInChunk, entity);

            _entityMetadata[index] = new EntityMetadata
            {
                Chunk = chunk,
                IndexInChunk = indexInChunk,
                Version = version
            };

            _entityCount++;
            return entity;
        }

        public Entity Instantiate(Entity source)
        {
            if (!Exists(source))
                throw new ArgumentException("Source entity does not exist");

            var srcMeta = _entityMetadata[source.Index];
            var archetype = srcMeta.Chunk.Archetype;
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

            // Managed Component 복사
            if (_managedComponents.TryGetValue(source, out var srcManaged))
            {
                foreach (var kvp in srcManaged)
                {
                    if (!_managedComponents.ContainsKey(newEntity))
                        _managedComponents[newEntity] = new Dictionary<Type, IManagedComponent>();

                    _managedComponents[newEntity][kvp.Key] = kvp.Value;
                }
            }

            return newEntity;
        }

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
            var movedEntity = archetype.RemoveEntity(chunk, meta.IndexInChunk);

            // Swap된 Entity 메타데이터 업데이트
            if (movedEntity != Entity.Null)
            {
                var movedMeta = _entityMetadata[movedEntity.Index];
                movedMeta.IndexInChunk = meta.IndexInChunk;
                _entityMetadata[movedEntity.Index] = movedMeta;
            }

            // 현재 Entity 무효화
            _entityMetadata[entity.Index] = new EntityMetadata
            {
                Chunk = null,
                Version = meta.Version + 1
            };

            _freeIndices.Enqueue(entity.Index);
            _entityCount--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Exists(Entity entity)
        {
            if (entity.Index < 0 || entity.Index >= _entityMetadata.Count)
                return false;

            var meta = _entityMetadata[entity.Index];
            return meta.IsAlive && meta.Version == entity.Version;
        }

        #endregion

        #region Unmanaged Component Operations

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
            var newComponentTypes = new List<Type>(oldArchetype.ComponentTypes) { typeof(T) };
            var newArchetype = GetOrCreateArchetype(newComponentTypes.ToArray());

            MoveEntity(entity, newArchetype);

            if (component is INeedInit needInit)
            {
                needInit.Initialize();
                component = (T)(object)needInit;
            }

            SetComponent(entity, component);
        }

        public void RemoveComponent<T>(Entity entity) where T : struct, IComponentData
        {
            if (!Exists(entity))
                throw new ArgumentException("Entity does not exist");

            if (!HasComponent<T>(entity))
                return;

            var meta = _entityMetadata[entity.Index];
            var oldArchetype = meta.Chunk.Archetype;
            var newComponentTypes = new List<Type>(oldArchetype.ComponentTypes);
            newComponentTypes.Remove(typeof(T));
            var newArchetype = GetOrCreateArchetype(newComponentTypes.ToArray());

            MoveEntity(entity, newArchetype);
        }

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

        #endregion

        #region Managed Component Operations

        public void AddManagedComponent<T>(Entity entity, T component) where T : class, IManagedComponent
        {
            if (!Exists(entity))
                throw new ArgumentException($"Entity {entity} does not exist");

            if (!_managedComponents.TryGetValue(entity, out var components))
            {
                components = new Dictionary<Type, IManagedComponent>();
                _managedComponents[entity] = components;
            }

            var type = typeof(T);
            if (components.ContainsKey(type))
                throw new InvalidOperationException($"Entity {entity} already has {type.Name}");

            if (component is INeedInit needInit)
            {
                needInit.Initialize();
            }

            components[type] = component;
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

        public bool HasManagedComponent<T>(Entity entity) where T : class, IManagedComponent
        {
            if (!_managedComponents.TryGetValue(entity, out var components))
                return false;

            return components.ContainsKey(typeof(T));
        }

        public void RemoveManagedComponent<T>(Entity entity) where T : class, IManagedComponent
        {
            if (!_managedComponents.TryGetValue(entity, out var components))
                return;

            var type = typeof(T);
            if (components.TryGetValue(type, out var component))
            {
                if (component is IDisposable disposable)
                    disposable.Dispose();

                components.Remove(type);

                if (components.Count == 0)
                    _managedComponents.Remove(entity);
            }
        }

        #endregion

        #region Archetype Management

        private Archetype GetOrCreateArchetype(Type[] componentTypes)
        {
            foreach (var archetype in _archetypes)
            {
                if (archetype.Matches(componentTypes))
                    return archetype;
            }

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

        private void MoveEntity(Entity entity, Archetype newArchetype)
        {
            var oldMeta = _entityMetadata[entity.Index];
            var oldChunk = oldMeta.Chunk;
            var oldArchetype = oldChunk.Archetype;
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

            var movedEntity = oldArchetype.RemoveEntity(oldChunk, oldMeta.IndexInChunk);

            // Swap된 Entity 업데이트
            if (movedEntity != Entity.Null)
            {
                var movedMeta = _entityMetadata[movedEntity.Index];
                movedMeta.IndexInChunk = oldMeta.IndexInChunk;
                _entityMetadata[movedEntity.Index] = movedMeta;
            }

            _entityMetadata[entity.Index] = new EntityMetadata
            {
                Chunk = newChunk,
                IndexInChunk = newIndex,
                Version = oldMeta.Version
            };
        }

        #endregion

        #region Query Operations

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

        #endregion

        #region Debug & Utilities

        public void DebugPrintArchetypes()
        {
            UnityEngine.Debug.Log($"=== EntityManager: {_entityCount} entities, {_archetypes.Count} archetypes ===");
            foreach (var archetype in _archetypes)
            {
                var types = string.Join(", ", Array.ConvertAll(archetype.ComponentTypes.ToArray(), t => t.Name));
                UnityEngine.Debug.Log($"  Archetype [{types}]: {archetype.Chunks.Count} chunks");
            }
        }

        public (int, int) GetEntityLocation(Entity entity)
        {
            var meta = _entityMetadata[entity.Index];
            return (meta.Chunk.Index, meta.IndexInChunk);
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            UnityEngine.Debug.Log($"[EntityManager] Disposing... {_entityCount} entities");

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

            foreach (var kvp in _managedComponents.Values)
            {
                foreach (var component in kvp.Values)
                {
                    if (component is IDisposable disposable)
                        disposable.Dispose();
                }
            }
            _managedComponents.Clear();

            _archetypes.Clear();
            _entityMetadata.Clear();
            _freeIndices.Clear();
            _emptyArchetype = null;
            _entityCount = 0;

            UnityEngine.Debug.Log("[EntityManager] Disposed");
        }

        #endregion
    }
}