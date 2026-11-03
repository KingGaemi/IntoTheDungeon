using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Battle;

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
                    // 날아가는 히트박스(검기·화살)는 이번 프레임에 지나온 구간 전체로 판정한다.
                    // 프레임마다 한 점만 보면 배속(큰 dt)·낮은 프레임에서 바로 앞이나 지나친 적을 건너뛴다
                    if (em.TryGetComponent<ProjectileComponent>(hitboxEntities[i], out var flying))
                    {
                        float back = flying.Velocity.X * dt;   // ProjectileMotionSystem이 이번 프레임에 옮긴 거리
                        if (back > 0f) min = new Vec2(min.X - back, min.Y);
                        else if (back < 0f) max = new Vec2(max.X - back, max.Y);
                    }
#if UNITY_EDITOR
                    DrawBox(min, max, hb.Height, UnityEngine.Color.red);
#endif
                    // 단일 대상(일반 근접 평타): 겹친 대상 중 공격자 쪽 끝에서 가장 가까운 하나만
                    if (hb.MaxTargets > 0)
                    {
                        if (hits.HitCount >= hb.MaxTargets) continue;
                        float nearX = hb.Center.X - hb.FacingSign * hb.HalfExtents.X;
                        int best = -1;
                        float bestD = float.MaxValue;
                        for (int k = 0; k < _targets.Count; k++)
                        {
                            var t = _targets[k];
                            if (!CanHit(hb, ref hits, min, max, t)) continue;
                            float d = System.MathF.Abs((t.Min.X + t.Max.X) * 0.5f - nearX);
                            if (d < bestD) { bestD = d; best = k; }
                        }
                        if (best < 0) continue;
                        var target = _targets[best];
                        hits.AddHit(target.E.Index);
                        int dealt = Mitigated(em, target.E, hb);
                        em.GetComponent<HpModificationQueue>(target.E).Enqueue(HpModification.Damage(dealt, hb.Owner));
                        ApplyHitReaction(em, target.E, hb);
                        TraitHitEffects.Apply(em, hitboxEntities[i], target.E, hb, dealt);
                        if (hb.StopPower > 0) PatternBreak.Apply(em, target.E, hb.StopPower, flinch: false, pushDir: hb.FacingSign);
                        continue;
                    }

                    foreach (var t in _targets)
                    {
                        if (hits.IsFull) break;
                        if (!CanHit(hb, ref hits, min, max, t)) continue;

                        hits.AddHit(t.E.Index);
                        int dealt = Mitigated(em, t.E, hb);
                        em.GetComponent<HpModificationQueue>(t.E).Enqueue(HpModification.Damage(dealt, hb.Owner));
                        ApplyHitReaction(em, t.E, hb);
                        TraitHitEffects.Apply(em, hitboxEntities[i], t.E, hb, dealt);
                        if (hb.StopPower > 0) PatternBreak.Apply(em, t.E, hb.StopPower, flinch: false, pushDir: hb.FacingSign);   // 필살기 저지력

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

        static bool CanHit(in HitboxComponent hb, ref HitListComponent hits, Vec2 min, Vec2 max, in Target t)
        {
            if (t.E == hb.Owner || t.Team == hb.Team) return false;
            if (hits.AlreadyHit(t.E.Index)) return false;
            if (!Overlap(min, max, t.Min, t.Max)) return false;
            return System.MathF.Abs(hb.Height - t.Height) <= HeightReach;   // 높이가 너무 다르면 안 맞음
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

        // 방어력(물리)·마법 저항(마법)으로 줄인 피해
        static int Mitigated(IEntityManager em, Entity victim, in HitboxComponent hb)
        {
            int dmg = hb.Damage;
            // 난이도: 파티가 받는 피해 배율
            if (em.TryGetComponent<TeamComponent>(victim, out var team) && team.TeamFlag == Party.Team)
                dmg = System.Math.Max(1, (int)System.MathF.Round(dmg * DifficultyTable.PartyDamageTaken(RunState.Current.Difficulty)));
            // 방패(전사 필살기): 받는 피해 감소
            if (em.TryGetComponent<GuardComponent>(victim, out var guard) && guard.Remaining > 0f)
                dmg = System.Math.Max(1, (int)System.MathF.Round(dmg * guard.DamageMul));
            // 배치 열: 전열은 덜, 후열은 더 아프다
            if (em.TryGetComponent<FormationComponent>(victim, out var form) && form.Active)
                dmg = System.Math.Max(1, (int)System.MathF.Round(dmg * Formation.DamageTakenMul(form.Row)));
            if (em.TryGetComponent<DerivedStatsComponent>(victim, out var d))
                dmg = StatFormula.Mitigate(dmg, hb.Type == DamageType.Magic ? d.MagicResist : d.Armor);
            CombatMeter.Block(em, victim, hb.Damage - dmg);   // 딜 미터: 막은 피해(방어·난이도·방패)
            return dmg;
        }

        // 맞은 쪽을 공격 방향으로 밀고 위로 띄운다(공중에서 또 맞으면 다시 띄워 공중 콤보)
        static void ApplyHitReaction(IEntityManager em, Entity victim, in HitboxComponent hb)
        {
            // 정신력 → 넉백 저항: 밀리고 뜨는 세기를 줄인다
            float keep = em.TryGetComponent<DerivedStatsComponent>(victim, out var d) ? 1f - d.KnockbackResist : 1f;
            if (hb.KnockbackSpeed > 0f && em.HasComponent<KnockbackComponent>(victim))
            {
                ref var k = ref em.GetComponent<KnockbackComponent>(victim);
                k.Velocity = new Vec2(hb.FacingSign * hb.KnockbackSpeed * keep, 0f);
            }
            if (hb.LaunchSpeed > 0f && em.HasComponent<HeightMotionComponent>(victim))
            {
                ref var m = ref em.GetComponent<HeightMotionComponent>(victim);
                float launch = hb.LaunchSpeed * keep;
                if (m.VelocityZ < launch) m.VelocityZ = launch;
            }
            // 경직은 HitStunSystem이 적용(같은 프레임 여러 번 맞으면 가장 긴 것)
            if (hb.HitStun > 0f && em.HasComponent<HitStunComponent>(victim))
            {
                ref var s = ref em.GetComponent<HitStunComponent>(victim);
                if (s.Pending < hb.HitStun) s.Pending = hb.HitStun;
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
