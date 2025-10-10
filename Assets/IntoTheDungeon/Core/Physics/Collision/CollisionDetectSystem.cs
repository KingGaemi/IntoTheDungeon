using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;

using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Physics.Collision.Components;

using System.Collections.Generic;

namespace IntoTheDungeon.Core.Physics.Collision.Systems
{
    
    public sealed class CollisionDetectSystem : GameSystem, ITick
    {
        struct Item 
        {
            public Entity E;
            public Vec2   P;
            public float  R;
            public int    Layer;
            public uint   Mask;
            public PhysicsUtility.AABB AABB;
        }

        readonly List<Item> _circles = new(256);

        // 현재/이전 프레임의 겹침 쌍
        HashSet<Pair> _now  = new(512);
        HashSet<Pair> _prev = new(512);

        readonly struct Pair : System.IEquatable<Pair>
        {
            public readonly Entity A;
            public readonly Entity B;

            public Pair(Entity a, Entity b)
            {
                // 정규화하여 (a,b)와 (b,a)를 동일 취급
                // Entity가 Id(또는 Index)를 가진다고 가정
                if (a.Index <= b.Index) { A = a; B = b; }
                else { A = b; B = a; }
            }
            public bool Equals(Pair o) => A.Equals(o.A) && B.Equals(o.B);
            public override int GetHashCode() => System.HashCode.Combine(A, B);
        }

        public void Tick(float dt)
        {
            var evq = _world.Require<ICollisionEvents>();
            var em  = _world.EntityManager;

            // 1) 원형 콜라이더 일괄 수집
            _circles.Clear();
            foreach (var ch in em.GetChunks(typeof(TransformComponent), typeof(CircleColliderComponent)))
            {
                var ents = ch.GetEntities();
                var tArr = ch.GetComponentArray<TransformComponent>();
                var cArr = ch.GetComponentArray<CircleColliderComponent>();
                for (int i = 0; i < ch.Count; i++)
                {
                    var p = tArr[i].Position;
                    var c = cArr[i];
                    _circles.Add(new Item {
                        E = ents[i],
                        P = p,
                        R = c.Radius,
                        Layer = (int)c.Layer,
                        Mask  = c.LayerMask,
                        AABB  = c.GetAABB(p)
                    });
                }
            }

            // 2) 충돌 판정 (동일 풀: i<j만)
            _now.Clear();
            var list = _circles;
            int n = list.Count;

            for (int i = 0; i < n; i++)
            {
                var A = list[i];
                for (int j = i + 1; j < n; j++)
                {
                    var B = list[j];

                    // 레이어마스크 상호 허용 확인
                    if (((A.Mask & (1u << B.Layer)) == 0) && ((B.Mask & (1u << A.Layer)) == 0))
                        continue;

                    // AABB 브로드페이즈
                    if (!OverlapAABB(A.AABB, B.AABB)) continue;

                    // 정확 판정
                    var d2 = Vec2.DistanceSquared(A.P, B.P);
                    var r  = A.R + B.R;
                    if (d2 <= r * r)
                        _now.Add(new Pair(A.E, B.E));
                }
            }

            // 3) 이벤트 생성
            foreach (var p in _now)
                if (!_prev.Contains(p))
                    evq.EnqueueEnter(p.A, p.B);

            foreach (var p in _prev)
                if (!_now.Contains(p))
                    evq.EnqueueExit(p.A, p.B);

            // 필요하면 Stay도 발행 가능
                        // foreach (var p in _now) if (_prev.Contains(p)) evq.Stay(p.A, p.B);

                        // 4) 스왑(할당 최소화)
            (_now, _prev) = (_prev, _now);
            _now.Clear();
        }

        static bool OverlapAABB(PhysicsUtility.AABB a, PhysicsUtility.AABB b)
            => !(a.Max.X < b.Min.X || a.Min.X > b.Max.X || a.Max.Y < b.Min.Y || a.Min.Y > b.Max.Y);
    }
}