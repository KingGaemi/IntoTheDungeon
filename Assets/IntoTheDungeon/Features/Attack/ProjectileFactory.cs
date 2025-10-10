using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components; //TransformComponent
using IntoTheDungeon.Core.Physics.Collision.Components;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Physics.Components;

namespace IntoTheDungeon.Features.Attack
{
    public static class ProjectileFactory
    {
        public static Entity CreateProjectile(
            IEntityManager entityManager,
            Entity owner,
            Vec2 position,
            Vec2 direction)
        {
            ref var attackerStatus = ref entityManager.GetComponent<StatusComponent>(owner);
            var normDir = direction.Normalized;
            var rotation = Mathx.Atan2(normDir.Y, normDir.X) * Mathx.Rad2Deg;

            var projectile = entityManager.CreateEntity();

            entityManager.AddComponent(projectile, new TransformComponent
            {
                Position = position,
                Direction = normDir,
                Rotation = rotation
            });

            entityManager.AddComponent(projectile, new KinematicComponent
            {
                Direction = normDir,
                Magnitude = attackerStatus.ProjectileAcceleration
            });

            entityManager.AddComponent(projectile, new SpriteComponent
            {
                SpriteId = 0, //TEMp
                SortingLayer = 7, //Projectiles
               
            });




            entityManager.AddComponent(projectile, new ProjectileComponent
            {
                Owner = owner,
                Damage = attackerStatus.Damage,
                Speed = attackerStatus.ProjectileAcceleration,
                LifeTime = attackerStatus.ProjectileLifeTime,
                ElapsedTime = 0f,
                Direction = normDir,
                Type = ProjectileType.Normal
            });
            
            entityManager.ViewOps.EnqueueSpawn(projectile, new ViewSpawnSpec {
                PrefabId = 0,
                SortingLayerId = 0,
                OrderInLayer = 0,
                Behaviours = new [] {
                    new BehaviourSpec { TypeName = "IntoTheDungeon.View.ProjectileView", Payload = null }
                }
            });

            // entityManager.AddComponent(projectile, new VisualTag { PrefabId = Prefabs.Projectile01 });

            entityManager.AddComponent(projectile, new CircleColliderComponent
            {
                Radius = 0.5f,
                Layer = CollisionLayer.Projectile,
                LayerMask = (int)CollisionLayer.Enemy
            });

            return projectile;
        }

        // AOE 공격 Factory
        public static Entity CreateAOEEffect(
            IEntityManager entityManager,
            Entity owner,
            Vec2 center,
            float radius)
        {
            if (!entityManager.TryGetComponent(owner, out StatusComponent attackerStatus))
            {
                
            }
            
            Entity aoe = entityManager.CreateEntity();

            entityManager.AddComponent(aoe, new TransformComponent
            {
                Position = center
            });

            entityManager.AddComponent(aoe, new AOEComponent
            {
                Owner = owner,
                Damage = attackerStatus.Damage,
                Radius = radius,
                Duration = 0.2f,  // AOE 지속시간
                ElapsedTime = 0f
            });

            entityManager.AddComponent(aoe, new BoxColliderComponent
            {
                Size = new Vec2(radius, radius),
                Layer = CollisionLayer.Enemy,
                LayerMask = (int)CollisionLayer.Enemy,
                IsTrigger = true
            });

            return aoe;
        }
    }
}
