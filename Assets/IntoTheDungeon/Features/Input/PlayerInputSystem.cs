using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Messages;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Command;

namespace IntoTheDungeon.Features.Input
{
    public sealed class PlayerInputSystem : GameSystem, ITick
    {
        ILogger logger;
        IInputService _input;
        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            if (!world.TryGet(out logger))
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

            if (_input == null) return;
            // 입력은 한 번만 읽기
            var axis = _input.MoveAxis();
            bool atk = _input.AttackPressed();

            foreach (var chunk in _world.EntityManager.GetChunks(
                typeof(PlayerTag),
                typeof(CharacterIntentBuffer)))
            {
                // var ents = chunk.GetEntities();
                var buffers = chunk.GetComponentArray<CharacterIntentBuffer>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var buf = ref buffers[i]; // ref로 직접 수정 → 복사 안 생김
                    if (axis != Vec2.Zero)
                    {
                        buf.Add(CharacterIntent.Move(Facing2DExt.FromX(axis.X)));
                    }
                    if (atk)
                    {
                        buf.Add(CharacterIntent.Attack());
                    }

                }
            }
        }
    }

}