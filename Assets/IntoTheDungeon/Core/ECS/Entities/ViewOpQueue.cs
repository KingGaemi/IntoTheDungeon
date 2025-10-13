using System;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Entities
{
    public sealed class ViewOpQueue : IViewOpQueue
    {
        readonly ViewOp[] _buffer;
        int _count;

        public ViewOpQueue(int capacity = 512)
        {
            _buffer = new ViewOp[capacity];
            _count = 0;
        }

        public void EnqueueSpawn(Entity e, in ViewSpawnSpec spec)
        {
            if (_count >= _buffer.Length) return;
            _buffer[_count++] = new ViewOp { Kind = ViewOpKind.Spawn, Entity = e, Spawn = spec };
        }

        public void EnqueueDespawn(Entity e)
        {
            if (_count >= _buffer.Length) return;
            _buffer[_count++] = new ViewOp { Kind = ViewOpKind.Despawn, Entity = e };
        }

        public int Drain(ViewOp[] sink, ref int count)
        {
            int n = Math.Min(_count, sink.Length - count);
            Array.Copy(_buffer, 0, sink, count, n);
            count += n;
            _count = 0;
            return n;
        }

    }
}