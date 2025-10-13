using UnityEngine;
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Unity.World;
using IntoTheDungeon.Unity.Factories;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Messages.Animation;
using IntoTheDungeon.Features.View;

namespace IntoTheDungeon.Unity.Bridge
{
    [DefaultExecutionOrder(9000)]
    public sealed class ViewBridge : MonoBehaviour, IWorldInjectable
    {
        #region Configuration

        [SerializeField] GameObject[] _prefabs;
        [SerializeField] bool _usePooling = true;
        [SerializeField] int _initialPoolSize = 32;
        [SerializeField] int _maxOpsPerFrame = 256;
        [SerializeField] int _maxBufferCapacity = 1024;

        #endregion

        #region Core References

        IWorld _world;
        IEntityManager _em;
        IEventHub _hub;

        public IWorld World => _world;

        #endregion

        #region Op Processing

        struct OpBufferConfig
        {
            public int InitialCapacity;
            public int MaxCapacity;
            public float GrowthFactor;
        }

        ViewOp[] _opBuffer;
        int _opCount;
        OpBufferConfig _config;

        #endregion

        #region Mappings

        readonly Dictionary<Entity, GameObject> _entityToGO = new(256);
        readonly Dictionary<GameObject, Entity> _goToEntity = new(256);

        readonly Dictionary<Entity, IStateEventListener[]> _stateListenerCache = new();
        readonly Dictionary<Entity, IStatusEventListener[]> _statusListenerCache = new();
        readonly Dictionary<Entity, IAnimationEventListener[]> _animListenerCache = new();

        #endregion

        #region Pooling

        readonly Dictionary<int, Stack<GameObject>> _pools = new();
        readonly Dictionary<string, IBehaviourFactory> _behaviourFactories = new();
        Transform _poolRoot;

        #endregion

        #region Editor Diagnostics

#if UNITY_EDITOR
        struct ViewBridgeStats
        {
            public int ProcessedOps;
            public int CoalescedOps;
            public int QueuedOps;
            public int ActiveMappings;
            public int PooledObjects;
        }

        ViewBridgeStats _stats;
        bool _showDebug;

        public static event System.Action<ViewBridge> AvailabilityChanged;
        public event System.Action<Entity, GameObject> Spawned;
        public event System.Action<Entity, GameObject> Despawned;
#endif

        #endregion

        #region Initialization

        public void Init(IWorld world)
        {
            _world = world;
            _em = world.EntityManager;
        }

        void Awake()
        {
            ResolveWorldReference();
            if (!IsInitialized())
            {
                Disable();
                return;
            }

            _world.TryGet(out _hub);

            _config = new OpBufferConfig
            {
                InitialCapacity = _maxOpsPerFrame,
                MaxCapacity = _maxBufferCapacity,
                GrowthFactor = 2f
            };

            _opBuffer = new ViewOp[_config.InitialCapacity];

            if (_usePooling)
            {
                _poolRoot = new GameObject("[ViewBridge Pool]").transform;
                _poolRoot.SetParent(transform);
                InitializePools();
            }

            RegisterDefaultFactories();

#if UNITY_EDITOR
            AvailabilityChanged?.Invoke(this);
#endif
        }

        void ResolveWorldReference()
        {
            if (_world == null)
                _world = this.GetWorld();
            if (_world != null)
                _em = _world.EntityManager;
        }

        void Disable()
        {
#if UNITY_EDITOR
            Debug.LogError("[ViewBridge] Initialization failed. Disabling.");
#endif
            enabled = false;
        }

        bool IsInitialized() => _world != null && _em != null;

        #endregion

        #region Update Loop

        void Update()
        {
            if (!IsInitialized())
            {
#if UNITY_EDITOR
                Debug.LogWarning("[ViewBridge] Not initialized. Skipping frame.");
#endif
                return;
            }

            ProcessViewOps();
            BroadcastEvents();

#if UNITY_EDITOR
            UpdateStats();
#endif
        }

        void ProcessViewOps()
        {
            int available = _maxOpsPerFrame;
            _opCount = 0;

            _em.ViewOps.Drain(_opBuffer, ref _opCount);

            EnsureBufferCapacity(_opCount);
            CoalesceOps(_opBuffer, ref _opCount);

            int processed = Mathf.Min(_opCount, available);

            for (int i = 0; i < processed; i++)
            {
                ref readonly var op = ref _opBuffer[i];
                DispatchOp(in op);
            }

#if UNITY_EDITOR
            _stats.ProcessedOps = processed;
            _stats.QueuedOps = _opCount - processed;
#endif
        }

        void DispatchOp(in ViewOp op)
        {
            switch (op.Kind)
            {
                case ViewOpKind.Spawn:
                    HandleSpawn(op.Entity, in op.Spawn);
                    break;
                case ViewOpKind.Despawn:
                    HandleDespawn(op.Entity);
                    break;
            }
        }

