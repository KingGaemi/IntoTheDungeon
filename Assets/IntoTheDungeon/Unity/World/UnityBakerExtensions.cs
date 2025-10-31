using System.Linq;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Unity;
using IntoTheDungeon.Core.Runtime.Physics;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Abstractions.View;
using IntoTheDungeon.Core.ECS.Abstractions.Spawn;
using IntoTheDungeon.Unity.Bridge.Core.Abstractions;
using IntoTheDungeon.Unity.Bridge.View.Abstractions;



namespace IntoTheDungeon.Unity.World
{
    public static class BakerExtensions
    {
        public static void BakeScene(this IWorld world)
        {
            // 필수 서비스 검증
            if (!world.TryGet(out IPhysicsBodyStore physicsStore) ||
                !world.TryGet(out ISystemSpawnQueue queue) ||
                !world.TryGet(out INameToRecipeRegistry name2recipe) ||
                !world.TryGet(out IViewRecipeRegistry viewRecipeRegistry) ||
                !world.TryGet(out ISceneViewRegistry sceneViewRegistry))
            {
                Debug.LogError("[BakeScene] 필수 서비스 누락");
                return;
            }


            var allMono = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var roots = allMono.OfType<IEntityRoot>().ToArray();


            foreach (var root in roots)
            {

                // ViewHandle이 있고 활성화된 경우 스킵
                if (world.EntityManager.HasComponent<ViewSpawnedTag>(root.Entity))
                {
                    continue;
                }

                var t = root.Transform;
                var rb = root.Rigidbody;
                var collider = root.Collider;

                var sceneLinkId = t.gameObject.GetInstanceID();
                sceneViewRegistry.Register(sceneLinkId, t.gameObject);

                var scriptContainer = t.GetComponentInChildren<ScriptContainer>(includeInactive: true);
                var scGO = scriptContainer.gameObject;

                var visualContainer = t.GetComponentInChildren<VisualContainer>(includeInactive: true);
                var vcGo = visualContainer.gameObject;

                // 물리 핸들
                if (rb != null)
                {
                    var body = new UnityPhysicsBody(rb, collider);
                    root.PhysicsHandle = physicsStore.Add(body);
                }
                else
                {
                    root.PhysicsHandle = PhysicsHandle.Invalid;
                }

                RecipeId recipe = default;

                var vmProvider = vcGo.GetComponent<IViewMarkerProvider>();

                var gpa = scGO.GetComponent<IGameplayAuthoring>();

                var inits = t.GetComponentsInChildren<ISpawnInitProvider>(true)
                            .SelectMany(p => p.BuildInits()).ToList();

                ViewMarker viewMarker = vmProvider.BuildMarker();

                if (gpa?.TryGetRecipe(out var rid1) == true)
                    recipe = rid1;
                else if (name2recipe?.TryGet(t.name, out var rid2) == true)
                    recipe = rid2;

                if (recipe == default)
                {
                    Debug.LogWarning($"[BakeScene] {t.name}: RecipeId 미해석, 스킵");
                    continue;
                }




                // SpawnSpec 생성
                float rad = t.eulerAngles.z * Mathf.Deg2Rad;
                var spec = new SpawnSpec
                {
                    PhysHandle = root.PhysicsHandle,
                    SceneLinkId = sceneLinkId,
                    Name = t.name,
                    Pos = new Vec2(t.position.x, t.position.y),
                    Dir = new Vec2(Mathf.Cos(rad), Mathf.Sin(rad)),
                    ViewOverride = vmProvider != null
                    ? new ViewOverride
                    {
                        SortingLayerId = viewMarker.SortingLayerId,
                        OrderInLayer = viewMarker.OrderInLayer
                    }
                    : null,
                    Inits = inits
                };

                Debug.Log($"[BakeScene] Enqueue {recipe.Value}");
                queue.Enqueue(new SpawnOrder(recipe, spec, SpawnSource.System));

            }
        }

    }
}