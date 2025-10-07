using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.Runtime.ECS.Manager;
using IntoTheDungeon.Core.Util.Physics;
using IntoTheDungeon.Features.Status;
using IntoTheDungeon.Features.Attack;
using UnityEngine;
using IntoTheDungeon.Core.Physics.Collision;

namespace IntoTheDungeon.Features.Attack
{
    public static class ProjectileFactory
    {
        public static Entity CreateProjectile(
            EntityManager entityManager,
            Entity owner,
            Vec2 position,
            Vec2 direction,
            StatusComponent attackerStatus)
        {
            // 1. 새 Entity 생성
            Entity projectile = entityManager.CreateEntity();

            // 2. Transform 컴포넌트 (위치/회전)
            entityManager.AddComponent(projectile, new TransformComponent
            {
                Position = position,
                Rotation = Mathf.Atan2(direction.Y, direction.X) * Mathf.Rad2Deg
            });

            ProjectileType TypeMemo = ProjectileType.Normal;
            // 3. Projectile 컴포넌트
            entityManager.AddComponent(projectile, new ProjectileComponent
            {
                Owner = owner,
                Damage = attackerStatus.Damage,
                Speed = attackerStatus.ProjectileAcceleration,  // 설정값 또는 attackerStatus에서
                LifeTime = attackerStatus.ProjectileLifeTime,
                ElapsedTime = 0f,
                Direction = direction.Normalized,
                Type = ProjectileType.Normal
            });


            switch (TypeMemo) {
                case ProjectileType.Normal:
                    entityManager.AddComponent(projectile, new CircleColliderComponent
                    {
                        Radius = 0.5f,
                        LayerMask = LayerMask.GetMask("Enemy")  // 필요한 레이어
                    });

                    break;
            }
            // 4. 충돌 컴포넌트
           
            // 5. Visual 컴포넌트 (선택사항)
            entityManager.AddComponent(projectile, new ProjectileVisualComponent
            {
                PrefabID = 0  // Prefab Pool에서 가져올 ID
            });

            return projectile;
        }

        // AOE 공격용 Factory
        public static Entity CreateAOEEffect(
            EntityManager entityManager,
            Entity owner,
            Vector2 center,
            float radius,
            StatusComponent attackerStatus)
        {
            Entity aoe = entityManager.CreateEntity();

            entityManager.AddComponent(aoe, new TransformComponent
            {
                Position = center
            });

            entityManager.AddComponent(aoe, new AOEComponent
            {
                Owner = owner,
                Damage = attackerStatus.AttackDamage,
                Radius = radius,
                Duration = 0.2f,  // AOE 지속시간
                ElapsedTime = 0f
            });

            entityManager.AddComponent(aoe, new ColliderComponent
            {
                Radius = radius,
                LayerMask = LayerMask.GetMask("Enemy")
            });

            return aoe;
        }
    }
}