        void EnsureBufferCapacity(int required)
        {
            if (required <= _opBuffer.Length) return;

            int newSize = Mathf.Min(
                Mathf.NextPowerOfTwo(required),
                _config.MaxCapacity
            );

            System.Array.Resize(ref _opBuffer, newSize);

#if UNITY_EDITOR
            Debug.Log($"[ViewBridge] Buffer resized: {_opBuffer.Length}");
#endif
        }

        void CoalesceOps(ViewOp[] buffer, ref int count)
        {
            if (count <= 1) return;

            var lastIndex = new Dictionary<Entity, int>(count);
            int writeIdx = 0;

#if UNITY_EDITOR
            int removedCount = 0;
#endif

            for (int i = 0; i < count; i++)
            {
                ref var op = ref buffer[i];

                if (lastIndex.TryGetValue(op.Entity, out int prevIdx))
                {
                    ref var prevOp = ref buffer[prevIdx];

                    // Spawn → Despawn 상쇄
                    if (prevOp.Kind == ViewOpKind.Spawn && op.Kind == ViewOpKind.Despawn)
                    {
                        prevOp = default;
                        op = default;
                        lastIndex.Remove(op.Entity);
#if UNITY_EDITOR
                        removedCount += 2;
#endif
                        continue;
                    }

                    // 동일 타입 덮어쓰기
                    if (prevOp.Kind == op.Kind)
                    {
                        prevOp = default;
#if UNITY_EDITOR
                        removedCount++;
#endif
                    }
                }

                lastIndex[op.Entity] = i;
            }

            // Compaction
            for (int i = 0; i < count; i++)
            {
                if (i != writeIdx) buffer[writeIdx] = buffer[i];
                writeIdx++;
            }

#if UNITY_EDITOR
            _stats.CoalescedOps = removedCount;
#endif

            count = writeIdx;
        }

        #endregion

        #region Event Broadcasting

        void BroadcastEvents()
        {
            BroadcastStateEvents();
            BroadcastStatusEvents();
            BroadcastAnimationEvents();
        }

        void BroadcastStateEvents()
        {
            var stateEvents = _hub.Consume<StateChangedEvent>();

            for (int i = 0; i < stateEvents.Length; i++)
            {
                ref readonly var evt = ref stateEvents[i];

                if (!_stateListenerCache.TryGetValue(evt.Entity, out var listeners))
                    continue;

                for (int j = 0; j < listeners.Length; j++)
                {
#if UNITY_EDITOR
                    try
                    {
#endif
                        listeners[j].OnStateChanged(evt);
#if UNITY_EDITOR
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[ViewBridge] StateListener exception: {ex}");
                    }
#endif
                }
            }
        }

        void BroadcastStatusEvents()
        {
            var statusEvents = _hub.Consume<StatusChangedEvent>();

            for (int i = 0; i < statusEvents.Length; i++)
            {
                ref readonly var evt = ref statusEvents[i];

                if (!_statusListenerCache.TryGetValue(evt.E, out var listeners))
                    continue;

                for (int j = 0; j < listeners.Length; j++)
                {
#if UNITY_EDITOR
                    try
                    {
#endif
                        listeners[j].OnStatusChanged(evt);
#if UNITY_EDITOR
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[ViewBridge] StatusListener exception: {ex}");
                    }
#endif
                }
            }
        }

        void BroadcastAnimationEvents()
        {
            var animEvents = _hub.Consume<AnimationPhaseChangedEvent>();

            for (int i = 0; i < animEvents.Length; i++)
            {
                ref readonly var evt = ref animEvents[i];

                if (!_animListenerCache.TryGetValue(evt.Entity, out var listeners))
                    continue;

                for (int j = 0; j < listeners.Length; j++)
                {
#if UNITY_EDITOR
                    try
                    {
#endif
                        listeners[j].OnAnimationPhaseChanged(evt);
#if UNITY_EDITOR
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[ViewBridge] AnimListener exception: {ex}");
                    }
#endif
                }
            }
        }

        #endregion

        #region Spawn/Despawn

        void HandleSpawn(Entity entity, in ViewSpawnSpec spec)
        {
            if (_entityToGO.ContainsKey(entity))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[ViewBridge] Entity {entity.Index} already spawned.");
#endif
                return;
            }

            GameObject go = AcquireGameObject(spec.PrefabId);

            if (go == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[ViewBridge] Failed to acquire GO for PrefabId {spec.PrefabId}.");
#endif
                go = CreateFallbackGameObject(entity);
            }

            go.name = $"Entity_{entity.Index}";
            go.SetActive(true);

            _entityToGO[entity] = go;
            _goToEntity[go] = entity;

            AttachBehaviours(go, entity, spec.Behaviours);
            ApplyRenderProperties(go, spec);

