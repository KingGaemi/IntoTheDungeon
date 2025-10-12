using UnityEngine;
using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Unity;

namespace IntoTheDungeon.Unity.Bridge
{
    public sealed class ViewBridge : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] GameObject[] _prefabs;
        [SerializeField] bool _usePooling = true;
        [SerializeField] int _initialPoolSize = 32;
        [SerializeField] int _maxOpsPerFrame = 256;

        // World 참조 추가 ⭐
        IWorld _world;
        IEntityManager _em;

        public IWorld World => _world; // Public 노출 ⭐
        
        ViewOp[] _opBuffer;
        int _opCount;
        
        readonly Dictionary<Entity, GameObject> _entityToGO = new(256);
        readonly Dictionary<GameObject, Entity> _goToEntity = new(256); // 역조회 ⭐
        readonly Dictionary<string, IBehaviourFactory> _behaviourFactories = new();
        readonly Dictionary<int, Stack<GameObject>> _pools = new();
        
        Transform _poolRoot;

        #region Initialization

        public void Init(IWorld world)
        {
            _world = world;
            _em = world.EntityManager;
        }

        void Awake()
        {
            // EntityBehaviour 패턴: GetWorld 폴백 ⭐
            if (_world == null)
            {
                _world = this.GetWorld();
                if (_world != null)
                    _em = _world.EntityManager;
            }

            if (!IsInitialized())
            {
                Debug.LogError("[ViewBridge] Failed to initialize. Missing World reference.");
                enabled = false;
                return;
            }

            _opBuffer = new ViewOp[_maxOpsPerFrame];

            if (_usePooling)
            {
                _poolRoot = new GameObject("[ViewBridge Pool]").transform;
                _poolRoot.SetParent(transform);
                InitializePools();
            }

            RegisterDefaultFactories();
        }

        bool IsInitialized() => _world != null && _em != null; // ⭐

        #endregion

        #region Update Loop

        void Update()
        {
            if (!IsInitialized()) // 안전 가드 ⭐
            {
                Debug.LogWarning("[ViewBridge] Not initialized. Skipping frame.");
                return;
            }

            ProcessViewOps();
        }

        void ProcessViewOps()
        {
            _opCount = 0;
            _em.ViewOps.Drain(_opBuffer, ref _opCount);

            for (int i = 0; i < _opCount; i++)
            {
                ref readonly var op = ref _opBuffer[i];
                
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
        }

        #endregion

        #region Spawn/Despawn

        void HandleSpawn(Entity entity, in ViewSpawnSpec spec)
        {
            if (_entityToGO.ContainsKey(entity))
            {
                Debug.LogWarning($"[ViewBridge] Entity {entity.Index} already spawned.");
                return;
            }

            GameObject go = AcquireGameObject(spec.PrefabId);
            
            if (go == null)
            {
                Debug.LogError($"[ViewBridge] Failed to acquire GO for PrefabId {spec.PrefabId}.");
                go = CreateFallbackGameObject(entity);
            }

            go.name = $"Entity_{entity.Index}";
            go.SetActive(true);

            // 양방향 매핑 ⭐
            _entityToGO[entity] = go;
            _goToEntity[go] = entity;

            AttachBehaviours(go, entity, spec.Behaviours);
            ApplyRenderProperties(go, spec);
            
            // View 컴포넌트에 ViewBridge 주입 ⭐
            InjectViewComponents(go);
        }

        void HandleDespawn(Entity entity)
        {
            if (!_entityToGO.TryGetValue(entity, out GameObject go))
                return;

            _entityToGO.Remove(entity);
            _goToEntity.Remove(go); // 역매핑도 제거 ⭐
            
            ReleaseGameObject(go);
        }

        #endregion

        #region GameObject Lifecycle

        GameObject AcquireGameObject(int prefabId)
        {
            if (_usePooling && _pools.TryGetValue(prefabId, out var pool) && pool.Count > 0)
                return pool.Pop();

            return InstantiateFromPrefab(prefabId);
        }

        void ReleaseGameObject(GameObject go)
        {
            if (_usePooling && go.TryGetComponent<ViewMetadata>(out var meta))
            {
                go.SetActive(false);
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
                Debug.LogError($"[ViewBridge] Invalid PrefabId: {prefabId}");
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
            var go = new GameObject($"Fallback_Entity_{entity.Index}");
            go.transform.SetParent(transform);
            
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDebugSprite();
            sr.color = Color.magenta;
            
            return go;
        }

        Sprite CreateDebugSprite()
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
        }

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

            try
            {
                factory.Attach(go, entity, spec.Payload, _em);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ViewBridge] Factory failed for {spec.TypeName}: {ex.Message}");
                return false;
            }
        }

        void AttachViaReflection(GameObject go, string typeName)
        {
            var type = System.Type.GetType(typeName);
            
            if (type == null)
            {
                Debug.LogError($"[ViewBridge] Type not found: {typeName}");
                return;
            }

            if (!typeof(Component).IsAssignableFrom(type))
            {
                Debug.LogError($"[ViewBridge] Type {typeName} is not a Component");
                return;
            }

            go.AddComponent(type);
        }

        // ViewComponent에 ViewBridge 주입 ⭐
        void InjectViewComponents(GameObject go)
        {
            var viewComponents = go.GetComponentsInChildren<IViewComponent>(true);
            foreach (var vc in viewComponents)
            {
                vc.ViewBridge = this;
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
                Debug.LogError($"[ViewBridge] Cannot register null factory for {typeName}");
                return;
            }

            _behaviourFactories[typeName] = factory;
        }

        #endregion

        #region Public API

        public bool TryGetGameObject(Entity entity, out GameObject go)
            => _entityToGO.TryGetValue(entity, out go);

        // GO→Entity 역조회 ⭐
        public Entity GetEntity(GameObject go)
        {
            _goToEntity.TryGetValue(go, out var entity);
            return entity;
        }

        // 유효성 검사 헬퍼 ⭐
        public bool IsEntityValid(Entity entity)
            => !entity.Equals(default) && _em?.Exists(entity) == true;

        public int MappedEntityCount => _entityToGO.Count;

        #endregion

        #region Cleanup

        void OnDestroy()
        {
            foreach (var go in _entityToGO.Values)
            {
                if (go != null) Destroy(go);
            }
            
            _entityToGO.Clear();
            _goToEntity.Clear(); // ⭐

            foreach (var pool in _pools.Values)
            {
                while (pool.Count > 0)
                {
                    var go = pool.Pop();
                    if (go != null) Destroy(go);
                }
            }
            _pools.Clear();
        }

        #endregion
    }

    #region Supporting Types

    internal sealed class ViewMetadata : MonoBehaviour
    {
        public int PrefabId;
    }

    // ViewComponent 인터페이스 ⭐
    public interface IViewComponent
    {
        ViewBridge ViewBridge { get; set; }
    }

    public interface IBehaviourFactory
    {
        Component Attach(GameObject go, Entity entity, byte[] payload, IEntityManager em);
    }

    #endregion
}