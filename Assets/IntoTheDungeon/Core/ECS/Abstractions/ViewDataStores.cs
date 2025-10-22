using System;
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    static public class ViewDataStores
    {
        public static readonly List<ViewSpawnData> Spawn = new(256);
        public static readonly List<Entity> Despawn = new(256);
        public static readonly TransformData[] transformData = new TransformData[512];
        private static int transformDataCount = 0;
        public static void ClearFrame()
        {
            Spawn.Clear();
            Despawn.Clear();
            Array.Clear(transformData, 0, transformDataCount);
            transformDataCount = 0;
        }
        public static int AddTransform(TransformData data)
        {
            int idx = transformDataCount;
            transformData[transformDataCount++] = data;
            return idx;

        }
    }
}