            InjectViewComponents(go, entity, null);

            CacheListeners(entity, go);


#if UNITY_EDITOR
            Spawned?.Invoke(entity, go);
#endif
        }

        void HandleDespawn(Entity entity)
        {
            if (!_entityToGO.TryGetValue(entity, out GameObject go))
                return;

            _stateListenerCache.Remove(entity);
            _statusListenerCache.Remove(entity);
            _animListenerCache.Remove(entity);

            _entityToGO.Remove(entity);
            _goToEntity.Remove(go);

            ReleaseGameObject(go);

#if UNITY_EDITOR
            Despawned?.Invoke(entity, go);
#endif
        }

        void CacheListeners(Entity entity, GameObject go)
        {
            _stateListenerCache[entity] = go.GetComponentsInChildren<IStateEventListener>(false);
            _statusListenerCache[entity] = go.GetComponentsInChildren<IStatusEventListener>(false);
            _animListenerCache[entity] = go.GetComponentsInChildren<IAnimationEventListener>(false);

#if UNITY_EDITOR
            Debug.Log($"[ViewBridge] Cached {_stateListenerCache[entity].Length} state listeners for Entity {entity.Index}");
#endif
        }

        #endregion

        #region GameObject Lifecycle

        GameObject AcquireGameObject(int prefabId)
        {
            GameObject go;

            if (_usePooling && _pools.TryGetValue(prefabId, out var pool) && pool.Count > 0)
            {
                go = pool.Pop();

                var poolables = go.GetComponentsInChildren<IPoolable>(true);
                for (int i = 0; i < poolables.Length; i++)
                    poolables[i].OnAcquire();
            }
            else
            {
                go = InstantiateFromPrefab(prefabId);
            }

            return go;
        }

        void ReleaseGameObject(GameObject go)
        {
            var poolables = go.GetComponentsInChildren<IPoolable>(true);
            for (int i = 0; i < poolables.Length; i++)
                poolables[i].OnRelease();

            go.SetActive(false);

            if (_usePooling && go.TryGetComponent<ViewMetadata>(out var meta))
            {
                go.transform.SetParent(_poolRoot);

                if (!_pools.ContainsKey(meta.PrefabId))
                    _pools[meta.PrefabId] = new Stack<GameObject>();

                _pools[meta.PrefabId].Push(go);
            }
            else
            {
                Destroy(go);
            }
        }

        GameObject InstantiateFromPrefab(int prefabId)
        {
            if (prefabId < 0 || prefabId >= _prefabs.Length || _prefabs[prefabId] == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[ViewBridge] Invalid PrefabId: {prefabId}");
#endif
                return null;
            }

            GameObject go = Instantiate(_prefabs[prefabId], transform);

            if (_usePooling)
            {
                var meta = go.AddComponent<ViewMetadata>();
                meta.PrefabId = prefabId;
            }

            return go;
        }

        GameObject CreateFallbackGameObject(Entity entity)
        {
#if UNITY_EDITOR
            var go = new GameObject($"Fallback_Entity_{entity.Index}");
            go.transform.SetParent(transform);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDebugSprite();
            sr.color = Color.magenta;

            return go;
#else
            return new GameObject($"Entity_{entity.Index}");
#endif
        }

#if UNITY_EDITOR
        Sprite CreateDebugSprite()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        }
#endif

        #endregion

        #region Behaviour Attachment

        void AttachBehaviours(GameObject go, Entity entity, BehaviourSpec[] specs)
        {
            if (specs == null || specs.Length == 0) return;

            for (int i = 0; i < specs.Length; i++)
            {
                ref readonly var spec = ref specs[i];

                if (string.IsNullOrEmpty(spec.TypeName))
                    continue;

                if (!AttachViaFactory(go, entity, in spec))
                    AttachViaReflection(go, spec.TypeName);
            }
        }

        bool AttachViaFactory(GameObject go, Entity entity, in BehaviourSpec spec)
        {
            if (!_behaviourFactories.TryGetValue(spec.TypeName, out var factory))
                return false;

#if UNITY_EDITOR
            try
            {
#endif
                factory.Attach(go, entity, spec.Payload, _em);
                return true;
#if UNITY_EDITOR
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ViewBridge] Factory failed for {spec.TypeName}: {ex.Message}");
                return false;
            }
