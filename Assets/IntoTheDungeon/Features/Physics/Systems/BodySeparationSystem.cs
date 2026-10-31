using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.Physics.Systems
{
    /// <summary>
    /// 살아 있는 캐릭터끼리 몸(BodyComponent)이 겹치면 바닥 평면에서 서로 밀어낸다(팀 상관없이).
    /// 무게 비율로 나눠 밀고(무거운 쪽이 덜 밀림), 한 프레임에 다 풀지 않고 부드럽게 푼다(떨림 방지).
    /// 시체·높이 뜬 캐릭터(점프·띄우기)는 몸 충돌에서 빠진다.
    /// 이동(Motion·Knockback) 다음, TransformProjection 전에 돈다.
    /// </summary>
    public sealed class BodySeparationSystem : GameSystem, ITick
    {
        struct Body
        {
            public Entity E;
            public Vec2 Pos;
            public float Radius;
            public float InvMass;   // 0이면 안 밀림
            public Vec2 Push;
        }

        const float Stiffness = 12f;        // 클수록 빨리 떨어진다(초당 겹침의 이 비율만큼 해소)
        const float AirborneHeight = 0.3f;  // 이보다 높이 떠 있으면 몸 충돌 무시(위로 넘어감)

        IWalkArea _area;
        readonly List<Body> _bodies = new(64);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            world.TryGet(out _area);
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            Collect(em);
            if (_bodies.Count < 2) return;

            float k = System.MathF.Min(1f, Stiffness * dt);
            for (int a = 0; a < _bodies.Count; a++)
            {
                for (int b = a + 1; b < _bodies.Count; b++)
                {
                    var A = _bodies[a];
                    var B = _bodies[b];
                    if (A.InvMass + B.InvMass <= 0f) continue;

                    // 깊이를 늘려 타원을 원으로 바꿔 계산
                    float dx = B.Pos.X - A.Pos.X;
                    float dz = (B.Pos.Y - A.Pos.Y) / BodyComponent.DepthRatio;
                    float minDist = A.Radius + B.Radius;
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= minDist * minDist) continue;

                    float d = System.MathF.Sqrt(d2);
                    Vec2 n;
                    if (d > 1e-4f) n = new Vec2(dx / d, dz / d);
                    else n = new Vec2(a < b ? 1f : -1f, 0f);   // 완전히 겹치면 가로로 갈라 놓는다(항상 같은 쪽으로)

                    float overlap = (minDist - d) * k;
                    float wA = A.InvMass / (A.InvMass + B.InvMass);
                    float wB = 1f - wA;
                    // 원 공간 → 바닥 평면(깊이 다시 납작하게)
                    var push = new Vec2(n.X * overlap, n.Y * overlap * BodyComponent.DepthRatio);
                    A.Push = A.Push - push * wA;
                    B.Push = B.Push + push * wB;
                    _bodies[a] = A;
                    _bodies[b] = B;
                }
            }

            foreach (var body in _bodies)
            {
                if (body.Push == Vec2.Zero) continue;
                ref var t = ref em.GetComponent<TransformComponent>(body.E);
                var p = t.Position + body.Push;
                if (_area != null) p = _area.Clamp(p);
                t.Position = p;
                t.Moved = true;
            }
        }

        void Collect(IEntityManager em)
        {
            _bodies.Clear();
            foreach (var chunk in em.GetChunks(typeof(BodyComponent), typeof(TransformComponent), typeof(StatusComponent)))
            {
                var bodies = chunk.GetComponentArray<BodyComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (!statuses[i].IsAlive || transforms[i].Height > AirborneHeight) continue;
                    var body = bodies[i];
                    if (body.Radius <= 0f) continue;
                    _bodies.Add(new Body
                    {
                        E = entities[i],
                        Pos = transforms[i].Position,
                        Radius = body.Radius,
                        InvMass = body.Immovable ? 0f : 1f / body.Mass
                    });
#if UNITY_EDITOR
                    DrawEllipse(transforms[i].Position, body.Radius, UnityEngine.Color.cyan);
#endif
                }
            }
        }

#if UNITY_EDITOR
        // Scene 뷰(또는 Gizmos 켠 Game 뷰)에서 몸 크기 확인용: 바닥 평면 타원을 화면에 투영
        static void DrawEllipse(Vec2 c, float r, UnityEngine.Color color)
        {
            const int Seg = 16;
            var prev = new UnityEngine.Vector3(c.X + r, c.Y);
            for (int s = 1; s <= Seg; s++)
            {
                float a = s * (System.MathF.PI * 2f / Seg);
                var cur = new UnityEngine.Vector3(c.X + System.MathF.Cos(a) * r, c.Y + System.MathF.Sin(a) * r * BodyComponent.DepthRatio);
                UnityEngine.Debug.DrawLine(prev, cur, color);
                prev = cur;
            }
        }
#endif
    }
}
