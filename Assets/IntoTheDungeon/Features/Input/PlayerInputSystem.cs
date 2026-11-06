using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Features.Input
{
    /// <summary>
    /// 플레이어 입력: 이 게임은 파티 전원이 자동전투하고, 플레이어는 파티원별 필살기만 누른다(버튼 또는 숫자키 1~4).
    /// 테스트용으로 R(오크 소환, Orc1→2→3 순환)·T(동료 소환, 케서린↔궁수 번갈아)도 받는다. 소환 위치는 파티 중심 기준.
    /// </summary>
    public sealed class PlayerInputSystem : GameSystem, ITick
    {
        ILogger _logger;
        IInputService _input;
        ISystemSpawnQueue _spawnQueue;
        IWalkArea _area;
        static readonly string[] OrcRecipeNames = { "Orc1", "Orc2", "Orc3", "Plant1", "Plant2", "Plant3", "Bat1", "Spider1", "Shaman1", "EggSac1" };
        RecipeId[] _orcRecipeIds; // R키 스폰용 - Initialize에서 한 번만 캐싱
        int _nextOrc;
        static readonly string[] AllyRecipeNames = { "Catherine", "Archer", "Warrior" };   // T키 동료 소환(팀만 Player로 바꿔 스폰)
        RecipeId[] _allyRecipeIds;
        int _nextAlly;
        readonly List<Party.Member> _party = new(8);

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            world.TryGet(out _logger);
            world.TryGet(out _area);
            if (!world.TryGet(out _input) || !world.TryGet(out _spawnQueue) || !world.TryGet(out IRecipeTable recipeTable))
            {
                Enabled = false;
                return;
            }
            _orcRecipeIds = new RecipeId[OrcRecipeNames.Length];
            for (int k = 0; k < OrcRecipeNames.Length; k++)
                _orcRecipeIds[k] = recipeTable.GetId(OrcRecipeNames[k]);
            _allyRecipeIds = new RecipeId[AllyRecipeNames.Length];
            for (int k = 0; k < AllyRecipeNames.Length; k++)
                _allyRecipeIds[k] = recipeTable.GetId(AllyRecipeNames[k]);
        }

        public void Tick(float dt)
        {
            if (_input == null) return;
            var em = _world.EntityManager;

            int slot = _input.SkillSlot;
            bool deploying = _world.TryGet(out BattleProgress bp) && bp.Phase == BattlePhase.Deploy;   // 배치 중엔 필살기 안 나감
            if (slot >= 0 && !deploying)
            {
                Party.Collect(em, _party);
                if (Party.TryGetSlot(_party, slot, out var m) && m.Alive && em.HasComponent<SpecialSkillComponent>(m.Entity))
                    em.GetComponent<SpecialSkillComponent>(m.Entity).Request();
            }

            if (_input.R_Down || _input.AllySpawnDown)
            {
                Party.TryGetCenter(em, out var center, out _);
                if (_input.R_Down)
                {
                    int orc = _nextOrc;
                    _nextOrc = (_nextOrc + 1) % _orcRecipeIds.Length;
                    Spawn(_orcRecipeIds[orc], OrcRecipeNames[orc], new Vec2(center.X + 5f, center.Y), TeamFlag.Enemy);
                }
                if (_input.AllySpawnDown)
                {
                    int ally = _nextAlly;
                    _nextAlly = (_nextAlly + 1) % _allyRecipeIds.Length;
                    Spawn(_allyRecipeIds[ally], AllyRecipeNames[ally], new Vec2(center.X - 1.5f, center.Y), Party.Team);
                }
            }

            _input.ConsumeFrame();
        }

        void Spawn(RecipeId id, string name, Vec2 pos, TeamFlag team)
        {
            if (_area != null) pos = _area.Clamp(pos);
            _spawnQueue.Enqueue(new SpawnOrder(id, new SpawnSpec
            {
                PhysHandle = new(-1, 0),
                Name = name,
                Pos = pos,
                Dir = new Vec2(1f, 0f),
                TeamFlag = team
            }, SpawnSource.System));
        }
    }
}
