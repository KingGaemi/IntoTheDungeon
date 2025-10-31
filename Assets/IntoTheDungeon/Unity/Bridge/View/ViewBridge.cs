using UnityEngine;
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Combat;
using IntoTheDungeon.Core.Abstractions.Messages.Animation;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Unity.Bridge.Physics.Abstractions;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;
using IntoTheDungeon.Unity.Bridge.Core.Abstractions;
using System.Linq;
using System.Linq.Expressions;


namespace IntoTheDungeon.Unity.Bridge.View
{
    struct State
    {
        public Vector2 prev, curr;
        public float rotPrev, rotCurr;
        public int seq;          // 뷰데이터 시퀀스나 worldTick
        public float tStart;     // 이 변화가 시작된 시간
    }
    [DefaultExecutionOrder(8000)]
    public sealed class ViewBridge : MonoBehaviour, IWorldInjectable, IViewPort, IViewBridge
    {
        #region Config
        [SerializeField] int _maxOpsPerFrame = 256;
        [SerializeField] int _maxBufferCapacity = 1024;
        #endregion

        #region Services
        IEventHub _hub;
        IViewRecipeRegistry _viewRecipeRegistry;
        IViewOpQueue _viewOpQueue;
        IEntityViewMapRegistry _entityViewMapRegistry;
        ISceneViewRegistry _sceneViewRegistry;
        IPhysicsPort _physPort;




        #endregion

        #region State
        ViewOp[] _opBuffer;
        int _opCount;

        readonly Dictionary<Entity, GameObject> _entityToGO = new(256);
        readonly Dictionary<GameObject, Entity> _goToEntity = new(256);
        readonly Dictionary<Entity, PhysicsHandle> _pending = new();
        readonly Dictionary<Entity, IStateEventListener[]> _stateListeners = new();
        readonly Dictionary<Entity, IStatusEventListener[]> _statusListeners = new();
        readonly Dictionary<Entity, IAnimationEventListener[]> _animListeners = new();
        readonly Dictionary<Entity, Transform> _trCache = new();
        readonly Dictionary<Entity, State> _state = new();

        #endregion

        float _alpha;

