// Runtime.Unity.Composition
using System;
using System.Linq;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using ILogger = IntoTheDungeon.Core.Abstractions.Messages.ILogger;
using IntoTheDungeon.Features.Unity;
using System.Collections.Generic;


namespace IntoTheDungeon.Unity.World
{
    public static class BakerExtensions
    {
        public static Entity BakeAuthoring(this IWorld world, IAuthoring authoring, Entity entity)
        {
            if (authoring == null) return Entity.Null;
            var baker = authoring.CreateBaker();
            if (baker == null) return Entity.Null;
            baker.Execute(authoring, world, entity);
            return entity;
        }

        public static Entity BakeAuthoring(this IWorld world, IAuthoringProvider provider, Entity entity)
            => world.BakeAuthoring(provider?.BuildAuthoring(), entity);

        public static void BakeScene(this IWorld world)
        {
            var log = world.TryGet(out ILogger l) ? l : null;
            var roots = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IEntityRoot>()
                .ToArray();
            var injected = new HashSet<Component>();
            foreach (var root in roots)
            {
                var em = world.EntityManager;
                var e = em.Exists(root.Entity) ? root.Entity : em.CreateEntity();
                bool createdHere = root.Entity == Entity.Null;


                try
                {
                    root.Entity = e;

                    var providers = root.Transform.GetComponentsInChildren<MonoBehaviour>(true)
                                       .OfType<IAuthoringProvider>().ToArray();

                    foreach (var p in providers)
                        world.BakeAuthoring(p, e);

                    // 베이크 완료 후 월드 주입
                    foreach (var inj in root.Transform.GetComponentsInChildren<MonoBehaviour>(true)
                                                      .OfType<IWorldInjectable>())
                    {
                        inj.Init(world);
                        injected.Add((Component)inj);
                    }
                }
                catch
                {
                    if (createdHere) world.EntityManager.DestroyEntity(e); // 롤백
                    throw;
                }
            }
            var services = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                       .OfType<IWorldInjectable>();

            foreach (var s in services)
            {
                var c = (Component)s;
                if (injected.Contains(c)) continue;                 // 이미 1)에서 주입됨
                if (c.GetComponentInParent<IEntityRoot>() != null)  // 루트 하위는 1)에서 처리
                    continue;

                s.Init(world); // ViewBridge, UI 시스템, 전역 매니저 등
            }
        }

    }
}