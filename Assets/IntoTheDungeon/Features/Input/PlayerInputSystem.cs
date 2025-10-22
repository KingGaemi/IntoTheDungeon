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

        }
        public void Tick(float dt)
        {
            Now += dt;
            if (_input == null) return;

            bool noChange = !_input.AttackDown && !_input.AttackUp &&
                            !_input.AxisChanged && !_input.AttackHeld;

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
                typeof(SpawnOutbox)))
            {
                // var ents = chunk.GetEntities();
                var buffers = chunk.GetComponentArray<CharacterIntentBuffer>();
                var outBoxes = chunk.GetComponentArray<SpawnOutbox>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var entities = chunk.GetEntities();
                for (int i = 0; i < chunk.Count; i++)
                {
                    processed = true;


                    ref var buf = ref buffers[i]; // ref로 직접 수정 → 복사 안 생김
                    var e = entities[i];

                    if (_input.AxisChanged && axis != Vec2.Zero)
                    {
                        buf.Add(CharacterIntent.Move(Facing2DExt.FromX(axis.X)));
                        // _logger.Log("Vertical");
                    }
                    else if (_input.AxisChanged && axis == Vec2.Zero) // ← 0으로 "변한" 프레임에만
                    {
                        buf.Add(CharacterIntent.Stop()); // 또는 CharacterIntent.None()
                    }

                    if (_input.AttackDown || _input.AttackHeld)
                    {
                        buf.Add(CharacterIntent.Attack());
                        // _logger.Log("Attack");
                    }
                    if (_input.E_Down)
                    {
                        ref var trans = ref transforms[i];
                        var order = new SpawnOrder(
                            RecipeIds.Character,
                            new SpawnSpec
                            {
                                // handle
                                // name
                                Pos = trans.Position,
                                Dir = trans.Direction
                            }
                            , SpawnSource.Entity,
                            e
                        );
                        outBoxes[i].Set(in order);
                    }

                }
            }

            _lastSeq = _input.Seq;
            _input.ConsumeFrame();


            if (!processed && noChange) _logger?.Warn("Input frame passed without consumers.");
        }

    }
}

