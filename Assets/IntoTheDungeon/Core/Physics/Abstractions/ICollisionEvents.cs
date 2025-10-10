using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface ICollisionEvents
    {
        void EnqueueEnter(Entity a, Entity b);
        void EnqueueExit(Entity a, Entity b);
        System.Collections.Generic.IEnumerable<(Entity A, Entity B, bool IsEnter)> Drain(); // 소비자 전용

    }
}