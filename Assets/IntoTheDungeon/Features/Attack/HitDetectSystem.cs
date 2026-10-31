using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 히트박스 × 허트박스 AABB 겹침을 검사해 피격자의 HpModificationQueue에 데미지를 넣는다.
    /// 실제 HP 차감은 DamageProcessingSystem이 한다.
    /// </summary>
    public sealed class HitDetectSystem : GameSystem, ITick
    {
        struct Target
        {
            public Entity E;
            public TeamFlag Team;
            public Vec2 Min, Max;
            public float Height;
        }

        readonly List<Target> _targets = new(64);

        // 이 높이 차이 안이면 맞는다(점프로 머리 위를 넘거나, 공중 공격이 땅의 적을 맞히는 범위)
        const float HeightReach = 0.8f;

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            CollectTargets(em);
            if (_targets.Count == 0) return;

            foreach (var chunk in em.GetChunks(typeof(HitboxComponent), typeof(HitListComponent)))
            {
                var hitboxes = chunk.GetComponentArray<HitboxComponent>();
                var hitLists = chunk.GetComponentArray<HitListComponent>();
                var hitboxEntities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    var hb = hitboxes[i];
                    ref var hits = ref hitLists[i];
                    var min = hb.Center - hb.HalfExtents;
                    var max = hb.Center + hb.HalfExtents;
#if UNITY_EDITOR
                    DrawBox(min, max, hb.Height, UnityEngine.Color.red);
#endif
                    foreach (var t in _targets)
                    {
                        if (hits.IsFull) break;
                        if (t.E == hb.Owner || t.Team == hb.Team) continue;
                        if (hits.AlreadyHit(t.E.Index)) continue;
                        if (!Overlap(min, max, t.Min, t.Max)) continue;
                        if (System.MathF.Abs(hb.Height - t.Height) > HeightReach) continue;   // 높이가 너무 다르면 안 맞음

                        hits.AddHit(t.E.Index);
                        ref var q = ref em.GetComponent<HpModificationQueue>(t.E);
                        q.Enqueue(HpModification.Damage(hb.Damage, hb.Owner));
                        ApplyHitReaction(em, t.E, hb);

                        // 일반 화살: 첫 대상에 꽂히고 사라진다(수명을 0으로 → LifetimeSystem이 지움)
                        var self = hitboxEntities[i];
                        if (em.TryGetComponent<ProjectileComponent>(self, out var proj) && proj.DestroyOnHit)
                        {
                            if (em.HasComponent<LifetimeComponent>(self)) em.GetComponent<LifetimeComponent>(self).Remaining = 0f;
                            break;
                        }
                    }
                }
            }
        }

        void CollectTargets(IEntityManager em)
        {
            _targets.Clear();
            foreach (var chunk in em.GetChunks(typeof(HurtboxComponent),
                                               typeof(TransformComponent),
                                               typeof(TeamComponent),
                                               typeof(StatusComponent),
                                               typeof(HpModificationQueue)))
            {
                var hurtboxes = chunk.GetComponentArray<HurtboxComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (!statuses[i].IsAlive) continue;
                    var center = transforms[i].Position + hurtboxes[i].Offset;
                    var t = new Target
                    {
                        E = entities[i],
                        Team = teams[i].TeamFlag,
                        Min = center - hurtboxes[i].HalfExtents,
                        Max = center + hurtboxes[i].HalfExtents,
                        Height = transforms[i].Height
                    };
                    _targets.Add(t);
#if UNITY_EDITOR
                    DrawBox(t.Min, t.Max, t.Height, UnityEngine.Color.green);
#endif
                }
            }
        }

        // 맞은 쪽을 공격 방향으로 밀고 위로 띄운다(공중에서 또 맞으면 다시 띄워 공중 콤보)
        static void ApplyHitReaction(IEntityManager em, Entity victim, in HitboxComponent hb)
        {
            if (hb.KnockbackSpeed > 0f && em.HasComponent<KnockbackComponent>(victim))
            {
                ref var k = ref em.GetComponent<KnockbackComponent>(victim);
                k.Velocity = new Vec2(hb.FacingSign * hb.KnockbackSpeed, 0f);
            }
            if (hb.LaunchSpeed > 0f && em.HasComponent<HeightMotionComponent>(victim))
            {
                ref var m = ref em.GetComponent<HeightMotionComponent>(victim);
                if (m.VelocityZ < hb.LaunchSpeed) m.VelocityZ = hb.LaunchSpeed;
            }
        }

        static bool Overlap(Vec2 aMin, Vec2 aMax, Vec2 bMin, Vec2 bMax)
            => aMin.X <= bMax.X && aMax.X >= bMin.X && aMin.Y <= bMax.Y && aMax.Y >= bMin.Y;

#if UNITY_EDITOR
        // Scene 뷰(또는 Gizmos 켠 Game 뷰)에서 판정 영역 확인용
        // 바닥 평면 박스를 화면 투영(화면 y = 깊이 + 높이)해서 그린다
        static void DrawBox(Vec2 min, Vec2 max, float height, UnityEngine.Color c)
        {
            var a = new UnityEngine.Vector3(min.X, min.Y + height);
            var b = new UnityEngine.Vector3(max.X, min.Y + height);
            var d = new UnityEngine.Vector3(max.X, max.Y + height);
            var e = new UnityEngine.Vector3(min.X, max.Y + height);
            UnityEngine.Debug.DrawLine(a, b, c);
            UnityEngine.Debug.DrawLine(b, d, c);
            UnityEngine.Debug.DrawLine(d, e, c);
            UnityEngine.Debug.DrawLine(e, a, c);
        }
#endif
    }
}
