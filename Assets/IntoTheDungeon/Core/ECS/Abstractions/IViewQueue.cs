
namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IViewOpQueue
    {
        void EnqueueSpawn(Entity e, in ViewSpawnSpec spec);
        void EnqueueDespawn(Entity e);

        // 모든 작업을 sink로 옮기고 내부 큐 비움
        int Drain(ViewOp[] sink,ref int count);
    }
}
