using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;

namespace IntoTheDungeon.Features.Attack
{
    /// <summary>
    /// 날아가는 히트박스를 속도만큼 옮긴다. 판정(HitDetectSystem)보다 먼저 돈다.
    /// 수명이 끝나면 LifetimeSystem이 지운다.
    /// </summary>
    public sealed class ProjectileMotionSystem : GameSystem, ITick
    {
        public void Tick(float dt)
        {
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(HitboxComponent), typeof(ProjectileComponent)))
            {
                var hitboxes = chunk.GetComponentArray<HitboxComponent>();
                var projectiles = chunk.GetComponentArray<ProjectileComponent>();
                for (int i = 0; i < chunk.Count; i++)
                    hitboxes[i].Center = hitboxes[i].Center + projectiles[i].Velocity * dt;
            }
        }
    }
}
