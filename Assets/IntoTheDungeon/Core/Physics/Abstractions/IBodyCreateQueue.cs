
namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface IBodyCreateQueue
    {
        void Enqueue(BodyCreateSpec spec);
        // 모든 작업을 sink로 옮기고 내부 큐 비움
        int Drain(BodyCreateSpec[] sink, ref int count);
    }
}
