using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Physics.Systems
{
    /// <summary>
    /// 넉백 속도로 바닥 평면 위치를 옮기고 지수적으로 줄인다. 걷는 영역 밖으로는 밀려나지 않는다.
    /// </summary>
    public sealed class KnockbackSystem : GameSystem, ITick
    {
        IWalkArea _area;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            world.TryGet(out _area);
        }

        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(TransformComponent), typeof(KnockbackComponent)))
            {
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var knocks = chunk.GetComponentArray<KnockbackComponent>();

                for (int i = 0; i < chunk.Count; i++)
                {
                    ref var k = ref knocks[i];
                    if (k.Velocity == Vec2.Zero) continue;

                    ref var t = ref transforms[i];
                    var p = t.Position + k.Velocity * dt;
                    if (_area != null) p = _area.Clamp(p);
                    t.Position = p;
                    t.Moved = true;

                    k.Velocity = k.Velocity * System.MathF.Exp(-k.Damping * dt);   // 프레임레이트와 무관한 감쇠
                    if (k.Velocity.SqrMagnitude < 0.0004f) k.Velocity = Vec2.Zero;
                }
            }
        }
    }
}
