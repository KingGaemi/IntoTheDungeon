using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Features.AI;
using IntoTheDungeon.Features.Command;

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>
    /// 방 하나의 전투: 지도에서 방을 고르면(RunState) 그 방의 몹 구성으로 시작 연출 → 웨이브 스폰 →
    /// 적 전멸이면 다음 웨이브 → 마지막까지 깨면 승리(파티 체력을 판에 기록). 파티가 전멸하면 패배(판 끝).
    /// 파티 체력은 방과 방 사이에 이어진다. 몹은 파티 중심 앞쪽(벽에 막히면 반대쪽)에 나온다.
    /// SpawnSystem보다 먼저 돌아야 같은 프레임에 스폰된다.
    /// </summary>
    /// <summary>레벨을 이미 입힌 적 표시.</summary>
    public struct EnemyLeveledTag : IComponentData { }

    public sealed class BattleFlowSystem : GameSystem, ITick
    {
        const float SpawnSettleTime = 1f;
        const float EnemyHpBase = 1.15f, EnemyDamageBase = 1.35f;   // 몹 전체 배율(임시값, 특성·레벨·봉인석 위에 곱함). 체력 1.6 → 1.15(지루함)
        const float PartyStartInset = 4f;   // 파티는 방 왼쪽 끝에서 이만큼 안쪽에서 시작(몹은 오른쪽에서 옴)   // 스폰 직후엔 아직 엔티티가 없을 수 있어 이 시간 뒤부터 전멸 판정

        BattleProgress _progress;
        BattleConfig _config;
        ISystemSpawnQueue _spawnQueue;
        IRecipeTable _recipes;
        IWalkArea _area;
        INameTable _names;
        IViewOpQueue _viewOps;
        bool _advanced;   // 이번 웨이브 사이에 이미 다음 구역으로 옮겼나
        readonly System.Random _rng = new(7);
        bool _partySpawned, _hpApplied;
        readonly System.Collections.Generic.List<Entity> _leveled = new(16);
        readonly System.Collections.Generic.List<int> _expectedSlots = new(4);   // 이번 방에 나오는 파티원 슬롯(스폰 순서)
        readonly System.Collections.Generic.List<Party.Member> _party = new(8);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _progress) && world.TryGet(out _config)
                   && world.TryGet(out _spawnQueue) && world.TryGet(out _recipes);
            world.TryGet(out _area);
            world.TryGet(out _names);
            world.TryGet(out _viewOps);
        }

        public void Tick(float dt)
        {
            var p = _progress;
            p.PhaseTime += dt;

            p.EnemiesAlive = CountEnemies();

            var run = RunState.Current;
            if (p.Phase == BattlePhase.Map)
            {
                if (run.Phase == RunPhase.InRoom && run.Current != null) BeginRoom(run.Current);
                return;
            }

            // 배치: 판 기록대로 파티를 배치 칸에 세우고(쓰러진 슬롯은 안 나옴) 「전투 시작」을 기다린다
            if (p.Phase == BattlePhase.Deploy) { TickDeploy(run); return; }

            bool partyFound = HasPartyMember();
            Party.TryGetCenter(_world.EntityManager, out var partyPos, out int partyAlive);
            if (!partyFound || p.IsOver) return;   // 파티가 아직 스폰 전이면 기다린다

            if (partyAlive == 0 && _hpApplied) { Enter(BattlePhase.Defeat); run.Lose(); return; }

            switch (p.Phase)
            {
                case BattlePhase.Intro:
                    if (!_hpApplied) { ApplyRunToParty(run); _hpApplied = true; }   // 파티가 스폰된 다음 프레임
                    if (_hpApplied && p.PhaseTime >= _config.IntroTime) StartWave(1, partyPos);
                    break;

                case BattlePhase.Fighting:
                    ApplyEnemyLevels(run);
                    if (p.PhaseTime >= SpawnSettleTime && p.EnemiesAlive == 0)
                    {
                        if (p.Wave < p.TotalWaves)
                        {
                            Enter(BattlePhase.Interlude);
                            CollectLoot(run);        // 떨어진 아이템은 웨이브마다 가방으로
                            BeginAdvance();          // 파티가 오른쪽(안쪽)으로 달려간다
                        }
                        else { Enter(BattlePhase.Victory); CollectLoot(run); run.WinRoom(CollectPartyHp()); }
                    }
                    break;

                case BattlePhase.Interlude:
                {
                    // 달리기 → 어두워짐 → (깜깜할 때) 시작 자리로 옮김 → 밝아짐 → 다음 웨이브
                    float t = p.PhaseTime - _config.AdvanceRunTime;
                    float dark = _config.AdvanceFadeOut, hold = _config.AdvanceHold, light = _config.AdvanceFadeIn;
                    if (t < 0f) { p.Fade = 0f; KeepRunning(); }
                    else if (t < dark) p.Fade = t / dark;
                    else if (t < dark + hold)
                    {
                        p.Fade = 1f;
                        if (!_advanced) { _advanced = true; FinishAdvance(); }
                    }
                    else if (t < dark + hold + light) p.Fade = 1f - (t - dark - hold) / light;
                    else
                    {
                        p.Fade = 0f;
                        Party.TryGetCenter(_world.EntityManager, out var start, out _);
                        StartWave(p.Wave + 1, start);
                    }
                    break;
                }
            }
        }

        void BeginRoom(MapNode node)
        {
            CombatMeter.Reset();   // 딜 미터는 방마다 새로
            _config.Waves.Clear();
            _config.Waves.AddRange(Encounters.ForParty(Encounters.For(node.Type, node.Layer, RunState.Current.Stage), Fighters(RunState.Current)));
            _progress.TotalWaves = _config.Waves.Count;
            _progress.Wave = 0;
            _progress.DeployConfirmed = false;
            _progress.DragSlot = -1;
            Enter(BattlePhase.Deploy);
        }

        void TickDeploy(RunData run)
        {
            if (!_partySpawned) { SpawnParty(run); _partySpawned = true; return; }
            if (!HasPartyMember()) return;                                   // 스폰 대기
            if (!_hpApplied) { ApplyRunToParty(run); _hpApplied = true; }   // 슬롯·덱·체력(스폰된 다음 프레임)
            PlaceParty(run, Formation.MiddleOf(_area), deploying: true);
            if (!_progress.DeployConfirmed) return;

            _progress.DeployConfirmed = false;
            _progress.DragSlot = -1;
            RefreshFormationStats(run);   // 열 보정(공격력·공속)을 정한 자리대로
            Enter(BattlePhase.Intro);
        }

        /// <summary>
        /// 살아 있는 파티원(슬롯 있는)에게 배치 칸 자리·열을 입힌다. deploying이면 그 자리에 바로 세운다(끄는 중인 파티원은 마우스 위치).
        /// </summary>
        void PlaceParty(RunData run, Vec2 middle, bool deploying)
        {
            var em = _world.EntityManager;
            run.EnsureFormation(IsRanged);
            Party.Collect(em, _party);
            int back = 0;
            foreach (var m in _party)
            {
                var e = m.Entity;
                if (m.Slot >= Party.NoSlot || !em.Exists(e) || !em.HasComponent<FormationComponent>(e)) continue;
                int cell = run.CellOf(m.Slot);
                if (cell < 0) continue;
                var anchor = Formation.CellPos(cell, middle, _area);
                ref var f = ref em.GetComponent<FormationComponent>(e);
                f.Active = m.Alive;
                f.Row = Formation.RowOf(cell);
                f.Anchor = anchor;

                if (!deploying && m.Alive) continue;
                // 배치 중: 칸에 바로 세움 / 웨이브 사이 이동 때 쓰러진 파티원: 후열 뒤에 눕혀 둔다
                var pos = m.Alive
                    ? (m.Slot == _progress.DragSlot ? _progress.DragPos : anchor)
                    : new Vec2(middle.X - Formation.ColGap * 2f - 0.8f - back++ * 0.6f, anchor.Y);
                if (_area != null) pos = _area.Clamp(pos);
                ref var tr = ref em.GetComponent<TransformComponent>(e);
                tr.Position = pos;
                tr.Height = 0f;
                tr.Moved = true;
                if (em.HasComponent<KnockbackComponent>(e)) em.GetComponent<KnockbackComponent>(e).Velocity = Vec2.Zero;
            }
        }

        bool IsRanged(string recipe)
            => _world.TryGet(out IntoTheDungeon.Features.Character.CharacterCatalog cat) && cat.Get(recipe).Ranged;

        // 배치를 정한 뒤 열 보정이 들어가도록 능력치를 다시 계산(체력 비율은 유지)
        void RefreshFormationStats(RunData run)
        {
            var em = _world.EntityManager;
            Party.Collect(em, _party);
            foreach (var m in _party)
            {
                if (m.Slot >= Party.NoSlot || !m.Alive) continue;
                ref var st0 = ref em.GetComponent<StatusComponent>(m.Entity);
                float ratio = st0.MaxHp > 0 ? (float)st0.CurrentHp / st0.MaxHp : 1f;
                RecalculatePartyStats(em, m.Entity, run, m.Slot);
                ref var st = ref em.GetComponent<StatusComponent>(m.Entity);
                st.CurrentHp = System.Math.Max(1, (int)System.MathF.Round(st.MaxHp * ratio));
            }
        }

        // 판의 덱(패시브 보정)과 슬롯별 체력 비율을 파티에 입힌다(쓰러졌던 파티원은 다시 쓰러뜨림)
        void ApplyRunToParty(RunData run)
        {
            var em = _world.EntityManager;

            // 슬롯 붙이기: 스폰된 파티원(생긴 순서 = 스폰 순서)에 살아 있는 슬롯 번호를 차례로
            Party.Collect(em, _party);
            int next = 0;
            foreach (var m in _party)
            {
                if (m.Slot < Party.NoSlot || next >= _expectedSlots.Count) continue;
                em.AddComponent(m.Entity, new PartySlotComponent { Slot = _expectedSlots[next++] });
            }
            Party.Collect(em, _party);

            for (int i = 0; i < _party.Count; i++)
            {
                var e = _party[i].Entity;
                int slot = _party[i].Slot;
                var mods = RecalculatePartyStats(em, e, run, slot);
                ref var st = ref em.GetComponent<StatusComponent>(e);
                st.CurrentHp = st.MaxHp;
                if (em.HasComponent<SpecialSkillComponent>(e))
                    em.GetComponent<SpecialSkillComponent>(e).Cooldown *= mods.SkillCooldownMul;
                if (em.HasComponent<BasicSkillComponent>(e))
                    em.GetComponent<BasicSkillComponent>(e).Skill.Cooldown *= mods.SkillCooldownMul;
                if (em.HasComponent<BodyComponent>(e))
                {
                    ref var body = ref em.GetComponent<BodyComponent>(e);
                    body.RadiusAdd += mods.BodyRadiusAdd;
                    body.MassMul *= mods.BodyMassMul;
                }

                if (slot >= run.PartyHp.Count) continue;   // 기록이 없는 슬롯(새로 영입·테스트 소환)은 체력 가득
                st.CurrentHp = System.Math.Max(1, (int)System.MathF.Round(st.MaxHp * run.PartyHp[slot]));
            }
        }

        /// <summary>
        /// 파티원 능력치를 판 기록(레벨·덱·그 파티원 장비)으로 다시 계산한다. 체력·마나는 가득으로 바뀌므로 필요하면 호출한 쪽이 되돌린다.
        /// 방 시작과 전투 중 레벨 업에 쓴다.
        /// </summary>
        public static PartyModifiers RecalculatePartyStats(IEntityManager em, Entity e, RunData run, int slot)
        {
            var mods = run.BuildModifiers(slot < Party.NoSlot ? slot : -1);   // 덱 + 그 파티원 장비
            ref var st = ref em.GetComponent<StatusComponent>(e);

            // 스탯 보너스·레벨로 세부 능력치를 다시 계산(체력·공격력·공속·이속·방어·저항·회복)
            if (em.HasComponent<PrimaryStatsComponent>(e) && em.HasComponent<DerivedStatsComponent>(e))
            {
                ref var ps = ref em.GetComponent<PrimaryStatsComponent>(e);
                ps.Bonus = mods.StatBonus;
                if (slot < Party.NoSlot) ps.Level = run.LevelOf(slot);
                ref var ds = ref em.GetComponent<DerivedStatsComponent>(e);
                StatFormula.Apply(ps.Total, ref st, ref ds);
                TraitComponent.ApplyIfAny(em, e, ref st, ref ds);
                if (em.HasComponent<HitStunComponent>(e)) em.GetComponent<HitStunComponent>(e).Resistance = ds.CcResist;
            }

            // 필살기 저지력 = 기본 + 장비(그 파티원 장비 칸·덱)
            if (em.HasComponent<SpecialSkillComponent>(e))
            {
                ref var sk = ref em.GetComponent<SpecialSkillComponent>(e);
                sk.StopPower = sk.StopPowerBase + mods.StopPowerAdd;
            }

            // 범위 평타: 캐릭터 자체 또는 장비
            if (em.HasComponent<MeleeAttackComponent>(e))
            {
                ref var melee = ref em.GetComponent<MeleeAttackComponent>(e);
                melee.Cleave = melee.CleaveBase || mods.Cleave;
            }

            st.Damage = (int)System.MathF.Round(st.Damage * mods.DamageMul);
            st.MaxHp = (int)System.MathF.Round(st.MaxHp * mods.MaxHpMul);
            st.AttackSpeed *= mods.AttackSpeedMul;
            st.MovementSpeed *= mods.MoveSpeedMul;

            // 배치 열 보정(전열 피해 감소는 HitDetectSystem에서)
            if (em.TryGetComponent<FormationComponent>(e, out var f) && f.Active)
            {
                st.Damage = (int)System.MathF.Round(st.Damage * Formation.DamageMul(f.Row));
                st.AttackSpeed *= Formation.AttackSpeedMul(f.Row);
            }
            return mods;
        }

        static bool IsFallen(RunData run, int slot) => slot < run.PartyHp.Count && run.PartyHp[slot] <= 0f;

        /// <summary>적 레벨(임시값): 1 + 던전 깊이/2(깊이 = (스테이지-1)×층 수 + 층), 정예 +1, 보스 +1. 파티가 평소엔 적보다 조금 높게.</summary>
        static int EnemyLevel(RunData run)
        {
            int lv = 1 + run.Depth / 2;
            if (run.Current != null && run.Current.Type == RoomType.Elite) lv += 1;
            if (run.Current != null && run.Current.Type == RoomType.Boss) lv += 1;
            return lv;
        }

        // 이번 방에 싸우러 나가는 인원(쓰러진 파티원 제외). 몹 마릿수·체력·공격력 보정에 쓴다
        static int Fighters(RunData run)
        {
            int n = 0;
            for (int s = 0; s < run.PartySize; s++)
                if (s >= run.PartyHp.Count || run.PartyHp[s] > 0f) n++;
            return System.Math.Max(1, n);
        }

        // 새로 나온 적에게 이 방의 레벨을 입히고 스탯을 다시 계산
        void ApplyEnemyLevels(RunData run)
        {
            var em = _world.EntityManager;
            _leveled.Clear();
            foreach (var chunk in em.GetChunks(typeof(PrimaryStatsComponent), typeof(TeamComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                    if (teams[i].TeamFlag == TeamFlag.Enemy && !em.HasComponent<EnemyLeveledTag>(entities[i])) _leveled.Add(entities[i]);
            }
            int level = EnemyLevel(run);
            int fighters = Fighters(run);
            NameId bossName = default;
            bool hasBossName = _names != null && _names.TryGetId(Boss.SpawnName, out bossName);
            foreach (var e in _leveled)
            {
                em.AddComponent(e, new EnemyLeveledTag());
                bool boss = hasBossName && em.TryGetComponent<InformationComponent>(e, out var info) && info.NameId.Equals(bossName);
                if (boss) Boss.Promote(em, e);   // 보스: 스탯 보너스·몸집·내려찍기
                ref var ps = ref em.GetComponent<PrimaryStatsComponent>(e);
                ps.Level = level;
                if (!em.HasComponent<DerivedStatsComponent>(e)) continue;
                ref var est = ref em.GetComponent<StatusComponent>(e);
                ref var eds = ref em.GetComponent<DerivedStatsComponent>(e);
                StatFormula.Apply(ps.Total, ref est, ref eds);
                TraitComponent.ApplyIfAny(em, e, ref est, ref eds);   // 몹 특성(단단함·유리 대포 등)
                // 몹 전체 기본 강화(유저: 몹이 너무 약하다)
                est.MaxHp = (int)System.MathF.Round(est.MaxHp * EnemyHpBase);
                est.Damage = (int)System.MathF.Round(est.Damage * EnemyDamageBase);
                // 파티 인원 보정(혼자·둘이면 약하게)
                est.MaxHp = (int)System.MathF.Round(est.MaxHp * Encounters.HpMul(fighters));
                est.Damage = System.Math.Max(1, (int)System.MathF.Round(est.Damage * Encounters.DamageMul(fighters)));
                // 봉인석: 체력·공격력 배율
                est.MaxHp = (int)System.MathF.Round(est.MaxHp * SealStone.EnemyHpMul(run.SealTier));
                est.CurrentHp = est.MaxHp;
                est.Damage = (int)System.MathF.Round(est.Damage * SealStone.EnemyDamageMul(run.SealTier));
                if (boss) Boss.ApplyHp(em, e);
                if (em.HasComponent<HitStunComponent>(e)) em.GetComponent<HitStunComponent>(e).Resistance = em.GetComponent<DerivedStatsComponent>(e).CcResist;
            }
        }

        // 바닥에 떨어진 아이템을 모두 주워 판에 넣는다(정비 화면에서 가방에 배치). 그림은 하나씩 차례로 가방으로 날아간다
        void CollectLoot(RunData run)
        {
            int k = 0;
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(LootDropComponent)))
            {
                var drops = chunk.GetComponentArray<LootDropComponent>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (drops[i].Collected || drops[i].Collecting) continue;
                    drops[i].Collecting = true;
                    drops[i].CollectAge = 0f;
                    drops[i].CollectDelay = 0.5f + 0.15f * k++;   // 임시값
                    var item = ItemPool.Get(drops[i].ItemIndex);
                    if (item != null) run.Loot.Add(item);
                }
            }
        }

        // 슬롯별 체력 비율. 이번 방에 안 나온(쓰러져 있던) 슬롯은 0 그대로
        System.Collections.Generic.List<float> CollectPartyHp()
        {
            var run = RunState.Current;
            Party.Collect(_world.EntityManager, _party);
            var hp = new System.Collections.Generic.List<float>(run.PartySize);
            for (int s = 0; s < run.PartySize; s++)
                hp.Add(Party.TryGetSlot(_party, s, out var m) && m.Alive && m.MaxHp > 0 ? (float)m.Hp / m.MaxHp : 0f);
            return hp;
        }

        void Enter(BattlePhase phase)
        {
            _progress.Phase = phase;
            _progress.PhaseTime = 0f;
            _progress.Fade = 0f;
            _advanced = false;
        }

        // 웨이브를 깨면 살아 있는 파티원이 AI를 멈추고 오른쪽(던전 안쪽)으로 달린다
        void BeginAdvance()
        {
            var em = _world.EntityManager;
            Party.Collect(em, _party);
            foreach (var m in _party)
            {
                if (!m.Alive || !em.Exists(m.Entity)) continue;
                if (em.HasComponent<CombatAIComponent>(m.Entity)) em.GetComponent<CombatAIComponent>(m.Entity).Enabled = false;
                if (em.HasComponent<CharacterIntentBuffer>(m.Entity)) em.GetComponent<CharacterIntentBuffer>(m.Entity).Add(CharacterIntent.Move(new Vec2(1f, 0f)));
            }
        }

        // 웨이브가 끝나는 순간 휘두르거나 스킬을 쓰던 파티원은 달리라는 의도를 놓칠 수 있어, 멈춰 있으면 다시 넣는다
        void KeepRunning()
        {
            var em = _world.EntityManager;
            Party.Collect(em, _party);
            foreach (var m in _party)
            {
                var e = m.Entity;
                if (!m.Alive || !em.Exists(e) || !em.TryGetComponent<IntoTheDungeon.Features.State.StateComponent>(e, out var st)) continue;
                if (st.Current.Movement != IntoTheDungeon.Core.Abstractions.Types.MovementState.Idle) continue;
                if (st.Current.Action is IntoTheDungeon.Core.Abstractions.Types.ActionState.Attack or IntoTheDungeon.Core.Abstractions.Types.ActionState.Skill) continue;   // 끝나면 다음 프레임에
                if (em.HasComponent<CombatAIComponent>(e)) em.GetComponent<CombatAIComponent>(e).Enabled = false;
                if (em.HasComponent<CharacterIntentBuffer>(e)) em.GetComponent<CharacterIntentBuffer>(e).Add(CharacterIntent.Move(new Vec2(1f, 0f)));
            }
        }

        // 화면이 깜깜할 때: 남은 적 시체를 치우고, 파티를 왼쪽 시작 자리로 옮겨 세운 뒤 AI를 다시 켠다(새 구역에 들어선 것처럼)
        void FinishAdvance()
        {
            var em = _world.EntityManager;
            _leveled.Clear();
            foreach (var chunk in em.GetChunks(typeof(StatusComponent), typeof(TeamComponent)))
            {
                var statuses = chunk.GetComponentArray<StatusComponent>();
                var teams = chunk.GetComponentArray<TeamComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                    if (!statuses[i].IsAlive && teams[i].TeamFlag != Party.Team) _leveled.Add(entities[i]);
            }
            foreach (var e in _leveled) { _viewOps?.Enqueue(e); em.DestroyEntity(e); }

            Party.Collect(em, _party);
            var middle = Formation.MiddleOf(_area);
            // 마지막 구역(문이 있는 곳)은 문 가까이에서 시작한다(방이 넓어 왼쪽 끝이면 한참 걸어가야 하므로)
            if (_progress.Wave + 1 == _progress.TotalWaves && _area is RectWalkArea ra)
                middle = new Vec2(System.MathF.Max(middle.X, DoorPosition(ra).X - 11f + Formation.ColGap), middle.Y);
            var run = RunState.Current;
            PlaceParty(run, middle, deploying: false);   // 자리(Anchor) 갱신 + 쓰러진 파티원은 뒤에 눕힘
            foreach (var m in _party)
            {
                var e = m.Entity;
                if (!em.Exists(e) || !em.HasComponent<TransformComponent>(e)) continue;
                // 살아 있으면 배치 칸 그대로(슬롯 없는 파티원은 중열 가운데 근처)
                if (!m.Alive) continue;
                Vec2 pos = em.TryGetComponent<FormationComponent>(e, out var fc) && fc.Active ? fc.Anchor : new Vec2(middle.X, RandomDepth(middle.Y));
                if (_area != null) pos = _area.Clamp(pos);
                ref var tr = ref em.GetComponent<TransformComponent>(e);
                tr.Position = pos;
                tr.Height = 0f;
                tr.Moved = true;
                if (em.HasComponent<KnockbackComponent>(e)) em.GetComponent<KnockbackComponent>(e).Velocity = Vec2.Zero;
                if (em.HasComponent<CharacterIntentBuffer>(e))
                {
                    ref var buf = ref em.GetComponent<CharacterIntentBuffer>(e);
                    buf.Add(CharacterIntent.Stop());
                    buf.Add(CharacterIntent.Turn(Facing2D.Right));
                }
                if (em.HasComponent<CombatAIComponent>(e)) em.GetComponent<CombatAIComponent>(e).Enabled = true;
            }
        }

        void StartWave(int wave, Vec2 partyPos)
        {
            _progress.Wave = wave;
            Enter(BattlePhase.Fighting);
            if (wave < 1 || wave > _config.Waves.Count) return;

            // 플레이어 앞(오른쪽)에 줄지어, 오른쪽 벽에 가까우면 왼쪽에
            float side = 1f;
            if (_area is RectWalkArea r && partyPos.X + _config.SpawnDistance + 2f > r.MaxX) side = -1f;

            bool fromDoor = wave == _config.Waves.Count && _area is RectWalkArea;   // 마지막 웨이브: 문 앞에서
            int n = 0;
            foreach (var s in _config.Waves[wave - 1])
            {
                if (!_recipes.TryGetId(s.Recipe, out var id)) continue;
                for (int k = 0; k < s.Count; k++, n++)
                {
                    var pos = fromDoor
                        ? DoorSpawn(n)
                        : new Vec2(partyPos.X + side * (_config.SpawnDistance + n * 0.7f), RandomDepth(partyPos.Y));
                    Enqueue(id, s.Boss ? Boss.SpawnName : s.Recipe, pos, side < 0 ? 1f : -1f, TeamFlag.Enemy);
                }
            }
        }

        // 씬에 놓인 첫 파티원 뒤로, 판에 기록된 동료들을 슬롯 순서대로 스폰한다
        // 판 기록대로 파티 전원(슬롯 0번부터)을 방 왼쪽 시작 위치에 줄지어 스폰. 쓰러진 슬롯은 스폰하지 않는다
        void SpawnParty(RunData run)
        {
            _expectedSlots.Clear();
            run.EnsureFormation(IsRanged);
            var middle = Formation.MiddleOf(_area);
            for (int slot = 0; slot < run.Members.Count; slot++)
            {
                if (IsFallen(run, slot) || !_recipes.TryGetId(run.Members[slot], out var id)) continue;
                var pos = Formation.CellPos(run.CellOf(slot), middle, _area);   // 배치 칸
                Enqueue(id, "Party", pos, 1f, Party.Team);
                _expectedSlots.Add(slot);
            }
        }

        // 파티 시작 위치: 걷는 영역 왼쪽에서 조금 안쪽, 깊이 가운데
        Vec2 StartPosition()
        {
            if (_area is RectWalkArea r) return new Vec2(r.MinX + PartyStartInset, (r.MinDepth + r.MaxDepth) * 0.5f);
            return Vec2.Zero;
        }

        /// <summary>마지막 웨이브의 문(오른쪽 벽 안쪽) 바닥 위치. DoorRenderer도 같은 자리에 문을 그린다.</summary>
        public static Vec2 DoorPosition(RectWalkArea r) => new(r.MaxX - 1.2f, r.MaxDepth);

        // 문 앞에서 부채꼴로 퍼져 나온다
        Vec2 DoorSpawn(int n)
        {
            var r = (RectWalkArea)_area;
            var door = DoorPosition(r);
            float spread = r.MaxDepth - r.MinDepth;
            return new Vec2(door.X - 0.4f - (n % 3) * 0.8f - (n / 3) * 0.5f, door.Y - 0.2f - ((n * 0.37f) % 1f) * spread * 0.7f);
        }

        float RandomDepth(float fallback)
        {
            if (_area is RectWalkArea r) return r.MinDepth + (float)_rng.NextDouble() * (r.MaxDepth - r.MinDepth);
            return fallback + ((float)_rng.NextDouble() - 0.5f);
        }

        void Enqueue(RecipeId id, string name, Vec2 pos, float dirX, TeamFlag team)
        {
            if (_area != null) pos = _area.Clamp(pos);
            _spawnQueue.Enqueue(new SpawnOrder(id, new SpawnSpec
            {
                PhysHandle = new(-1, 0),
                Name = name,
                Pos = pos,
                Dir = new Vec2(dirX, 0f),
                TeamFlag = team
            }, SpawnSource.System));
        }

        int CountEnemies()
        {
            int n = 0;
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(TeamComponent), typeof(StatusComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                var statuses = chunk.GetComponentArray<StatusComponent>();
                for (int i = 0; i < chunk.Count; i++)
                    if (teams[i].TeamFlag == TeamFlag.Enemy && statuses[i].IsAlive) n++;
            }
            return n;
        }

        bool HasPartyMember()
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(TeamComponent), typeof(StatusComponent)))
            {
                var teams = chunk.GetComponentArray<TeamComponent>();
                for (int i = 0; i < chunk.Count; i++)
                    if (teams[i].TeamFlag == Party.Team) return true;
            }
            return false;
        }
    }
}
