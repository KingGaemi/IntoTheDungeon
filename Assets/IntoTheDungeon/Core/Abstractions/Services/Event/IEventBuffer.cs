
using System;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IEventBuffer<T>  where T : unmanaged
    {
        int Count { get; }
        void Add(T item);
        ReadOnlySpan<T> AsSpan();
        void Clear();
    }
}