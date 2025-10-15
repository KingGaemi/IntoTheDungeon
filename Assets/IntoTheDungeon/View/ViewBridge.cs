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
        T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

        Transform GetOrCreate(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (!t) { var go = new GameObject(name); go.transform.SetParent(parent, false); t = go.transform; }
            return t;
        }

        static int StableId(string s)
        {
            unchecked
            {
                const int p = 16777619; int h = (int)2166136261;
                for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * p;
                return h;
            }
        }

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
        readonly Dictionary<int, IBehaviourFactory> _behaviourFactories = new();
        readonly Dictionary<int, System.Type> _behaviourTypes = new();
        public void RegisterFactory(int typeId, IBehaviourFactory f) => _behaviourFactories[typeId] = f;
        public void RegisterType(int typeId, System.Type t) => _behaviourTypes[typeId] = t;


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

        int Id(object o) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        void Awake()
        {

            if (!IsInitialized())
            {
                Disable();
                return;
            }

            _world.TryGet(out _hub);
            if (_hub == null) { Disable(); return; }
            Debug.Log($"Hub(ViewBridge)={Id(_hub)}");


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
            // Debug.Log($"[VB] before consume count={_hub.Count<StateChangedEvent>()}");

            ProcessViewOps();
            BroadcastEvents();

#if UNITY_EDITOR
            UpdateStats();
#endif
        }

        void ProcessViewOps()
        {
            _opCount = 0;
            // 필요 시 루프 Drain
            int drained;
            do
            {
                EnsureBufferCapacity(_opCount + _maxOpsPerFrame);
                drained = _em.ViewOps.Drain(_opBuffer, ref _opCount); // Drain이 누적 작성하도록 구현되어야 안전
            } while (drained > 0 && _opCount < _config.MaxCapacity);

            CoalesceOps(_opBuffer, ref _opCount);
            int processed = Mathf.Min(_opCount, _maxOpsPerFrame);
            for (int i = 0; i < processed; i++) DispatchOp(in _opBuffer[i]);
#if UNITY_EDITOR
            _stats.ProcessedOps = processed;
            _stats.QueuedOps = _opCount - processed;

#endif
            ViewDataStores.ClearFrame();
            if (processed == _opCount)
                ViewDataStores.ClearFrame();
        }

        void DispatchOp(in ViewOp op)
        {
            switch (op.Kind)
            {
                case ViewOpKind.Spawn:
                    HandleSpawn(op.Entity, op.DataIndex);
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
            int write = 0;
#if UNITY_EDITOR
            int removed = 0;
#endif

            for (int i = 0; i < count; i++)
            {
                ref var op = ref buffer[i];
                if (op.Kind == ViewOpKind.None) continue;

                if (lastIndex.TryGetValue(op.Entity, out int prevIdx))
                {
                    ref var prev = ref buffer[prevIdx];

                    // Spawn→Despawn 상쇄
                    if (prev.Kind == ViewOpKind.Spawn && op.Kind == ViewOpKind.Despawn)
                    {
#if UNITY_EDITOR
                        removed += 2;
#endif
                        buffer[prevIdx] = default;
                        // 현재 것도 버림
                        continue;
                    }

                    // 동일 타입은 최신으로 교체
                    if (prev.Kind == op.Kind)
                    {
#if UNITY_EDITOR
                        removed++;
#endif
                        buffer[prevIdx] = default;
                    }
                }

                lastIndex[op.Entity] = i;
            }

            // 압축: default 제외만 앞으로 모음
            for (int i = 0; i < count; i++)
            {
                if (buffer[i].Kind == ViewOpKind.None) continue;
                if (i != write) buffer[write] = buffer[i];
                write++;
            }
#if UNITY_EDITOR
            _stats.CoalescedOps = removed;
#endif
            count = write;
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
            _hub.Clear<StateChangedEvent>();
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
            _hub.Clear<StatusChangedEvent>();
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
            _hub.Clear<AnimationPhaseChangedEvent>();
        }

        #endregion

        #region Spawn/Despawn

        void HandleSpawn(Entity entity, int idx)
        {
            if (_entityToGO.ContainsKey(entity))
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[ViewBridge] Entity {entity.Index} already spawned.");
#endif
                return;
            }

            SpawnData data = ViewDataStores.Spawn[idx];

            GameObject root = AcquireGameObject(data.PrefabId);

            if (root == null)
            {
#if UNITY_EDITOR
                Debug.LogError($"[ViewBridge] Failed to acquire GO for PrefabId {data.PrefabId}.");
#endif
                root = CreateFallbackGameObject(entity);
            }

            root.name = $"Entity_{entity.Index}";
            root.SetActive(true);

            // 1) 루트 보장
            var rootBehaviour = Ensure<EntityRootBehaviour>(root);
            rootBehaviour.Entity = entity;

            // 2) 컨테이너 보장
            Transform scripts = GetOrCreate(root.transform, "Scripts");
            Transform visual = GetOrCreate(root.transform, "VisualContainer");
            Ensure<ScriptContainer>(scripts.gameObject);
            var visualScript = Ensure<VisualContainer>(visual.gameObject);



            visualScript.SetName("DefaultCurrently");
            // 3) 물리(필요 시만)
            Ensure<Rigidbody2D>(root); // rb는 루트에만

            // 4) 비주얼 서브트리 보장
            Transform spriteRoot = GetOrCreate(visual.transform, "SpriteRoot");
            Transform shadow = GetOrCreate(visual.transform, "Shadow");
            Transform effects = GetOrCreate(visual.transform, "Effects");

            // 스프라이트 관련 필수 컴포넌트
            Ensure<SpriteRenderer>(spriteRoot.gameObject);
            Ensure<Animator>(spriteRoot.gameObject);
            Ensure<UnityEngine.U2D.Animation.SpriteResolver>(spriteRoot.gameObject);
            Ensure<UnityEngine.U2D.Animation.SpriteLibrary>(spriteRoot.gameObject);


            AttachBehaviours(root, entity, data.Behaviours);

            ApplyRenderProperties(root, data);

            InjectViewComponents(root, entity, null);

            CacheListeners(entity, root);

            _entityToGO[entity] = root;
            _goToEntity[root] = entity;


#if UNITY_EDITOR
            Spawned?.Invoke(entity, root);
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

        void AttachBehaviours(GameObject go, Entity e, BehaviourSpec[] specs)
        {
            if (specs == null || specs.Length == 0) return;

            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                if (_behaviourFactories.TryGetValue(spec.TypeId, out var fac))
                {
                    fac.Attach(go, e, spec.Payload, _em);
                    continue;
                }
                if (_behaviourTypes.TryGetValue(spec.TypeId, out var t) && typeof(Component).IsAssignableFrom(t))
                {
                    go.AddComponent(t);
                    continue;
                }
#if UNITY_EDITOR
                Debug.LogError($"[ViewBridge] Behaviour TypeId {spec.TypeId} not registered.");
#endif
            }
        }

        bool AttachViaFactory(GameObject go, Entity entity, in BehaviourSpec spec)
        {
            if (!_behaviourFactories.TryGetValue(spec.TypeId, out var factory))
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
                Debug.LogError($"[ViewBridge] Factory failed for {spec.TypeId}: {ex.Message}");
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

        void ApplyRenderProperties(GameObject go, in SpawnData data)
        {
            if (!go.TryGetComponent<Renderer>(out var renderer))
                return;

            if (data.SortingLayerId != 0)
                renderer.sortingLayerID = data.SortingLayerId;

            renderer.sortingOrder = data.OrderInLayer;
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
            RegisterFactory(StableId("IntoTheDungeon.View.ProjectileView"), new ProjectileViewFactory());
            RegisterType(StableId("IntoTheDungeon.Features.Character.CharacterAnimator"),
                         typeof(IntoTheDungeon.Features.Character.CharacterAnimator));
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
            // Debug.Log($"BindExisting e={entity.Index} go={go.name}");
            InjectViewComponents(go, entity, payload);
            CacheListeners(entity, go);
            // Debug.Log($"Cached {_stateListenerCache[entity].Length} listeners e={entity.Index}");
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