using System.Collections.Generic;

using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.Runtime.World;

namespace IntoTheDungeon.Core.Runtime.Runner
{
    // Core.Runtime.Runner (순수)
    public sealed class SystemRunner
    {
        private readonly List<IGameSystem> _systems = new();
        public GameWorld World { get; }
        public SystemRunner(GameWorld world) { World = world; }

        public void Add(IGameSystem s){ _systems.Add(s); s.Initialize(World); }
        public void RunUpdate(float dt){ foreach (var s in _systems) if (s.Enabled && s is ITick t) t.Tick(dt); }
        public void RunFixed (float dt){ foreach (var s in _systems) if (s.Enabled && s is IFixedTick f) f.FixedTick(dt); }
        public void RunLate  (float dt){ foreach (var s in _systems) if (s.Enabled && s is ILateTick  l) l.LateTick(dt); }
    }

}