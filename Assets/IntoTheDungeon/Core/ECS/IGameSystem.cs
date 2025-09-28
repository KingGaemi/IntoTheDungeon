
namespace IntoTheDungeon.Core.ECS
{
    public interface IGameSystem
    {
        bool Enabled { get; set; }
        int  Priority { get; }          // 실행 우선순위
        void Initialize(Runtime.World.GameWorld world);   // DI/레퍼런스 주입 지점
        void Shutdown();
    }

    public interface ITick       { void Tick(float dt); }        // Update
    public interface IFixedTick  { void FixedTick(float dt); }   // FixedUpdate
    public interface ILateTick   { void LateTick(float dt); }    // LateUpdate
}
