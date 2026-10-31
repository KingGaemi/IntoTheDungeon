using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.Types;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Spawning;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Command;
using IntoTheDungeon.Features.AI;
using IntoTheDungeon.Features.Attack;

namespace IntoTheDungeon.Features.Input
{
    public sealed class PlayerInputSystem : GameSystem, ITick
    {
        // float _atkNextTime = 0f;
        const float _atkInitialDelay = 0.15f; // 홀드 후 첫 지연
        const float _atkRepeatHz = 5f;        // 초당 5발
        float Now; // 누적 시간, Tick에서 증가
        ILogger _logger;
        IInputService _input;
        uint _lastSeq;
        static readonly string[] OrcRecipeNames = { "Orc1", "Orc2", "Orc3" };
        RecipeId[] _orcRecipeIds; // R키 스폰용(누를 때마다 Orc1→2→3 순서) - Initialize에서 한 번만 캐싱
        int _nextOrc;
        RecipeId _allyRecipeId; // T키 동료 소환용
        const string AllyRecipeName = "Catherine";   // 동료로 소환할 레시피(팀만 Player로 바꿔 스폰)
        const float ManualPause = 1.5f;              // 자동전투 중 직접 조작하면 이 시간 동안 AI를 쉰다
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out _logger))
            {
                Enabled = false;

            }
            if (!world.TryGet(out _input))
            {
                Enabled = false;
                return;
            }
            if (!world.TryGet(out IRecipeTable recipeTable))
            {
                Enabled = false;
                return;
            }
            _orcRecipeIds = new RecipeId[OrcRecipeNames.Length];
            for (int k = 0; k < OrcRecipeNames.Length; k++)
                _orcRecipeIds[k] = recipeTable.GetId(OrcRecipeNames[k]);
            _allyRecipeId = recipeTable.GetId(AllyRecipeName);
        }
        public void Tick(float dt)
        {
            Now += dt;
            if (_input == null) return;

            bool noChange = !_input.AttackDown && !_input.AttackUp &&
                            !_input.AxisChanged && !_input.AttackHeld && !_input.JumpDown &&
                            !_input.AutoToggleDown && !_input.AllySpawnDown && !_input.SkillDown;

            // 자동전투 중이라도 방향키를 누르고 있는 동안은 직접 조작이 우선
            if (_input.Move != Vec2.Zero || _input.AttackHeld || _input.JumpDown)
                PauseAutoBattle();

            if (_input.Seq == _lastSeq && noChange)
            {
                _input.ConsumeFrame(); // 프레임 소비는 여기서 한 번
                return;
            }


            var processed = false;
            var axis = _input.Move;


            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(PlayerTag),
                typeof(CharacterIntentBuffer),
                typeof(TransformComponent),
                typeof(SpawnOutbox),
                typeof(CombatAIComponent),
                typeof(SpecialSkillComponent)))
            {
                // var ents = chunk.GetEntities();
                var buffers = chunk.GetComponentArray<CharacterIntentBuffer>();
                var outBoxes = chunk.GetComponentArray<SpawnOutbox>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var ais = chunk.GetComponentArray<CombatAIComponent>();
                var skills = chunk.GetComponentArray<SpecialSkillComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    processed = true;


                    ref var buf = ref buffers[i]; // ref로 직접 수정 → 복사 안 생김
                    var e = entities[i];

                    if (_input.AxisChanged && axis != Vec2.Zero)
                    {
                        buf.Add(CharacterIntent.Move(axis));   // 8방향: 가로·깊이 축 그대로
                        // _logger.Log("Vertical");
                    }
                    else if (_input.AxisChanged && axis == Vec2.Zero) // ← 0으로 "변한" 프레임에만
                    {
                        buf.Add(CharacterIntent.Stop()); // 또는 CharacterIntent.None()
                    }

                    if (_input.JumpDown)
                        buf.Add(CharacterIntent.Jump());

                    // 필살기는 자동전투 중에도 직접 누르는 것(타이밍이 재미). 실제 시작은 SpecialSkillSystem
                    if (_input.SkillDown)
                        skills[i].Request();

                    if (_input.AutoToggleDown)
                    {
                        ref var ai = ref ais[i];
                        ai.Enabled = !ai.Enabled;
                        ai.State = CombatAIState.Idle;
                        ai.PauseTimer = 0f;
                        if (!ai.Enabled && axis == Vec2.Zero)
                            buf.Add(CharacterIntent.Stop());   // AI가 걷게 해 둔 채로 꺼지지 않게
                        _logger?.Log($"[자동전투] {(ai.Enabled ? "켜짐" : "꺼짐")}");
                    }

                    if (_input.AttackDown || _input.AttackHeld)
                    {
                        buf.Add(CharacterIntent.Attack());
                        // _logger.Log("Attack");
                    }
                    if (_input.E_Down)
                    {
                        // ref var trans = ref transforms[i];
                        // var order = new SpawnOrder(
                        //     RecipeIds.Character,
                        //     new SpawnSpec
                        //     {
                        //         PhysHandle = new(-1, 0),// handle
                        //         // name
                        //         Pos = trans.Position,
                        //         Dir = trans.Direction
                        //     }
                        //     , SpawnSource.Entity,
                        //     e
                        // );
                        // // _logger.Log($"position {trans.Position}, direction{trans.Direction}");
                        // outBoxes[i].Set(in order);
                    }
                    if (_input.AllySpawnDown)
                    {
                        ref var trans = ref transforms[i];
                        var order = new SpawnOrder(
                            _allyRecipeId,
                            new SpawnSpec
                            {
                                PhysHandle = new(-1, 0),
                                Pos = new Vec2(trans.Position.X - 1.5f, trans.Position.Y),   // 플레이어 뒤쪽
                                Dir = trans.Direction,
                                Name = "Ally",
                                TeamFlag = TeamFlag.Player   // 같은 팀 → 적을 공격하고 적이 없으면 플레이어를 따라온다
                            },
                            SpawnSource.Entity,
                            e
                        );
                        outBoxes[i].Set(in order);
                    }
                    if (_input.R_Down)
                    {
                        ref var trans = ref transforms[i];
                        int orc = _nextOrc;
                        _nextOrc = (_nextOrc + 1) % _orcRecipeIds.Length;
                        var order = new SpawnOrder(
                            _orcRecipeIds[orc],
                            new SpawnSpec
                            {
                                PhysHandle = new(-1, 0),// handle
                                // name
                                Pos = new Vec2(trans.Position.X + 5, trans.Position.Y),
                                Dir = trans.Direction,
                                Name = OrcRecipeNames[orc],
                                TeamFlag = TeamFlag.Enemy

                            }
                            , SpawnSource.Entity,
                            e
                        );
                        // _logger.Log($"position {trans.Position}, direction{trans.Direction}");
                        outBoxes[i].Set(in order);
                    }


                }
            }

            _lastSeq = _input.Seq;
            _input.ConsumeFrame();


            if (!processed && noChange) _logger?.Warn("Input frame passed without consumers.");
        }

        void PauseAutoBattle()
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(PlayerTag), typeof(CombatAIComponent)))
            {
                var ais = chunk.GetComponentArray<CombatAIComponent>();
                for (int i = 0; i < chunk.Count; i++)
                    if (ais[i].Enabled) ais[i].PauseTimer = ManualPause;
            }
        }

    }
}

