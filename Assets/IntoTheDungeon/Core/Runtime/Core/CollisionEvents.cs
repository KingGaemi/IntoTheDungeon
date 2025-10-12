using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.ECS.Abstractions;
using System.Collections.Generic;


namespace IntoTheDungeon.Runtime.Core
{
    public sealed class CollisionEvents : ICollisionEvents
    {
        private readonly List<(Entity, Entity)> _bufEnter = new();
        private readonly List<(Entity, Entity)> _bufExit = new();
        public void EnqueueEnter(Entity a, Entity b) => _bufEnter.Add((a, b));
        public void EnqueueExit(Entity a, Entity b) => _bufExit.Add((a, b));
        public IEnumerable<(Entity, Entity, bool)> Drain()
        {
            // Enter 이벤트
            foreach (var (a, b) in _bufEnter)
                yield return (a, b, true);

            // Exit 이벤트
            foreach (var (a, b) in _bufExit)
                yield return (a, b, false);

            // 비우기
            _bufEnter.Clear();
            _bufExit.Clear();
        }
    }
}