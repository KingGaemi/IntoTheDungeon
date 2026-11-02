using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Stats;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 캐릭터 종류별 특성(주로 몹): 기본 스탯 계산 뒤에 곱하는 배율 + 맞히거나 맞을 때 붙는 효과.
    /// 힘 하나가 체력·공격력을 같이 올리므로 "체력은 낮은데 아프게 때리는" 몹은 배율로 따로 만든다.
    /// </summary>
    public struct TraitComponent : IComponentData
    {
        public float HpMul, DamageMul, MoveSpeedMul, AttackSpeedMul;
        public float KnockbackResistAdd;   // 음수면 더 잘 밀리고 뜬다(가벼운 몹)
        public float Thorns;               // 근접으로 때린 쪽이 입힌 피해의 이 비율만큼 돌려받는다
        public float Lifesteal;            // 입힌 피해의 이 비율만큼 회복
        public float PoisonPerSec;         // 맞힌 피해의 이 비율을 초마다 독 피해로(방어 무시)
        public float PoisonDuration;

        public static readonly TraitComponent None = new() { HpMul = 1f, DamageMul = 1f, MoveSpeedMul = 1f, AttackSpeedMul = 1f };

        public readonly bool IsNone =>
            HpMul == 1f && DamageMul == 1f && MoveSpeedMul == 1f && AttackSpeedMul == 1f &&
            KnockbackResistAdd == 0f && Thorns == 0f && Lifesteal == 0f && PoisonPerSec == 0f;

        /// <summary>StatFormula.Apply 직후에 부른다(레벨·봉인석으로 다시 계산할 때마다). 현재 체력은 가득으로.</summary>
        public readonly void ApplyStats(ref StatusComponent st, ref DerivedStatsComponent d)
        {
            st.MaxHp = System.Math.Max(1, (int)System.MathF.Round(st.MaxHp * HpMul));
            st.CurrentHp = st.MaxHp;
            st.Damage = System.Math.Max(1, (int)System.MathF.Round(st.Damage * DamageMul));
            st.MovementSpeed *= MoveSpeedMul;
            st.AttackSpeed *= AttackSpeedMul;
            d.KnockbackResist = System.Math.Clamp(d.KnockbackResist + KnockbackResistAdd, -1f, 0.9f);
        }

        /// <summary>그 엔티티에 특성이 있으면 스탯에 적용.</summary>
        public static void ApplyIfAny(IEntityManager em, Entity e, ref StatusComponent st, ref DerivedStatsComponent d)
        {
            if (em.TryGetComponent<TraitComponent>(e, out var t)) t.ApplyStats(ref st, ref d);
        }
    }

    /// <summary>중독: 남은 시간 동안 Tick마다 피해(방어 무시). 캐릭터는 모두 갖고 있고 Remaining이 0이면 꺼진 상태.</summary>
    public struct PoisonComponent : IComponentData
    {
        public float Dps;
        public float Remaining;
        public float TickTimer;
        public float Carry;      // 정수 피해에 못 미친 누적
        public Entity Source;
        public readonly bool Active => Remaining > 0f;
    }

    /// <summary>독 피해를 0.5초마다 HpModificationQueue에 넣는다(DamageProcessingSystem보다 먼저).</summary>
    public sealed class PoisonSystem : GameSystem, ITick
    {
        const float Interval = 0.5f;

        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(PoisonComponent), typeof(StatusComponent), typeof(HpModificationQueue)))
            {
                var poisons = chunk.GetComponentArray<PoisonComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var queues = chunk.GetComponentArray<HpModificationQueue>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var p = ref poisons[i];
                    if (!p.Active) continue;
                    if (!statuses[i].IsAlive) { p.Remaining = 0f; continue; }

                    float step = System.MathF.Min(dt, p.Remaining);
                    p.Remaining -= dt;
                    p.Carry += p.Dps * step;
                    p.TickTimer += dt;
                    if (p.TickTimer < Interval && p.Remaining > 0f) continue;
                    p.TickTimer = 0f;
                    int dmg = (int)p.Carry;
                    if (dmg <= 0) continue;
                    p.Carry -= dmg;
                    queues[i].Enqueue(HpModification.Damage(dmg, p.Source));
                }
            }
        }
    }

    /// <summary>맞힘/맞음 특성 효과(HitDetectSystem에서 피해를 넣은 직후).</summary>
    public static class TraitHitEffects
    {
        public static void Apply(IEntityManager em, Entity hitbox, Entity victim, in HitboxComponent hb, int dealt)
        {
            if (dealt <= 0) return;
            var owner = hb.Owner;
            bool ownerAlive = em.Exists(owner) && em.TryGetComponent<StatusComponent>(owner, out var ost) && ost.IsAlive;

            // 공격자 특성: 흡혈·독
            if (ownerAlive && em.TryGetComponent<TraitComponent>(owner, out var atk))
            {
                if (atk.Lifesteal > 0f && em.HasComponent<HpModificationQueue>(owner))
                {
                    int heal = System.Math.Max(1, (int)System.MathF.Round(dealt * atk.Lifesteal));
                    em.GetComponent<HpModificationQueue>(owner).Enqueue(HpModification.Heal(heal, owner));
                }
                if (atk.PoisonPerSec > 0f && atk.PoisonDuration > 0f && em.HasComponent<PoisonComponent>(victim))
                {
                    ref var p = ref em.GetComponent<PoisonComponent>(victim);
                    float dps = hb.Damage * atk.PoisonPerSec;
                    if (!p.Active || dps >= p.Dps) { p.Dps = dps; p.Source = owner; }
                    p.Remaining = System.MathF.Max(p.Remaining, atk.PoisonDuration);
                }
            }

            // 맞은 쪽 특성: 가시(근접 평타·근접 히트박스만, 화살·검기는 안 돌려받음)
            if (ownerAlive && em.HasComponent<FollowOwnerComponent>(hitbox)
                && em.TryGetComponent<TraitComponent>(victim, out var def) && def.Thorns > 0f
                && em.HasComponent<HpModificationQueue>(owner))
            {
                int back = System.Math.Max(1, (int)System.MathF.Round(dealt * def.Thorns));
                em.GetComponent<HpModificationQueue>(owner).Enqueue(HpModification.Damage(back, victim));
            }
        }
    }
}
