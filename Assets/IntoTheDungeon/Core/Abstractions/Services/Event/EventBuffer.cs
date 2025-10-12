using System;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    internal sealed class EventBuffer<T> : IEventBuffer<T> where T : unmanaged
    {
        T[] _items;
        int _count;

        public int Count => _count;

        public EventBuffer(int initialCapacity = 256)
        {
            _items = new T[initialCapacity];
        }

        public void Add(T item)
        {
            if (_count == _items.Length)
                Array.Resize(ref _items, _items.Length * 2);
            
            _items[_count++] = item;
        }

        public ReadOnlySpan<T> AsSpan() => new(_items, 0, _count);

        public void Clear() => _count = 0;
    }
}