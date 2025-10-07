using System.Collections.Generic;
using IntoTheDungeon.Core.Runtime.World;

namespace IntoTheDungeon.Core.ECS
{
    public abstract class GameSystem : IGameSystem
    {
        protected GameWorld _world;
        public bool Enabled { get; set; } = true;
        public abstract int Priority { get; }

        public virtual void Initialize(GameWorld world) { _world = world; }
        public virtual void Shutdown() { }

    }

}
