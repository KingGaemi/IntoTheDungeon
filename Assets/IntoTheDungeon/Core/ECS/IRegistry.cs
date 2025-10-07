using IntoTheDungeon.Core.ECS.Components;
namespace IntoTheDungeon.Core.Abstractions
{
    public interface IRegistry<T> where T : IComponentData
    {
        bool Register(T item);
        bool Unregister(T item);
        bool Contains(T item);
        System.Collections.Generic.IReadOnlyList<T> Items { get; }
    }
}