        #region Init
        public void Init(IWorld world)
        {
            if (!world.TryGet(out _hub))
            {
                Debug.Log("[ViewBridge] no _hub");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _viewOpQueue))
            {
                Debug.Log("[ViewBridge] no _viewOpQueue");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _viewRecipeRegistry))
            {
                Debug.Log("[ViewBridge] no _viewRecipeRegistry");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _entityViewMapRegistry))
            {
                Debug.Log("[ViewBridge] no _entityViewRegistry");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _sceneViewRegistry))
            {
                Debug.Log("[ViewBridge] no _sceneViewRegistry");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _physPort))
            {
                Debug.Log("[ViewBridge] no _physPort");
                enabled = false;
                return;
            }
        }

        void Awake()
        {
            _opBuffer = new ViewOp[_maxOpsPerFrame];
        }
        #endregion

        #region Update
        void Update()
        {
            _alpha = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
            ProcessOps();
            BroadcastEvents();
            if (_pending.Count == 0) return;
            foreach (var (e, h) in _pending.ToArray())
                if (_physPort.TryGetRigidbody(h, out _)) { _physPort.AdoptBody(h, _entityToGO[e].transform); _pending.Remove(e); }
        }


        void LateUpdate()
        {

        }
        // {
        // float now = Time.time;

        // 임계값
        // const float posEps = 0.0005f;
        // const float rotEps = 0.05f;

        // foreach (var kv in _state)
        // {
        //     var e = kv.Key;
        //     var s = kv.Value;
        //     if (!_trCache.TryGetValue(e, out var t)) continue;

        //     // 물리틱 기반 알파. 알파 힌트를 이미 계산해 둔 경우 그 값을 사용.
        //     float elapsed = now - s.tStart;
        //     float alpha = physicsDt > 0f ? Mathf.Clamp01(elapsed / physicsDt) : 1f;

        //     // 위치 보간
        //     Vector2 pos = Vector2.Lerp(s.prev, s.curr, alpha);
        //     // 회전 보간(Shortest path)
        //     float rot = Mathf.LerpAngle(s.rotPrev, s.rotCurr, alpha);

        //     // 변동이 충분할 때만 적용해 트랜스폼 더티 최소화
        //     Vector3 curPos3 = t.position;
        //     float curRot = t.eulerAngles.z;

        //     bool posChanged = (new Vector2(curPos3.x, curPos3.y) - pos).sqrMagnitude > posEps * posEps;
        //     bool rotChanged = Mathf.Abs(Mathf.DeltaAngle(curRot, rot)) > rotEps;

        //     if (posChanged || rotChanged)
        //     {
        //         t.SetPositionAndRotation(new Vector3(pos.x, pos.y, curPos3.z), Quaternion.Euler(0, 0, rot));
        //     }
        // }
        // }

        void ProcessOps()
        {
            _opCount = 0;
            EnsureCapacity(_opCount + _maxOpsPerFrame);

            // int drainCount = 0;
            while (_viewOpQueue.Drain(_opBuffer, ref _opCount) > 0 && _opCount < _maxBufferCapacity)
            {
                // Debug.Log($"[ViewBridge] Drain #{drainCount++}: _opCount={_opCount}");
                EnsureCapacity(_opCount + _maxOpsPerFrame);
            }

            // Debug.Log($"[ViewBridge] Before Coalesce: _opCount={_opCount}");
            Coalesce();
            // Debug.Log($"[ViewBridge] After Coalesce: _opCount={_opCount}");

            int count = Mathf.Min(_opCount, _maxOpsPerFrame);
            // Debug.Log($"[ViewBridge] Dispatching {count} ops");

            for (int i = 0; i < count; i++)
            {
                // Debug.Log($"[ViewBridge] Dispatch[{i}]: {_opBuffer[i].Kind} for Entity {_opBuffer[i].Entity.Index}");
                Dispatch(_opBuffer[i]);
            }

            ViewDataStores.ClearFrame();
        }

        void Dispatch(ViewOp op)
        {


            switch (op.Kind)
            {
                case ViewOpKind.Spawn: Spawn(op.Entity, op.DataIndex); break;
                case ViewOpKind.Despawn: Despawn(op.Entity); break;
                case ViewOpKind.SetTransform: SetTransform(op.Entity, op.DataIndex); break;
            }
        }

        void EnsureCapacity(int required)
        {
            if (required <= _opBuffer.Length) return;
            System.Array.Resize(ref _opBuffer, Mathf.Min(Mathf.NextPowerOfTwo(required), _maxBufferCapacity));
        }

        void Coalesce()
        {
            if (_opCount <= 1) return;

            var last = new Dictionary<Entity, int>(_opCount);
            int write = 0;

            for (int i = 0; i < _opCount; i++)
            {
                ref var op = ref _opBuffer[i];
                if (op.Kind == ViewOpKind.None) continue;

                if (last.TryGetValue(op.Entity, out int prev))
                {
                    ref var prevOp = ref _opBuffer[prev];

                    // Spawn→Despawn 상쇄
                    if (prevOp.Kind == ViewOpKind.Spawn && op.Kind == ViewOpKind.Despawn)
                    {
                        _opBuffer[prev] = default;
                        continue;
                    }

                    // 같은 타입은 최신으로
                    if (prevOp.Kind == op.Kind)
                        _opBuffer[prev] = default;
                }

                last[op.Entity] = i;
            }

            // 압축
            for (int i = 0; i < _opCount; i++)
            {
                if (_opBuffer[i].Kind == ViewOpKind.None) continue;
                if (i != write) _opBuffer[write] = _opBuffer[i];
                write++;
            }

            _opCount = write;
        }
        #endregion

        #region Spawn/Despawn
        void Spawn(Entity entity, int idx)
        {
            // Debug.Log("[ViewBridge] View Spawn");
            if (_entityToGO.ContainsKey(entity)) return;

            var data = ViewDataStores.Spawn[idx];

            _entityViewMapRegistry.TryGetView(data.RecipeId, out var viewId);

            if (!_viewRecipeRegistry.TryGetRecipe(viewId, out var viewRecipe))
            {
                Debug.LogError($"[ViewBridge] No viewRecipe: {viewId}");
                return;
            }

            GameObject go;


            if (data.SceneLinkId != 0 && _sceneViewRegistry.TryTake(data.SceneLinkId, out var existing))
            {
                go = existing; // 기존 씬 GO 재사용
                Debug.Log($"[ViewBridge] Reused existing {data.SceneLinkId}");
            }
            else
            {
                go = viewRecipe.Prefab ? Instantiate(viewRecipe.Prefab) : new GameObject($"Entity_{entity.Index}");

            }

            go.name = $"Entity_{entity.Index}";
            go.SetActive(true);
            go.transform.SetParent(gameObject.transform);
            go.transform.position = gameObject.transform.position;

            var root = Ensure<EntityRootBehaviour>(go);
            root.Entity = entity;

            // Debug.Log($"PhysicsHandle = {data.PhysicsHandle}");
            if (data.PhysicsHandle.IsValid)
            {
                TryAdopt(entity, data.PhysicsHandle, go.transform);
            }
            else
            {

            }

            AttachBehaviours(root, viewRecipe);
            ApplyRender(go, data);
            CacheListeners(entity, go);

            _entityToGO[entity] = go;
            _goToEntity[go] = entity;



        }



        void Despawn(Entity entity)
        {
            if (!_entityToGO.TryGetValue(entity, out var go)) return;

            _stateListeners.Remove(entity);
            _statusListeners.Remove(entity);
            _animListeners.Remove(entity);
            _entityToGO.Remove(entity);
            _goToEntity.Remove(go);

            Destroy(go);
        }
        void RegisterView(Entity e, Transform visualTransform)
        {
            _trCache[e] = visualTransform;
            _state[e] = new State
            {
                prev = (Vector2)visualTransform.position,
                curr = (Vector2)visualTransform.position,
                rotPrev = visualTransform.eulerAngles.z,
                rotCurr = visualTransform.eulerAngles.z,
                seq = -1,
                tStart = Time.time
            };
        }

        // 매 프레임 또는 ViewOp 처리 시 호출
        void SetTransform(Entity entity, int idx)
        {

            ref var d = ref ViewDataStores.transformData[idx];
            // d에는 X,Y,RotDeg 외에 가능하면 d.Seq(증분 카운터)를 넣어라. 없으면 월드 tick으로 대체.

            if (!_entityToGO.TryGetValue(entity, out var go)) return;

            var visual = go.GetComponentInChildren<VisualContainer>();


            var t = visual.gameObject.transform;


            t.SetPositionAndRotation(new Vector3(d.X, d.Y, t.position.z), Quaternion.Euler(0, 0, d.RotDeg));


        }
        #endregion

        #region Behaviours
        void AttachBehaviours(EntityRootBehaviour root, IViewRecipe recipe)
        {

            var scripts = recipe.GetScriptContainerBehaviours();
            var visual = recipe.GetVisualContainerBehaviours();
            foreach (var type in scripts)
            {
                if (type == null || !typeof(MonoBehaviour).IsAssignableFrom(type)) continue;
                if (root.Script.GetComponent(type)) continue;

                root.Script.gameObject.AddComponent(type);
            }
            foreach (var type in visual)
            {
                var spriteRoot = root.Visual.gameObject.transform.Find("SpriteRoot");
                if (type == null || !typeof(Component).IsAssignableFrom(type)) continue;
                if (spriteRoot.GetComponent(type)) continue;
                spriteRoot.gameObject.AddComponent(type);
            }
        }

        void ApplyRender(GameObject go, ViewSpawnData data)
        {
            if (!go.TryGetComponent<Renderer>(out var r)) return;
            if (data.SortingLayerId != 0) r.sortingLayerID = data.SortingLayerId;
            r.sortingOrder = data.OrderInLayer;
        }

        void CacheListeners(Entity entity, GameObject go)
        {
            _stateListeners[entity] = go.GetComponentsInChildren<IStateEventListener>();
            _statusListeners[entity] = go.GetComponentsInChildren<IStatusEventListener>();
            _animListeners[entity] = go.GetComponentsInChildren<IAnimationEventListener>();


        }
        void TryAdopt(Entity e, PhysicsHandle h, Transform t)
        {
            if (_physPort.TryGetRigidbody(h, out var _)) { _physPort.AdoptBody(h, t); _pending.Remove(e); }
            else _pending[e] = h; // 아직 미생성 → 대기열
        }
        #endregion

        #region Events
        void BroadcastEvents()
        {
            BroadcastStateEvents();
            BroadcastStatusEvents();
            BroadcastAnimationEvents();
        }
        void BroadcastStateEvents()
        {
            var events = _hub.Consume<StateChangedEvent>();  // 1. 이벤트 가져오기

            foreach (ref readonly var evt in events)         // 2. 직접 디스패치
            {
                if (_stateListeners.TryGetValue(evt.Entity, out var listeners))
                {
                    foreach (var l in listeners)
                        l.OnStateChanged(evt);
                }
            }

            _hub.Clear<StateChangedEvent>();
        }
        void BroadcastStatusEvents()
        {
            var events = _hub.Consume<StatusChangedEvent>();

            foreach (ref readonly var evt in events)
            {
                if (_statusListeners.TryGetValue(evt.E, out var listeners))
                {
                    foreach (var l in listeners)
                        l.OnStatusChanged(evt);
                }
            }

            _hub.Clear<StatusChangedEvent>();
        }

        void BroadcastAnimationEvents()
        {
            var events = _hub.Consume<AnimationPhaseChangedEvent>();

            foreach (ref readonly var evt in events)
            {
                if (_animListeners.TryGetValue(evt.Entity, out var listeners))
                {
                    foreach (var l in listeners)
                        l.OnAnimationPhaseChanged(evt);
                }
            }

            _hub.Clear<AnimationPhaseChangedEvent>();
        }


        Entity GetEntity<T>(T evt)
        {
            if (evt is StateChangedEvent sce) return sce.Entity;
            if (evt is StatusChangedEvent stce) return stce.E;
            if (evt is AnimationPhaseChangedEvent ape) return ape.Entity;
            return default;
        }
        #endregion

        #region Utilities
        T Ensure<T>(GameObject go) where T : Component
            => go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();

        public bool TryGetGameObject(Entity entity, out GameObject go)
            => _entityToGO.TryGetValue(entity, out go);

        public Entity GetEntity(GameObject go)
            => _goToEntity.TryGetValue(go, out var e) ? e : default;

        void OnDestroy()
        {
            foreach (var go in _entityToGO.Values)
                if (go) Destroy(go);

            _entityToGO.Clear();
            _goToEntity.Clear();
        }
        public bool TryGetViewRoot(Entity e, out Transform root)
        {
            _entityToGO.TryGetValue(e, out GameObject value);

            root = value.transform;

            return root;
        }

        #endregion


    }


}