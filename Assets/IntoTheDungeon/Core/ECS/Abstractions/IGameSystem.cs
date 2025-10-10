
using IntoTheDungeon.Core.World.Abstractions;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IGameSystem
    {
        bool Enabled { get; set; }
        int Priority { get; }          // 실행 우선순위
        void Initialize(IWorld world);   // DI/레퍼런스 주입 지점
        void Shutdown();
        
    }

}
