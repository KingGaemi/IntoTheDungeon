using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Systems
{
    public static class SystemManagerExt
    {
        public static void AddUnique<T>(this ISystemManager m, T sys) where T : IGameSystem
        {
            if (m.Has<T>()) return;
            m.Add(sys);
        }
    }
}