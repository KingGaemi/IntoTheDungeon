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

            foreach (var root in roots)
            {
                var e = world.EntityManager.CreateEntity();
                root.Entity = e;

                // ③ AuthoringProvider 수집
                var providers = root.Transform.GetComponentsInChildren<MonoBehaviour>(true)
                    .OfType<IAuthoringProvider>()
                    .ToArray();

                int ok = 0;
                foreach (var p in providers)
                {
                    try { if (world.BakeAuthoring(p, e) != Entity.Null) ok++; }
                    catch (Exception ex) { Debug.LogError(ex); }
                }

                // ⑤ 월드 주입은 베이크 끝나고
                foreach (var inj in root.Transform.GetComponentsInChildren<MonoBehaviour>(true).OfType<IWorldInjectable>())
                    inj.Init(world);
            }
        }

    }
}