using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Systems;

namespace IntoTheDungeon.Features.Physics.Systems
{
    /// <summary>
    /// 높이(Z) 운동: 중력으로 VelocityZ를 줄이고 Height를 갱신한다. 바닥(0)에 닿으면 착지.
    /// 점프·띄우기는 VelocityZ를 위로 주기만 하면 된다.
    /// </summary>
    public sealed class HeightMotionSystem : GameSystem, ITick
    {
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(TransformComponent), typeof(HeightMotionComponent)))
            {
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var motions = chunk.GetComponentArray<HeightMotionComponent>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var t = ref transforms[i];
                    ref var m = ref motions[i];
                    if (!m.IsAirborne(t.Height)) continue;

                    m.VelocityZ -= m.Gravity * dt;
                    t.Height += m.VelocityZ * dt;
                    if (t.Height <= 0f)
                    {
                        t.Height = 0f;
                        m.VelocityZ = 0f;
                    }
                    t.Moved = true;
                }
            }
        }
    }
}
