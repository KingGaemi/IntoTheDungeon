
namespace IntoTheDungeon.Core.ECS
{
    public abstract class GameSystemBase : IGameSystem
    {
        public bool Enabled { get; set; } = true;
        public virtual int Priority => 0;
        protected Runtime.World.GameWorld World { get; private set; }

        public virtual void Initialize(Runtime.World.GameWorld world) => World = world;
        public virtual void Shutdown() { }

        // 공통 훅 확장 여지: OnBeforeTick/OnAfterTick 등
    }

    // 기본 no-op 구현 제공. 필요 메서드만 오버라이드.
    public abstract class GameSystem : GameSystemBase, ITick, IFixedTick, ILateTick
    {
        public virtual void Tick(float dt) { }
        public virtual void FixedTick(float dt) { }
        public virtual void LateTick(float dt) { }
    }
}
