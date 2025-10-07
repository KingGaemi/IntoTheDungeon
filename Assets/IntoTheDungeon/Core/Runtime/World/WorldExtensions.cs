using System.Collections.Generic;
using UnityEngine;

namespace IntoTheDungeon.Core.Runtime.World
{
    public static class WorldAccessExtensions
    {
        private static readonly Dictionary<Transform, GameWorld> _cache = new();
        
        public static GameWorld GetWorld(this MonoBehaviour mono)
        {
            // 1. IWorldDependent 체크 (가장 빠름)
            if (mono is IWorldDependent dependent && dependent.World != null)
            {
                return dependent.World;
            }
            
            // 2. Root 캐싱 탐색
            var root = mono.transform.root;
            if (_cache.TryGetValue(root, out var world))
            {
                return world;
            }
            
            // 3. EntityRoot 찾기
            var entityRoot = root.GetComponent<EntityRootBehaviour>();
            world = entityRoot?.World;
            
            if (world != null)
            {
                _cache[root] = world;
            }
            #if UNITY_EDITOR
            else
            {
                Debug.LogWarning($"[WorldAccess] No World found for {mono.name}");
            }
            #endif
            
            return world;
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache()
        {
            _cache.Clear();
        }
    }
}
