using IntoTheDungeon.Core.ECS;

namespace IntoTheDungeon.Core.Physics.Collision
{
    public class ProjectileCollisionSystem : GameSystem, ITick
    {
        public override int Priority => 250;  // Movement 이후

        public void Tick(float dt)
        {
            // Projectile vs Enemy 충돌 검사
            var projectileChunks = _world.EntityManager.GetChunks(
                typeof(ProjectileComponent),
                typeof(TransformComponent),
                typeof(CircleColliderComponent));

            var enemyChunks = _world.EntityManager.GetChunks(
                typeof(EnemyComponent),  // 또는 적절한 컴포넌트
                typeof(TransformComponent),
                typeof(CircleColliderComponent));

            foreach (var pChunk in projectileChunks)
            {
                var pEntities = pChunk.GetEntityArray();
                var projectiles = pChunk.GetComponentArray<ProjectileComponent>();
                var pTransforms = pChunk.GetComponentArray<TransformComponent>();
                var pColliders = pChunk.GetComponentArray<CircleColliderComponent>();

                foreach (var eChunk in enemyChunks)
                {
                    var eEntities = eChunk.GetEntityArray();
                    var eTransforms = eChunk.GetComponentArray<TransformComponent>();
                    var eColliders = eChunk.GetComponentArray<CircleColliderComponent>();

                    for (int i = 0; i < pChunk.Count; i++)
                    {
                        ref var projectile = ref projectiles[i];
                        var pTransform = pTransforms[i];
                        var pCollider = pColliders[i];

                        // LayerMask 체크
                        if ((pCollider.LayerMask & (int)CollisionLayer.Enemy) == 0)
                            continue;

                        for (int j = 0; j < eChunk.Count; j++)
                        {
                            var eTransform = eTransforms[j];
                            var eCollider = eColliders[j];

                            // Circle-Circle 충돌 검사
                            float distSq = Vec2.DistanceSquared(
                                pTransform.Position, 
                                eTransform.Position);
                            float radiusSum = pCollider.Radius + eCollider.Radius;

                            if (distSq <= radiusSum * radiusSum)
                            {
                                // 충돌 처리
                                OnProjectileHit(pEntities[i], eEntities[j], projectile);
                            }
                        }
                    }
                }
            }
        }

        private void OnProjectileHit(Entity projectile, Entity target, ProjectileComponent data)
        {
            // 데미지 적용 (DamageSystem에서 처리하도록 이벤트 발생)
            if (_world.EntityManager.HasComponent<HealthComponent>(target))
            {
                ref var health = ref _world.EntityManager
                    .GetComponent<HealthComponent>(target);
                health.CurrentHealth -= data.Damage;
            }

            // Piercing이 아니면 발사체 제거
            if (!_world.EntityManager.HasComponent<PiercingComponent>(projectile))
            {
                _world.EntityManager.DestroyEntity(projectile);
            }
            else
            {
                ref var piercing = ref _world.EntityManager
                    .GetComponent<PiercingComponent>(projectile);
                piercing.CurrentPierceCount++;
                
                if (piercing.CurrentPierceCount >= piercing.MaxPierceCount)
                {
                    _world.EntityManager.DestroyEntity(projectile);
                }
            }
        }
    }
}