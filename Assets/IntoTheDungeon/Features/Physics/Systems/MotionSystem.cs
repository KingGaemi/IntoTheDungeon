using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Physics.Systems
{
    /// <summary>
    /// 2.5D 이동: 물리 엔진 없이 KinematicComponent 속도로 바닥 평면 위치를 직접 옮긴다.
    /// 물리 바디가 있는 엔티티(PhysicsBodyRef)는 기존 물리 경로(KinematicSystem)가 맡으므로 건너뛴다.
    /// 걸을 수 있는 영역(IWalkArea)이 등록돼 있으면 그 안으로 제한한다.
    /// </summary>
    public sealed class MotionSystem : GameSystem, ITick
    {
        IWalkArea _area;

        public override void Initialize(IWorld world)
        {
            base.Initialize(world);
            world.TryGet(out _area);
        }

        public void Tick(float dt)
        {
            var em = _world.EntityManager;
            foreach (var chunk in em.GetChunks(typeof(TransformComponent), typeof(KinematicComponent)))
            {
                var transforms = chunk.GetComponentArray<TransformComponent>();
                var kinematics = chunk.GetComponentArray<KinematicComponent>();
                var entities = chunk.GetEntities();

                for (int i = 0; i < chunk.Count; i++)
                {
                    if (em.HasComponent<PhysicsBodyRef>(entities[i])) continue;

                    var v = kinematics[i].Velocity;
                    if (v == Vec2.Zero) continue;

                    ref var t = ref transforms[i];
                    var p = t.Position + v * dt;
                    if (_area != null) p = _area.Clamp(p);
                    t.Position = p;
                    t.Moved = true;
                }
            }
        }
    }
}
