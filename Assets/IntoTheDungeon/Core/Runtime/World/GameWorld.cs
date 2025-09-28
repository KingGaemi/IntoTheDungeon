namespace IntoTheDungeon.Core.Runtime.World
{    public sealed class GameWorld
    {
        readonly System.Collections.Generic.Dictionary<System.Type, object> map = new();

        public T Get<T>() where T : class
            => map.TryGetValue(typeof(T), out var v) ? (T)v : null;

        public void Set<T>(T svc) where T : class
            => map[typeof(T)] = svc;


        public Runner.SystemRunner Runner { get; internal set; }
    }
}