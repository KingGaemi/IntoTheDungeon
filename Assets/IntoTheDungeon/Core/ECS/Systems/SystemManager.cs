using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;


namespace IntoTheDungeon.Core.ECS.Systems
{
    public sealed class SystemManager : ISystemManager
    {
        private readonly IWorld _world;
        private readonly Dictionary<System.Type, IGameSystem> _registry = new();
        private readonly List<IGameSystem> _flat = new();
        private readonly List<(IGameSystem S, int Seq)> _systems = new();
        private int _seq;
        private bool _needsSort;

        public IReadOnlyList<IGameSystem> Systems => _flat.AsReadOnly();
        public SystemManager(IWorld world) => _world = world;


        public void Add<T>(T system) where T : IGameSystem
        {
            var type = system.GetType();

            if (_registry.ContainsKey(type))
            {
                UnityEngine.Debug.LogWarning($"[SystemManager] System {type.Name} already registered");
                return;
            }

            system.Initialize(_world);
            _registry[type] = system;
            _systems.Add((system, ++_seq));
            _flat.Add(system);
            _needsSort = true;
        }
        public T Get<T>() where T : IGameSystem
            => (T)_registry[typeof(T)];

        public bool Has<T>() where T : IGameSystem
            => _registry.ContainsKey(typeof(T));


        public void ExecuteUpdate(float dt)
        {
            EnsureSorted();
            foreach (var (S, _) in _systems)
                if (S.Enabled && S is ITick t) t.Tick(dt);
        }


        public void ExecuteFixed(float dt)
        {
            EnsureSorted();
            foreach (var (S, _) in _systems)
                if (S.Enabled && S is IFixedTick f) f.FixedTick(dt);
        }

        public void ExecuteLate(float dt)
        {
            EnsureSorted();
            foreach (var (S, _) in _systems)
                if (S.Enabled && S is ILateTick l) l.LateTick(dt);
        }

        private void EnsureSorted()
        {
            if (!_needsSort) return;
            _systems.Sort((x, y) =>
            {
                int c = x.S.Priority.CompareTo(y.S.Priority);
                return c != 0 ? c : x.Seq.CompareTo(y.Seq);
            });
            _flat.Clear();
            for (int i = 0; i < _systems.Count; i++) _flat.Add(_systems[i].S);
            _needsSort = false;
        }
#if DEBUG
        public void DebugPrint()
        {
            UnityEngine.Debug.Log($"=== SystemManager: {_flat.Count} systems ===");

            EnsureSorted();

            foreach (var (sys, order) in _systems)
            {
                var type = sys.GetType();
                var enabled = sys.Enabled ? "✓" : "✗";
                var interfaces = new List<string>();

                if (sys is ITick) interfaces.Add("Update");
                if (sys is IFixedTick) interfaces.Add("Fixed");
                if (sys is ILateTick) interfaces.Add("Late");

                var stages = string.Join(", ", interfaces);

                UnityEngine.Debug.Log($"  [{enabled}] {type.Name} (order={order}, stages={stages})");
            }
        }
#endif

        public void Dispose()
        {
            for (int i = _systems.Count - 1; i >= 0; i--)
                _systems[i].S.Shutdown();
            _systems.Clear();
            _registry.Clear();
        }
    }
}