using System;

namespace IntoTheDungeon.Core.Abstractions
{
    public interface IRegistry<T> where T : notnull, IEquatable<T>
    {
        bool Register(T item);
        bool Unregister(T item);
        bool Contains(T item);
        System.Collections.Generic.IReadOnlyCollection<T> Items { get; }
    }
}