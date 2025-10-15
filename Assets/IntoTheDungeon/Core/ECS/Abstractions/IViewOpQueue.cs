
namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IViewOpQueue
    {
        void Enqueue(Entity e);
        void Enqueue(Entity e, in SpawnData spawnData);
        void Enqueue(Entity e, in TransformData transformData);

        // 모든 작업을 sink로 옮기고 내부 큐 비움
        int Drain(ViewOp[] sink, ref int count);
    }
}
