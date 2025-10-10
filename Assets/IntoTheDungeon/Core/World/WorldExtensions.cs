using System.Collections.Generic;
using IntoTheDungeon.Core.World.Abstractions;
using UnityEngine;

namespace IntoTheDungeon.Core.Runtime.World
{
    public static class WorldAccessExtensions
    {
        private static readonly Dictionary<Transform, IWorld> Cache = new();
        #nullable enable        
        public static IWorld? GetWorld(this MonoBehaviour mono)
        {
            if (mono is IWorldDependent dependent && dependent.World != null)
            {
                return dependent.World;
            }

            var root = mono.transform.root;
            if (Cache.TryGetValue(root, out var world))
            {
                return world;
            }

            var entityRoot = root.GetComponent<EntityRootBehaviour>();
            world = entityRoot?.World;

            if (world != null)
            {
                Cache[root] = world;
            }
#if UNITY_EDITOR
            else
            {
                Debug.LogWarning($"[WorldAccess] No World found for {mono.name}");
            }
#endif

            return world;
        }
        #nullable restore

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
