using UnityEngine;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Features.Unity;
using System;
using System.Linq;

namespace IntoTheDungeon.Core.Runtime.World
{
    public static class BakerExtensions
    {
        /// <summary>
        /// 단일 Authoring을 Entity로 변환
        /// </summary>
        public static Entity BakeAuthoring(
            this GameWorld world, 
            MonoBehaviour authoring, 
            Entity entity)
        {
            if (authoring == null)
                throw new ArgumentNullException(nameof(authoring));

            if (authoring is not IAuthoring bakable)
            {
                throw new ArgumentException(
                    $"{authoring.name} does not implement IAuthoring", 
                    nameof(authoring)
                );
            }

            var baker = bakable.CreateBaker();
            if (baker == null)
            {
                Debug.LogWarning($"[BakerExtensions] {authoring.name} returned null baker");
                return Entity.Null;
            }

            // Baker 실행
            baker.Execute(authoring, world, entity);

            return entity;
        }

        /// <summary>
        /// Scene의 모든 Entity 변환
        /// </summary>
        public static void BakeScene(this GameWorld world)
        {
            // 1. EntityRoot 찾기
            var entityRoots = UnityEngine.Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IEntityRoot>()
                .ToArray();

            Debug.Log($"[BakerExtensions] Found {entityRoots.Length} entity roots");

            foreach (var root in entityRoots)
            {
                try
                {
                    // 2. Entity 생성
                    var entity = world.EntityManager.CreateEntity();
                    root.Entity = entity;

                    // 3. 하위 Authoring Component 수집
                    var authorings = root.Transform
                        .GetComponentsInChildren<MonoBehaviour>()
                        .OfType<IAuthoring>()
                        .ToArray();

                    Debug.Log($"[BakerExtensions] Baking {root.Transform.name}: {authorings.Length} components");

                    // 4. 각 Authoring Bake
                    int successCount = 0;
                    foreach (var authoring in authorings)
                    {
                        try
                        {
                            world.BakeAuthoring(authoring as MonoBehaviour, entity);
                            successCount++;
                        }
                        catch (Exception e)
                        {
                            Debug.LogError(
                                $"[BakerExtensions] Failed to bake {(authoring as MonoBehaviour)?.name}: " +
                                $"{e.Message}\n{e.StackTrace}"
                            );
                        }
                    }

                    Debug.Log($"[BakerExtensions] {root.Transform.name}: {successCount}/{authorings.Length} baked");

                    // 5. World 주입
                    var injectables = root.Transform
                        .GetComponentsInChildren<MonoBehaviour>()
                        .OfType<IWorldInjectable>();

                    foreach (var injectable in injectables)
                    {
                        injectable.Init(world);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BakerExtensions] Failed to bake root {root.Transform.name}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Prefab Spawn
        /// </summary>
        public static Entity SpawnPrefab(
            this GameWorld world, 
            GameObject prefab, 
            Vector3 position, 
            Quaternion rotation)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            // 1. Instantiate
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation);

            // 2. EntityRoot 찾기
            var root = instance.GetComponent<IEntityRoot>();
            if (root == null)
            {
                Debug.LogError($"[BakerExtensions] Prefab {prefab.name} has no IEntityRoot");
                UnityEngine.Object.Destroy(instance);
                return Entity.Null;
            }

            // 3. Entity 생성
            var entity = world.EntityManager.CreateEntity();
            root.Entity = entity;

            // 4. Authoring Bake
            var authorings = instance.GetComponentsInChildren<MonoBehaviour>()
                .OfType<IAuthoring>();

            foreach (var authoring in authorings)
            {
                world.BakeAuthoring(authoring as MonoBehaviour, entity);
            }

            // 5. World 주입
            var injectables = instance.GetComponentsInChildren<MonoBehaviour>()
                .OfType<IWorldInjectable>();

            foreach (var injectable in injectables)
            {
                injectable.Init(world);
            }

            return entity;
        }
    }
}