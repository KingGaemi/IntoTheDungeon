using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.World.Abstractions;

namespace IntoTheDungeon.Core.ECS.Systems
{
    public abstract class GameSystem : IGameSystem
    {
        public bool Enabled { get; set; } = true;
        public int Priority { get; }
        protected IWorld _world;

        public GameSystem(int priority = 0) => Priority = priority;
        public virtual void Initialize(IWorld world) { _world = world; }
        public virtual void Shutdown() { }

    }

}
