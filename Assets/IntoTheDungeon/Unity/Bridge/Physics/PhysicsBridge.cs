using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Unity.Bridge.Physics.Abstractions;
using IntoTheDungeon.Core.Runtime.Physics;

namespace IntoTheDungeon.Unity.Bridge.Physics
{
    /// <summary>
    /// PhysicsOpStore를 읽어서 Unity Rigidbody2D에 적용하는 Bridge
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class PhysicsBridge : MonoBehaviour, IWorldInjectable, IPhysicsPort
    {
        [SerializeField] int _maxCreatesPerFrame = 256;
        [SerializeField] int _maxBufferCapacity = 4096;

        #region Services
        IPhysicsCommandStore _opStore_out;
        IPhysicsFeedbackStore _opStore_in;
        IPhysicsBodyStore _bodyStore;
        IPhysicsOpResolveSystem _resolver;

        IBodyCreateQueue _bodyQueue;
        BodyCreateSpec[] _createBuffer;
        int _createCount;


        #endregion

        #region Init
        public void Init(IWorld world)
        {
            if (!world.TryGet(out _opStore_out))
            {
                Debug.LogError("[PhysicsBridge] No PhysicsOpStore_Out");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _opStore_in))
            {
                Debug.LogError("[PhysicsBridge] No PhysicsOpStore_In");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _bodyStore))
            {
                Debug.LogError("[PhysicsBridge] No PhysicsBodyStore");
                enabled = false;
                return;
            }

