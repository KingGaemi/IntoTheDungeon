// Runtime.Unity.Composition
using System;
using System.Linq;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using ILogger = IntoTheDungeon.Core.Abstractions.Messages.ILogger;
using IntoTheDungeon.Features.Unity;

namespace IntoTheDungeon.Unity.World
{
    public static class BakerExtensions
    {
        public static Entity BakeAuthoring(this IWorld world, MonoBehaviour mb, Entity entity)
        {
            if (mb == null) throw new ArgumentNullException(nameof(mb));

            // MonoBehaviour → UnityAuthoring 어댑트
            var ua = new UnityAuthoring(mb);

            var baker = ua.CreateBaker();
            if (baker == null)
            {
                if (world.TryGet(out ILogger log))
                    log.Warn($"[Baker] {mb.name} returned null baker");
                else
                    Debug.LogWarning($"[Baker] {mb.name} returned null baker");
                return Entity.Null;
            }

            baker.Execute(ua, world, entity);
            return entity;
        }

        public static void BakeScene(this IWorld world)
        {
            var log = world.TryGet(out ILogger l) ? l : null;

            // 1) 루트 수집
            var roots = UnityEngine.Object
                .FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(mb => mb is IEntityRoot)
                .Cast<IEntityRoot>()
                .ToArray();

            if (log != null) log.Log($"[Baker] Found {roots.Length} entity roots");
            else Debug.Log($"[Baker] Found {roots.Length} entity roots");



            foreach (var root in roots)
            {
                try
                {
                    // 2) 엔티티 생성
                    var e = world.EntityManager.CreateEntity();
                    root.Entity = e;

                    // 3) Authoring 컴포넌트 수집
                    var authorings = root.Transform
                        .GetComponentsInChildren<MonoBehaviour>(true)
                        .Where(mb => mb is IAuthoring)   // IAuthoring 구현만
                        .ToArray();

                    if (log != null) log.Log($"[Baker] Baking {root.Transform.name}: {authorings.Length} components");
                    else Debug.Log($"[Baker] Baking {root.Transform.name}: {authorings.Length} components");


                    // 4) 베이크
                    int ok = 0;
                    foreach (var mb in authorings)
                    {
                        try { if (world.BakeAuthoring(mb, e) != Entity.Null) ok++; }
                        catch (Exception ex)
                        {

                            if (log != null) log.Error($"[Baker] Fail {mb.name}: {ex.Message}\n{ex.StackTrace}");
                            else Debug.LogError($"[Baker] Fail {mb.name}: {ex.Message}\n{ex.StackTrace}");
                        }
                    }

                    if (log != null) log.Log($"[Baker] {root.Transform.name}: {ok}/{authorings.Length} baked");
                    else Debug.Log($"[Baker] {root.Transform.name}: {ok}/{authorings.Length} baked");
                    // 5) 월드 주입
                    var injects = root.Transform
                        .GetComponentsInChildren<MonoBehaviour>(true)
                        .Where(mb => mb is IWorldInjectable)
                        .Cast<IWorldInjectable>();

                    foreach (var inj in injects) inj.Init(world);
                }
                catch (Exception ex)
                {
                    if (log != null) log.Error($"[Baker] Root {root.Transform.name} failed: {ex.Message}");
                    else Debug.LogError($"[Baker] Root {root.Transform.name} failed: {ex.Message}");
                }
            }
        }
    }
}