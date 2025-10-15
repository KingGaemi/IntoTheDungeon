using System;
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Runtime.World;
using UnityEngine;

namespace IntoTheDungeon.Unity.World
{
    public static class WorldAccessExtensions
    {
        private static readonly Dictionary<Transform, IWorld> Cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearCache()
        {
            Cache.Clear();
        }
    }
    public static class WorldServicesExt
    {
        public static void SetOnce<T>(this GameWorld w, T svc) where T : class
        {
            if (w == null) throw new ArgumentNullException(nameof(w));
            if (svc == null) throw new ArgumentNullException(nameof(svc));
            if (!w.TryGet(out T _)) w.Set(svc);
        }
    }

}
