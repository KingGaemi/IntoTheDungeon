using System.Collections.Generic;
using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.Runtime.World;


namespace IntoTheDungeon.Core.Runtime.ECS.Manager
{
    public sealed class SystemManager : Abstractions.IDisposable
    {
        private readonly GameWorld _world;
        private readonly Dictionary<System.Type, IGameSystem> _registry = new();
        private List<IGameSystem> _systems = new();
        private bool _needsSort;
        public IReadOnlyList<IGameSystem> GetSystems() => _systems.AsReadOnly();
        public SystemManager(GameWorld world) { _world = world; }
        public void Add<T>(T system) where T : IGameSystem
        {
            var type = typeof(T);
            
            if (_registry.ContainsKey(type))
            {
                UnityEngine.Debug.LogWarning($"[SystemManager] System {type.Name} already registered");
                return;
            }
            
            _registry[type] = system;
            _systems.Add(system);
            _needsSort = true;
            system.Initialize(_world);
        }
        public T Get<T>() where T : IGameSystem
            => (T)_registry[typeof(T)];

        public bool Has<T>() where T : IGameSystem
            => _registry.ContainsKey(typeof(T));

        public void ExecuteUpdate(float dt)
        {
            EnsureSorted();
            foreach (var s in _systems)
                if (s.Enabled && s is ITick t) t.Tick(dt);
        }

        public void ExecuteFixed(float dt)
        {
            EnsureSorted();
            foreach (var s in _systems)
                if (s.Enabled && s is IFixedTick f) f.FixedTick(dt);
        }

        public void ExecuteLate(float dt)
        {
            EnsureSorted();
            foreach (var s in _systems)
                if (s.Enabled && s is ILateTick l) l.LateTick(dt);
        }

        private void EnsureSorted()
        {
            if (!_needsSort) return;
            _systems.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _needsSort = false;
        }

 
        public void Dispose()
        {
            for (int i = _systems.Count - 1; i >= 0; i--)
                _systems[i].Shutdown();
            _systems.Clear();
            _registry.Clear();
        }
    }
}