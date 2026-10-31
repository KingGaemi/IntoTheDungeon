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

namespace IntoTheDungeon.Features.Battle
{
    /// <summary>
    /// 전투 한 판: 시작 연출 → 웨이브 스폰 → 적 전멸이면 다음 웨이브 → 마지막까지 깨면 승리.
    /// 파티가 전멸하면 패배. 몹은 파티 중심 앞쪽(벽에 막히면 반대쪽)에 나온다.
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
        bool _alliesSpawned;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            Enabled = world.TryGet(out _progress) && world.TryGet(out _config)
                   && world.TryGet(out _spawnQueue) && world.TryGet(out _recipes);
            world.TryGet(out _area);
            if (Enabled) _progress.TotalWaves = _config.Waves.Count;
        }

        public void Tick(float dt)
        {
            var p = _progress;
            p.PhaseTime += dt;

            p.EnemiesAlive = CountEnemies();
            bool partyFound = HasPartyMember();
            Party.TryGetCenter(_world.EntityManager, out var partyPos, out int partyAlive);
            if (!partyFound || p.IsOver) return;   // 파티가 아직 스폰 전이면 기다린다

            if (partyAlive == 0) { Enter(BattlePhase.Defeat); return; }

            switch (p.Phase)
            {
                case BattlePhase.Intro:
                    if (!_alliesSpawned) { SpawnAllies(partyPos); _alliesSpawned = true; }
                    if (p.PhaseTime >= _config.IntroTime) StartWave(1, partyPos);
                    break;

                case BattlePhase.Fighting:
                    if (p.PhaseTime >= SpawnSettleTime && p.EnemiesAlive == 0)
                        Enter(p.Wave < p.TotalWaves ? BattlePhase.Interlude : BattlePhase.Victory);
                    break;

                case BattlePhase.Interlude:
                    if (p.PhaseTime >= _config.InterludeTime) StartWave(p.Wave + 1, partyPos);
                    break;
            }
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

        void SpawnAllies(Vec2 partyPos)
        {
            if (_config.StartingAllies <= 0 || !_recipes.TryGetId(_config.AllyRecipe, out var id)) return;
            for (int k = 0; k < _config.StartingAllies; k++)
            {
                var pos = new Vec2(partyPos.X - 1.2f - k * 0.8f, RandomDepth(partyPos.Y));   // 플레이어 뒤쪽
                Enqueue(id, "Ally", pos, 1f, TeamFlag.Player);
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
