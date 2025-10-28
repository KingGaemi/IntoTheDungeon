using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Physics.Implementation;
using IntoTheDungeon.Unity.Bridge.Physics.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.Physics
{
    /// <summary>
    /// PhysicsOpStore를 읽어서 Unity Rigidbody2D에 적용하는 Bridge
    /// </summary>
    [DefaultExecutionOrder(-7500)]
    public sealed class PhysicsBridge : MonoBehaviour, IWorldInjectable, IPhysicsPort
    {
        #region Services
        IPhysicsOpStore _opStore;
        IPhysicsBodyStore _bodyStore;

        IPhysicsOpResolveSystem _resolver;


        #endregion

        #region Init
        public void Init(IWorld world)
        {
            if (!world.TryGet(out _opStore))
            {
                Debug.LogError("[PhysicsBridge] No PhysicsOpStore");
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
        }
        #endregion

        #region Update
        void FixedUpdate()
        {
            _resolver.Resolve();
            ExecuteOps();
        }

        void ExecuteOps()
        {
            var handles = _opStore.Handles;  // ReadOnlySpan
            var kinds = _opStore.Kinds;
            var flags = _opStore.Flags;
            var vecs = _opStore.Vecs;
            var angles = _opStore.Angles;

            for (int i = 0; i < _opStore.Count; i++)
            {
                if (!_bodyStore.TryGet(handles[i], out var body))
                {
                    Debug.LogWarning($"[PhysicsBridge] Body not found for handle {handles[i].Value}");
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



        #region Cleanup
        void OnDestroy()
        {

        }

        public bool TryGetRigidbody(PhysicsHandle h, out Rigidbody2D rb)
        {
            _bodyStore.TryGet(h, out var pb);

            rb = pb.Rb;

            return rb;
        }

        public void AdoptBody(PhysicsHandle h, Rigidbody2D rb, Transform newRoot)
        {
            // 1) 바디 GO를 새 뷰 밑으로 편입
            rb.transform.SetParent(newRoot, worldPositionStays: true);

            // 2) 맵 갱신은 필요 없음(핸들→rb 유지). 필요 시 레이어/태그 동기화
            // 3) 그래픽 루트와 물리 루트 분리 권장: newRoot/Graphics, newRoot/PhysicsBody
        }
        #endregion
    }
}