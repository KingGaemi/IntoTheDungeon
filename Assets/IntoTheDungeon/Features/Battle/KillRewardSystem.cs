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
using IntoTheDungeon.Features.Attack;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>처치 보상 판정을 이미 한 시체 표시.</summary>
    public struct KillRewardedTag : IComponentData { }

    /// <summary>
    /// 이 몹만 주는 잼(보스 잼): 보스로 나오면 BossGem 확정, 아니면 Gem을 Chance 확률로. GemPool 번호(-1 = 없음).
    /// </summary>
    public struct GemDropComponent : IComponentData
    {
        public int Gem, BossGem;
        public float Chance;
    }

    /// <summary>적 하나를 처치해 경험치·골드가 나왔다(이펙트용).</summary>
    public struct KillRewardEvent
    {
        public Vec2 Position;
        public float Height;   // 발 높이 + 머리 높이(숫자가 나올 자리)
        public int Xp, Gold;
        public string Gem;     // 잼을 떨궜으면 이름(아니면 null)
        public float GemR, GemG, GemB;
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
        const float BlastRadius = 1.8f;
        // 폭발 히트박스 수명(임시값). 이 시스템 뒤에 도는 LifetimeSystem이 같은 틱에 dt를 빼고 HitDetectSystem은 다음 틱에야 보므로,
        // 0.1이면 3배속·30fps(dt 0.1)에서 한 번도 못 맞히고 사라진다 → 두 틱은 버티게 넉넉히
        const float BlastLifetime = 0.2f;

        // 불씨 주머니: 쓰러진 적 자리에서 터져 주변 적에게 그 최대 체력 비율만큼(떼 몹이 줄줄이 터진다)
        static void CorpseBlast(IEntityManager em, Entity dead, int maxHp, Vec2 pos)
        {
            var hit = em.CreateEntity();
            em.AddComponent(hit, new HitboxComponent
            {
                Owner = dead,   // 쓰러진 몸이라 자기 자신은 이미 대상이 아님
                Team = Party.Team,
                Damage = System.Math.Max(1, (int)System.MathF.Round(maxHp * CombatPerks.CorpseBlast)),
                Center = pos,
                HalfExtents = new Vec2(BlastRadius, BlastRadius * 0.5f),
                KnockbackSpeed = 3f,
                LaunchSpeed = 2f,
                HitStun = 0.2f
            });
            em.AddComponent(hit, new LifetimeComponent { Remaining = BlastLifetime });
            em.AddComponent(hit, new HitListComponent());
        }

        // 역병의 손가락: 중독된 채 쓰러진 적 자리에서 남은 독 피해 × 비율이 터진다(독처럼 방어를 무시하는 마법 피해, 불씨 주머니와 같은 범위)
        static void PoisonBurst(IEntityManager em, Entity dead, int damage, Vec2 pos)
        {
            var hit = em.CreateEntity();
            em.AddComponent(hit, new HitboxComponent
            {
                Type = DamageType.Magic,
                Owner = dead,
                Team = Party.Team,
                Damage = System.Math.Max(1, damage),
                Center = pos,
                HalfExtents = new Vec2(BlastRadius, BlastRadius * 0.5f),
                KnockbackSpeed = 1.5f,
                HitStun = 0.1f
            });
            em.AddComponent(hit, new LifetimeComponent { Remaining = BlastLifetime });
            em.AddComponent(hit, new HitListComponent());
        }

        const float LevelUpHeal = 0.2f;   // 임시값

        const float EliteGemChance = 0.1f;   // 정예 방 몹 하나당 잼 확률(보스는 확정)
        const float EliteGoldMul = 1.3f;     // (임시값) 정예 길 보상 체감

        readonly List<(Entity e, KillRewardEvent evt, int maxHp, int level, bool boss)> _dead = new(8);
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
                    if (Revive.IsDowned(em, e)) continue;   // 무너진 해골: 완전히 부서져야 보상
                    float head = em.TryGetComponent<BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
                    int level = em.TryGetComponent<PrimaryStatsComponent>(e, out var ps) ? ps.Level : 1;
                    var evt = new KillRewardEvent { Position = transforms[i].Position, Height = transforms[i].Height + head };
                    _dead.Add((e, evt, statuses[i].MaxHp, level, em.HasComponent<BossComponent>(e)));
                }
            }
            if (_dead.Count == 0) return;

            var run = RunState.Current;
            Party.Collect(em, _party);
            float goldMul = run.BuildModifiers(-1).GoldMul;   // 잼(황수정)
            bool elite = (run.Current != null && run.Current.Type == RoomType.Elite) || run.ForcedEncounter == RoomType.Elite;   // 매복(강제 정예 구성)도 정예 처치 보상
            int fighters = Encounters.Fighters(run);

            // 청크 순회가 끝난 뒤 구조 변경
            foreach (var (e, evt0, maxHp, level, boss) in _dead)
            {
                em.AddComponent(e, new KillRewardedTag());
                if (CombatPerks.CorpseBlast > 0f) CorpseBlast(em, e, maxHp, evt0.Position);
                // 역병의 손가락: 중독된 채 쓰러지면 남은 독 피해(초당 × 남은 초) × 비율이 주변 적에게 터진다
                // (PoisonSystem은 다음 Tick에야 죽은 몸의 독을 끄므로 같은 Tick에 처치를 본 여기선 아직 살아 있다)
                if (CombatPerks.PoisonBurst > 0f && em.TryGetComponent<PoisonComponent>(e, out var poison) && poison.Active)
                {
                    int burst = (int)System.MathF.Round(poison.Dps * poison.Remaining * CombatPerks.PoisonBurst);
                    if (burst > 0) PoisonBurst(em, e, burst, evt0.Position);
                }
                // 사냥의 열기·사냥꾼: 적이 쓰러질 때마다 필살기 대기가 준다
                foreach (var m in _party)
                    if (m.Alive && em.TryGetComponent<MemberPerkComponent>(m.Entity, out var perk) && perk.KillCooldownCut > 0f
                        && em.HasComponent<SpecialSkillComponent>(m.Entity))
                    {
                        ref var sk = ref em.GetComponent<SpecialSkillComponent>(m.Entity);
                        sk.CooldownRemaining = System.MathF.Max(0f, sk.CooldownRemaining - perk.KillCooldownCut);
                    }
                // 영혼 흡입관·흡수: 적이 쓰러질 때마다 마나가 찬다(최대 마나까지)
                foreach (var m in _party)
                    if (m.Alive && em.TryGetComponent<MemberPerkComponent>(m.Entity, out var manaPerk) && manaPerk.KillManaAdd > 0f
                        && em.HasComponent<DerivedStatsComponent>(m.Entity))
                    {
                        ref var ds = ref em.GetComponent<DerivedStatsComponent>(m.Entity);
                        ds.Mana = System.MathF.Min(ds.MaxMana, ds.Mana + manaPerk.KillManaAdd);
                    }
                var evt = evt0;
                // 파티 인원 보정으로 줄어든 체력·마릿수만큼 보상이 줄지 않게 되돌린다(혼자여도 방 하나 보상은 3인과 비슷)
                int baseHp = (int)System.MathF.Round(maxHp / Encounters.HpMul(fighters) / DungeonTier.EnemyHpMul(run.StageTier));   // 던전 등급 보상은 레벨 항으로만(체력 배율까지 곱하면 과함)
                evt.Xp = (int)System.MathF.Round(RunData.KillXp(baseHp, level) * SealStone.RewardMul(run.SealTier));   // 봉인석 보상
                float countBack = boss ? 1f : 1f / Encounters.CountMul(fighters);
                evt.Gold = System.Math.Max(1, (int)System.MathF.Round(run.KillGold(evt.Xp) * goldMul * countBack * (elite ? EliteGoldMul : 1f)));
                // 보스 잼: 그 보스는 확정, 지정 몹(해골 기사)은 확률. 없으면 예전처럼 보스·정예 무작위
                GemDef gem = null;
                em.TryGetComponent<GemDropComponent>(e, out var drop);
                bool hasDrop = em.HasComponent<GemDropComponent>(e);
                if (boss && hasDrop && drop.BossGem >= 0) gem = run.FindGem(GemPool.All[drop.BossGem]);
                else if (hasDrop && drop.Gem >= 0 && run.Rng.NextDouble() < drop.Chance) gem = run.FindGem(GemPool.All[drop.Gem]);
                else if (boss || (elite && run.Rng.NextDouble() < EliteGemChance)) gem = run.FindGem();
                if (gem != null)
                {
                    evt.Gem = gem.Name;
                    evt.GemR = gem.R; evt.GemG = gem.G; evt.GemB = gem.B;
                }
                run.Gold += evt.Gold;
                run.Stats.GoldEarned += evt.Gold;
                run.Stats.Kills++;
                if (elite) run.Stats.EliteKills++;
                if (boss) run.Stats.BossKills++;
                run.LastGold += evt.Gold;
                run.LastXp += evt.Xp;
                BattleRewardEvents.RaiseKilled(evt);

                for (int slot = 0; slot < run.PartySize; slot++)
                {
                    bool here = Party.TryGetSlot(_party, slot, out var m) && m.Alive && em.Exists(m.Entity);
                    int xpForSlot = here ? evt.Xp : evt.Xp / 2;
                    xpForSlot = (int)System.MathF.Round(xpForSlot * run.BuildModifiers(slot).XpMul);   // 경험치 배율(유물·특성)
                    int ups = run.AddXp(slot, xpForSlot);
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
            CombatMeter.Heal(em, e, st.CurrentHp - (st.MaxHp - missing));   // 레벨 업 회복
            if (hasMana)
            {
                ref var ds = ref em.GetComponent<DerivedStatsComponent>(e);
                ds.Mana = System.MathF.Min(ds.MaxMana, before.Mana);
            }
            BattleRewardEvents.RaiseLeveledUp(new LevelUpEvent { Entity = e, Level = run.LevelOf(slot) });
        }
    }
}