#endif
        }

        void AttachViaReflection(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName);

            if (type == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[ViewBridge] Type not found: {typeName}");
#endif
                return;
            }

            if (!typeof(Component).IsAssignableFrom(type))
            {
#if UNITY_EDITOR
                Debug.LogError($"[ViewBridge] Type {typeName} is not a Component");
#endif
                return;
            }

            go.AddComponent(type);
        }

        void InjectViewComponents(GameObject go, Entity entity, byte[] payload)
        {
            var viewComponents = go.GetComponentsInChildren<IViewComponent>(true);
            for (int i = 0; i < viewComponents.Length; i++)
            {
                viewComponents[i].ViewBridge = this;
                viewComponents[i].Initialize(entity, _em, payload);
            }
        }

        #endregion

        #region Render Properties

        void ApplyRenderProperties(GameObject go, in ViewSpawnSpec spec)
        {
            if (!go.TryGetComponent<Renderer>(out var renderer))
                return;

            if (spec.SortingLayerId != 0)
                renderer.sortingLayerID = spec.SortingLayerId;

            renderer.sortingOrder = spec.OrderInLayer;
        }

        #endregion

        #region Pooling

        void InitializePools()
        {
            for (int i = 0; i < _prefabs.Length; i++)
            {
                if (_prefabs[i] == null) continue;

                var stack = new Stack<GameObject>(_initialPoolSize);

                for (int j = 0; j < _initialPoolSize; j++)
                {
                    var go = InstantiateFromPrefab(i);
                    if (go != null)
                    {
                        go.SetActive(false);
                        go.transform.SetParent(_poolRoot);
                        stack.Push(go);
                    }
                }

                _pools[i] = stack;
            }
        }

        #endregion

        #region Factory Registration

        void RegisterDefaultFactories()
        {
            RegisterFactory("IntoTheDungeon.View.ProjectileView", new ProjectileViewFactory());
            RegisterFactory("IntoTheDungeon.View.CharacterView", new CharacterViewFactory());
        }

        public void RegisterFactory(string typeName, IBehaviourFactory factory)
        {
            if (factory == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[ViewBridge] Cannot register null factory for {typeName}");
#endif
                return;
            }

            _behaviourFactories[typeName] = factory;
        }

        #endregion

        #region Public API

        public bool TryGetGameObject(Entity entity, out GameObject go)
            => _entityToGO.TryGetValue(entity, out go);

        public Entity GetEntity(GameObject go)
        {
            _goToEntity.TryGetValue(go, out var entity);
            return entity;
        }

        public bool IsEntityValid(Entity entity)
            => !entity.Equals(default) && _em?.Exists(entity) == true;

        public int MappedEntityCount => _entityToGO.Count;

        public void BindExisting(GameObject go, Entity entity, byte[] payload = null)
        {
            _entityToGO[entity] = go;
            _goToEntity[go] = entity;
            _em.AddComponent(entity, new ViewSpawnedTag());
            InjectViewComponents(go, entity, payload);
            CacheListeners(entity, go);
        }

        #endregion

        #region Diagnostics

#if UNITY_EDITOR
        void UpdateStats()
        {
            _stats.ActiveMappings = _entityToGO.Count;
            _stats.PooledObjects = 0;
            foreach (var pool in _pools.Values)
                _stats.PooledObjects += pool.Count;
        }

        void ValidateInvariants()
        {
            foreach (var kvp in _entityToGO)
            {
                if (!_goToEntity.TryGetValue(kvp.Value, out var e) || !e.Equals(kvp.Key))
                    Debug.LogError($"[ViewBridge] Mapping mismatch: {kvp.Key}");
            }

            foreach (var entity in _stateListenerCache.Keys)
            {
                if (!_entityToGO.ContainsKey(entity))
                    Debug.LogError($"[ViewBridge] Orphaned cache: {entity}");
            }
        }

        void OnGUI()
        {
            if (!_showDebug) return;

            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label($"Processed: {_stats.ProcessedOps}/{_maxOpsPerFrame}");
            GUILayout.Label($"Coalesced: {_stats.CoalescedOps}");
            GUILayout.Label($"Queued: {_stats.QueuedOps}");
            GUILayout.Label($"Active: {_stats.ActiveMappings}");
            GUILayout.Label($"Pooled: {_stats.PooledObjects}");
            GUILayout.EndArea();
        }
#endif

        #endregion

        #region Cleanup

        void OnDestroy()
        {
            foreach (var go in _entityToGO.Values)
            {
                if (go != null) Destroy(go);
            }

            _entityToGO.Clear();
            _goToEntity.Clear();

            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    var go = pool.Pop();
                    if (go != null) Destroy(go);
                }
            }
            _pools.Clear();

#if UNITY_EDITOR
            AvailabilityChanged?.Invoke(null);
#endif
        }

        #endregion
    }

    #region Supporting Types

    internal sealed class ViewMetadata : MonoBehaviour
    {
        public int PrefabId;
    }

    public interface IViewComponent
    {
        ViewBridge ViewBridge { get; set; }
        void Initialize(Entity entity, IEntityManager em, byte[] payload);
    }

    public interface IPoolable
    {
        void OnAcquire();
        void OnRelease();
    }

    public interface IBehaviourFactory
    {
        Component Attach(GameObject go, Entity entity, byte[] payload, IEntityManager em);
    }

    #endregion
}