            if (!world.TryGet(out _resolver))
            {
                Debug.LogError("[PhysicsBridge] No PhysicsOpResolveSystem");
                enabled = false;
                return;
            }
            if (!world.TryGet(out _bodyQueue))
            {
                Debug.LogError("[PhysicsBridge] No BodyCreateQueue");
                enabled = false;
                return;
            }
        }
        #endregion

        #region Update

        void Update()
        {

            ProcessCreates();
        }
        void FixedUpdate()
        {
            _resolver.Resolve();
            ExecuteOps();




            SyncPhysToEcs();
        }

        void ExecuteOps()
        {
            var handles = _opStore_out.Handles;  // ReadOnlySpan
            var kinds = _opStore_out.Kinds;
            var flags = _opStore_out.Flags;
            var vecs = _opStore_out.Vecs;
            var angles = _opStore_out.Angles;

            for (int i = 0; i < _opStore_out.Count; i++)
            {
                if (!_bodyStore.TryGet(handles[i], out var body))
                {
                    Debug.LogWarning($"[PhysicsBridge] Body not found for handle {handles[i].Index}");
                    continue;
                }

                var rb = body.Rb;


                Dispatch(rb, kinds[i], flags[i], vecs[i], angles[i]);
            }
        }

        void Dispatch(Rigidbody2D rb, PhysicsOpKind kind, PhysFlags flags, Vec2 vec, float angle)
        {
            switch (kind)
            {
                case PhysicsOpKind.AddForce:
                    rb.AddForce(new Vector2(vec.X, vec.Y), ForceMode2D.Force);
                    break;

                case PhysicsOpKind.AddImpulse:
                    rb.AddForce(new Vector2(vec.X, vec.Y), ForceMode2D.Impulse);
                    break;

                case PhysicsOpKind.AddTorque:
                    rb.AddTorque(angle, ForceMode2D.Force);
                    break;

                case PhysicsOpKind.Teleport:
                    if ((flags & PhysFlags.HasPos) != 0)
                        rb.position = new Vector2(vec.X, vec.Y);
                    if ((flags & PhysFlags.HasAngle) != 0)
                        rb.rotation = angle * Mathf.Rad2Deg;
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    break;

                case PhysicsOpKind.SetTransform:
                    if ((flags & PhysFlags.HasPos) != 0)
                        rb.position = new Vector2(vec.X, vec.Y);
                    if ((flags & PhysFlags.HasAngle) != 0)
                        rb.rotation = angle * Mathf.Rad2Deg;
                    break;

                case PhysicsOpKind.SetLinearVelocity:
                    rb.linearVelocity = new Vector2(vec.X, rb.linearVelocityY);
                    break;

                case PhysicsOpKind.SetAngularVelocity:
                    rb.angularVelocity = angle * Mathf.Rad2Deg;
                    break;

                case PhysicsOpKind.SyncEcsToPhys:
                    // 구현 필요
                    break;

                case PhysicsOpKind.SyncPhysToEcs:
                    // 구현 필요
                    break;
            }
        }
        #endregion


        void Awake()
        {
            if (_maxCreatesPerFrame <= 0) _maxCreatesPerFrame = 128;
            if (_maxBufferCapacity <= 0) _maxBufferCapacity = 4096;
            _createBuffer = new BodyCreateSpec[_maxCreatesPerFrame];
        }
        #region Cleanup
        void OnDestroy()
        {

        }
        // PhysicsBridge.FixedUpdate
        void SyncPhysToEcs()
        {


            var handles = _bodyStore.AllHandles();
            foreach (var h in handles)
            {
                if (!_bodyStore.TryGet(h, out var b)) continue;

                var rb = b.Rb;

                var p = rb.position;
                var a = rb.rotation * Mathf.Deg2Rad;
                // var v = rb.linearVelocity;
                // var w = rb.angularVelocity * Mathf.Deg2Rad;

                var f = PhysFlags.HasPos | PhysFlags.HasAngle;

                // Vec2 한 개만 있는 스토어라면: 위치/속도를 순차 Push 하거나,
                // 구조를 확장(예: 두 번째 Vec 버퍼)하라.
                // 간단히 하나의 패킷으로 보낸다면 Vec=pos, Angle=a로 쓰고,
                // Vel은 Flags로 표기 후 별도 채널을 추가하거나 아래처럼 확장:

                _opStore_in.Push(h, PhysicsOpKind.SyncPhysToEcs, new Vec2(p.x, p.y), a, f);
                // 선택: 속도 전용 채널이 있다면
                //_fb.Push(h, PhysicsOpKind.SyncPhysToEcsVelocity, new Vec2(v.x, v.y), w, PhysFlags.HasLinVel|PhysFlags.HasAngVel);
            }

        }


        public bool TryGetRigidbody(PhysicsHandle h, out Rigidbody2D rb)
        {
            if (_bodyStore.TryGet(h, out var pb) && pb.Rb != null) { rb = pb.Rb; return true; }
            rb = null; return false;
        }

        public void AdoptBody(PhysicsHandle h, Transform newRoot)
        {

            _bodyStore.TryGet(h, out var body);

            var rb = body.Rb;
            var col = body.Collider;


            // Debug.Log($"AdoptBody handle {h.Index}");
            rb.transform.SetParent(newRoot, worldPositionStays: true);
            col.transform.SetParent(newRoot, worldPositionStays: true);

            // 2) 맵 갱신은 필요 없음(핸들→rb 유지). 필요 시 레이어/태그 동기화
            // 3) 그래픽 루트와 물리 루트 분리 권장: newRoot/Graphics, newRoot/PhysicsBody
        }
        void EnsureCapacity(int required)
        {
            if (_createBuffer == null || _createBuffer.Length == 0)
                _createBuffer = new BodyCreateSpec[Mathf.Min(_maxCreatesPerFrame, _maxBufferCapacity)];

            if (required <= _createBuffer.Length) return;

            // required 최소 1 보장
            int want = Mathf.Max(1, required);
            int next = Mathf.NextPowerOfTwo(want);
            int cap = Mathf.Min(next, _maxBufferCapacity);
            System.Array.Resize(ref _createBuffer, cap);
        }

        public void ProcessCreates()
        {

            _createCount = 0;
            EnsureCapacity(_createCount + _maxCreatesPerFrame);
            while (_bodyQueue.Drain(_createBuffer, ref _createCount) > 0 && _createCount < _maxBufferCapacity)
            {
                EnsureCapacity(_createCount + _maxCreatesPerFrame);
            }
            int count = Mathf.Min(_createCount, _maxCreatesPerFrame);
            for (int i = 0; i < count; i++)
            {

                CreateBody(_createBuffer[i]);

            }

        }

        public GameObject CreateBody(BodyCreateSpec spec)
        {
            var go = new GameObject($"Phys_{spec.Handle.Index}");
            go.transform.SetPositionAndRotation(new Vector3(spec.Position.X, spec.Position.Y, 0f), Quaternion.Euler(0, 0, spec.Rotation));
            go.transform.SetParent(gameObject.transform);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = spec.BodyType switch
            {
                BodyType.Dynamic => RigidbodyType2D.Dynamic,
                BodyType.Kinematic => RigidbodyType2D.Kinematic,
                _ => RigidbodyType2D.Static
            };
            rb.mass = spec.Mass;
            rb.gravityScale = spec.GravityScale;
            rb.freezeRotation = true;

            Collider2D collider;
            switch (spec.ColliderType)
            {
                case ColliderType.Box:
                    collider = go.AddComponent<BoxCollider2D>();
                    break;
                case ColliderType.Circle:
                    collider = go.AddComponent<CircleCollider2D>();
                    break;
                case ColliderType.Capsule:
                    collider = go.AddComponent<CapsuleCollider2D>();
                    break;
                default:
                    collider = go.AddComponent<BoxCollider2D>();
                    break;
            }


            var body = new UnityPhysicsBody(rb, collider);
            _bodyStore.Bind(spec.Handle, body);
            // Debug.Log($"[PhysicsBridge] Body Binded {spec.Handle.Index}");
            return go;

        }
        #endregion
    }
}
