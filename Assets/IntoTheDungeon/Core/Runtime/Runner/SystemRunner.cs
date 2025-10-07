#if false
using System.Collections.Generic;
using System;

using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.Runtime.World;


namespace IntoTheDungeon.Core.Runtime.Runner
{
    // Core.Runtime.Runner (순수)
    public sealed class SystemRunner : Abstractions.IDisposable
    {
        private readonly GameWorld _world;  // private 참조
        private readonly List<IGameSystem> _systems = new();
        private readonly Dictionary<Type, IGameSystem> _registry = new();
        private bool _needsSort;

        public SystemRunner(GameWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public void Add(IGameSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            
            // 중복 방지
            var type = system.GetType();
            if (_registry.ContainsKey(type))
            {
                UnityEngine.Debug.LogWarning($"System {type.Name} already added");
                return;
            }

            _systems.Add(system);
            _registry[type] = system;
            _needsSort = true;
            
            system.Initialize(_world);
        }

        // System 조회
        public T Get<T>() where T : IGameSystem
        {
            if (_registry.TryGetValue(typeof(T), out var sys))
                return (T)sys;
            throw new InvalidOperationException($"System not found: {typeof(T).Name}");
        }

        public void RunUpdate(float dt)
        {
            EnsureSorted();
            foreach (var s in _systems)
                if (s.Enabled && s is ITick t) t.Tick(dt);
        }

        public void RunFixed(float dt)
        {
            EnsureSorted();
            foreach (var s in _systems)
                if (s.Enabled && s is IFixedTick f) f.FixedTick(dt);
        }

        public void RunLate(float dt)
        {
            EnsureSorted();
            foreach (var s in _systems)
                if (s.Enabled && s is ILateTick l) l.LateTick(dt);
            FlushAll();
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
#endif