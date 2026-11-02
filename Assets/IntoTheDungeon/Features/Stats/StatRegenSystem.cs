using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Features.Stats
{
    /// <summary>살아 있는 캐릭터의 초당 체력·마나 회복(힘·지능에서 나옴). 체력은 정수라 모자란 회복량을 누적해 둔다.</summary>
    public sealed class StatRegenSystem : GameSystem, ITick
    {
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(StatusComponent), typeof(DerivedStatsComponent)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var derived = chunk.GetComponentArray<DerivedStatsComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var st = ref statuses[i];
                    ref var d = ref derived[i];
                    if (!st.IsAlive) continue;

                    if (d.HpRegen > 0f && st.CurrentHp < st.MaxHp)
                    {
                        d.HpRegenCarry += d.HpRegen * dt;
                        int whole = (int)d.HpRegenCarry;
                        if (whole > 0)
                        {
                            d.HpRegenCarry -= whole;
                            int before = st.CurrentHp;
                            st.CurrentHp = System.Math.Min(st.MaxHp, st.CurrentHp + whole);
                            IntoTheDungeon.Features.Battle.CombatMeter.Heal(_world.EntityManager, entities[i], st.CurrentHp - before);   // 딜 미터 회복량
                        }
                    }
                    else d.HpRegenCarry = 0f;

                    if (d.Mana < d.MaxMana) d.Mana = System.MathF.Min(d.MaxMana, d.Mana + d.ManaRegen * dt);
                }
            }
        }
    }
}
