using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>처치 보상 판정을 이미 한 시체 표시.</summary>
    public struct KillRewardedTag : IComponentData { }

    /// <summary>적 하나를 처치해 경험치·골드가 나왔다(이펙트용).</summary>
    public struct KillRewardEvent
    {
        public Vec2 Position;
        public float Height;   // 발 높이 + 머리 높이(숫자가 나올 자리)
        public int Xp, Gold;
    }

    /// <summary>전투 중 파티원이 레벨 업했다(이펙트용).</summary>
    public struct LevelUpEvent
    {
        public Entity Entity;
        public int Level;
    }

    /// <summary>전투 피드백 이벤트(Unity 쪽 렌더러가 받는다).</summary>
    public static class BattleRewardEvents
    {
        public static event System.Action<KillRewardEvent> Killed;
        public static event System.Action<LevelUpEvent> LeveledUp;

        internal static void RaiseKilled(in KillRewardEvent e) => Killed?.Invoke(e);
        internal static void RaiseLeveledUp(in LevelUpEvent e) => LeveledUp?.Invoke(e);
    }

    /// <summary>
    /// 적이 쓰러질 때마다 그 자리에서 경험치·골드를 준다(RunData.KillXp·KillGold).
    /// 경험치는 파티원 모두에게(살아 있으면 전부, 쓰러졌거나 이번 방에 못 나왔으면 절반), 골드는 판에 쌓인다.
    /// 전투 중 레벨이 오르면 바로 능력치를 다시 계산하고, 잃은 체력은 그대로 둔 채 늘어난 최대 체력만큼 + 최대 체력의 20%를 회복한다.
    /// </summary>
    public sealed class KillRewardSystem : GameSystem, ITick
    {
        const float LevelUpHeal = 0.2f;   // 임시값

        readonly List<(Entity e, KillRewardEvent evt, int maxHp, int level)> _dead = new(8);
        readonly List<Party.Member> _party = new(8);

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            _dead.Clear();
            foreach (var chunk in em.GetChunks(typeof(StatusComponent), typeof(TeamComponent), typeof(TransformComponent)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (statuses[i].IsAlive || teams[i].TeamFlag == Party.Team) continue;
                    var e = entities[i];
                    if (em.HasComponent<KillRewardedTag>(e)) continue;
                    float head = em.TryGetComponent<BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
                    int level = em.TryGetComponent<PrimaryStatsComponent>(e, out var ps) ? ps.Level : 1;
                    var evt = new KillRewardEvent { Position = transforms[i].Position, Height = transforms[i].Height + head };
                    _dead.Add((e, evt, statuses[i].MaxHp, level));
                }
            }
            if (_dead.Count == 0) return;

            var run = RunState.Current;
            Party.Collect(em, _party);

            // 청크 순회가 끝난 뒤 구조 변경
            foreach (var (e, evt0, maxHp, level) in _dead)
            {
                em.AddComponent(e, new KillRewardedTag());
                var evt = evt0;
                evt.Xp = RunData.KillXp(maxHp, level);
                evt.Gold = run.KillGold(evt.Xp);
                run.Gold += evt.Gold;
                run.LastGold += evt.Gold;
                run.LastXp += evt.Xp;
                BattleRewardEvents.RaiseKilled(evt);

                for (int slot = 0; slot < run.PartySize; slot++)
                {
                    bool here = Party.TryGetSlot(_party, slot, out var m) && m.Alive && em.Exists(m.Entity);
                    int ups = run.AddXp(slot, here ? evt.Xp : evt.Xp / 2);
                    if (ups > 0 && here) LevelUpInBattle(em, m.Entity, run, slot);
                }
            }
        }

        void LevelUpInBattle(IEntityManager em, Entity e, RunData run, int slot)
        {
            ref var st = ref em.GetComponent<StatusComponent>(e);
            int missing = st.MaxHp - st.CurrentHp;
            bool hasMana = em.TryGetComponent<DerivedStatsComponent>(e, out var before);

            BattleFlowSystem.RecalculatePartyStats(em, e, run, slot);   // 체력·마나가 가득으로 바뀜

            st = ref em.GetComponent<StatusComponent>(e);
            st.CurrentHp = System.Math.Clamp(st.MaxHp - missing + (int)System.MathF.Round(st.MaxHp * LevelUpHeal), 1, st.MaxHp);
            if (hasMana)
            {
                ref var ds = ref em.GetComponent<DerivedStatsComponent>(e);
                ds.Mana = System.MathF.Min(ds.MaxMana, before.Mana);
            }
            BattleRewardEvents.RaiseLeveledUp(new LevelUpEvent { Entity = e, Level = run.LevelOf(slot) });
        }
    }
}
