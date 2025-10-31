using System;
using IntoTheDungeon.Core.Physics.Abstractions;


namespace IntoTheDungeon.Core.Physics.Implementation
{
    public class BodyCreateQueue : IBodyCreateQueue
    {
        readonly BodyCreateSpec[] _buffer;
        int _count;

        public BodyCreateQueue(int capacity = 512)
        {
            _buffer = new BodyCreateSpec[capacity];
            _count = 0;
        }

        public int Drain(BodyCreateSpec[] sink, ref int count)
        {
            int n = Math.Min(_count, sink.Length - count);
            Array.Copy(_buffer, 0, sink, count, n);
            count += n;
            _count = 0;
            return n;
        }

        public void Enqueue(BodyCreateSpec spawnData)
        {
            if (_count >= _buffer.Length) return;
            _buffer[_count++] = spawnData;
        }

    }
}