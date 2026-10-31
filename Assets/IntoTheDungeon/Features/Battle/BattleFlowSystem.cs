using IntoTheDungeon.Core.ECS.Systems;
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

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>
    /// 방 하나의 전투: 지도에서 방을 고르면(RunState) 그 방의 몹 구성으로 시작 연출 → 웨이브 스폰 →
    /// 적 전멸이면 다음 웨이브 → 마지막까지 깨면 승리(파티 체력을 판에 기록). 파티가 전멸하면 패배(판 끝).
    /// 파티 체력은 방과 방 사이에 이어진다. 몹은 파티 중심 앞쪽(벽에 막히면 반대쪽)에 나온다.
    /// SpawnSystem보다 먼저 돌아야 같은 프레임에 스폰된다.
    /// </summary>
    public sealed class BattleFlowSystem : GameSystem, ITick
    {
        const float SpawnSettleTime = 1f;   // 스폰 직후엔 아직 엔티티가 없을 수 있어 이 시간 뒤부터 전멸 판정

        BattleProgress _progress;
        BattleConfig _config;
        ISystemSpawnQueue _spawnQueue;
        IRecipeTable _recipes;
        IWalkArea _area;
        readonly System.Random _rng = new(7);
        bool _alliesSpawned, _hpApplied;
        readonly System.Collections.Generic.List<Party.Member> _party = new(8);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _progress) && world.TryGet(out _config)
                   && world.TryGet(out _spawnQueue) && world.TryGet(out _recipes);
            world.TryGet(out _area);
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

            bool partyFound = HasPartyMember();
            Party.TryGetCenter(_world.EntityManager, out var partyPos, out int partyAlive);
            if (!partyFound || p.IsOver) return;   // 파티가 아직 스폰 전이면 기다린다

            if (partyAlive == 0 && _hpApplied) { Enter(BattlePhase.Defeat); run.Lose(); return; }

            switch (p.Phase)
            {
                case BattlePhase.Intro:
                    if (!_alliesSpawned) { SpawnAllies(run, partyPos); _alliesSpawned = true; }
                    else if (!_hpApplied) { ApplyRunToParty(run); _hpApplied = true; }   // 동료가 스폰된 다음 프레임
                    if (_hpApplied && p.PhaseTime >= _config.IntroTime) StartWave(1, partyPos);
                    break;

                case BattlePhase.Fighting:
                    if (p.PhaseTime >= SpawnSettleTime && p.EnemiesAlive == 0)
                    {
                        if (p.Wave < p.TotalWaves) Enter(BattlePhase.Interlude);
                        else { Enter(BattlePhase.Victory); run.WinRoom(CollectPartyHp()); }
                    }
                    break;

                case BattlePhase.Interlude:
                    if (p.PhaseTime >= _config.InterludeTime) StartWave(p.Wave + 1, partyPos);
                    break;
            }
        }

        void BeginRoom(MapNode node)
        {
            _config.Waves.Clear();
            _config.Waves.AddRange(Encounters.For(node.Type, node.Layer));
            _progress.TotalWaves = _config.Waves.Count;
            _progress.Wave = 0;
            Enter(BattlePhase.Intro);
        }

        // 판의 덱(패시브 보정)과 슬롯별 체력 비율을 파티에 입힌다(쓰러졌던 파티원은 다시 쓰러뜨림)
        void ApplyRunToParty(RunData run)
        {
            var em = _world.EntityManager;
            var mods = run.BuildModifiers();
            Party.Collect(em, _party);
            for (int i = 0; i < _party.Count; i++)
            {
                var e = _party[i].Entity;
                ref var st = ref em.GetComponent<StatusComponent>(e);

                st.Damage = (int)System.MathF.Round(st.Damage * mods.DamageMul);
                st.MaxHp = (int)System.MathF.Round(st.MaxHp * mods.MaxHpMul);
                st.CurrentHp = st.MaxHp;
                st.AttackSpeed *= mods.AttackSpeedMul;
                st.MovementSpeed *= mods.MoveSpeedMul;
                if (em.HasComponent<SpecialSkillComponent>(e))
                    em.GetComponent<SpecialSkillComponent>(e).Cooldown *= mods.SkillCooldownMul;
                if (em.HasComponent<BodyComponent>(e))
                {
                    ref var body = ref em.GetComponent<BodyComponent>(e);
                    body.RadiusAdd += mods.BodyRadiusAdd;
                    body.MassMul *= mods.BodyMassMul;
                }

                if (i >= run.PartyHp.Count) continue;   // 기록이 없는 슬롯(새로 영입 등)은 체력 가득
                float ratio = run.PartyHp[i];
                if (ratio <= 0f)
                    em.GetComponent<HpModificationQueue>(e).Enqueue(HpModification.Damage(st.MaxHp * 10, e));
                else
                    st.CurrentHp = System.Math.Max(1, (int)System.MathF.Round(st.MaxHp * ratio));
            }
        }

        System.Collections.Generic.List<float> CollectPartyHp()
        {
            Party.Collect(_world.EntityManager, _party);
            var hp = new System.Collections.Generic.List<float>(_party.Count);
            foreach (var m in _party)
                hp.Add(m.Alive && m.MaxHp > 0 ? (float)m.Hp / m.MaxHp : 0f);
            return hp;
        }

        void Enter(BattlePhase phase)
        {
            _progress.Phase = phase;
            _progress.PhaseTime = 0f;
        }

        void StartWave(int wave, Vec2 partyPos)
        {
            _progress.Wave = wave;
            Enter(BattlePhase.Fighting);
            if (wave < 1 || wave > _config.Waves.Count) return;

            // 플레이어 앞(오른쪽)에 줄지어, 오른쪽 벽에 가까우면 왼쪽에
            float side = 1f;
            if (_area is RectWalkArea r && partyPos.X + _config.SpawnDistance + 2f > r.MaxX) side = -1f;

            int n = 0;
            foreach (var s in _config.Waves[wave - 1])
            {
                if (!_recipes.TryGetId(s.Recipe, out var id)) continue;
                for (int k = 0; k < s.Count; k++, n++)
                {
                    var pos = new Vec2(partyPos.X + side * (_config.SpawnDistance + n * 0.7f), RandomDepth(partyPos.Y));
                    Enqueue(id, s.Recipe, pos, side < 0 ? 1f : -1f, TeamFlag.Enemy);
                }
            }
        }

        // 씬에 놓인 첫 파티원 뒤로, 판에 기록된 동료들을 슬롯 순서대로 스폰한다
        void SpawnAllies(RunData run, Vec2 partyPos)
        {
            for (int k = 0; k < run.Allies.Count; k++)
            {
                if (!_recipes.TryGetId(run.Allies[k], out var id)) continue;
                var pos = new Vec2(partyPos.X - 1.2f - k * 0.8f, RandomDepth(partyPos.Y));   // 뒤쪽에 줄지어
                Enqueue(id, "Ally", pos, 1f, Party.Team);
            }
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
