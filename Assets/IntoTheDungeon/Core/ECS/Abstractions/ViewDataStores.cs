using System.Collections.Generic;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    static public class ViewDataStores
    {
        public static readonly List<SpawnData> Spawn = new(256);
        public static readonly List<Entity> Despawn = new(256);
        public static readonly List<TransformData> Xform = new(512);
        public static readonly List<BehaviourSpec> BehPool = new(1024);

        public static void ClearFrame()
        {
            Spawn.Clear(); Xform.Clear(); BehPool.Clear();
        }
    }
}