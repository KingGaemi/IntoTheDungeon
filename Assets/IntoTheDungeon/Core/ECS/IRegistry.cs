namespace IntoTheDungeon.Core.ECS
{
    public interface IRegistry<T> where T : IComponent
    {
        bool Register(T item);
        bool Unregister(T item);
        bool Contains(T item);
        System.Collections.Generic.IReadOnlyList<T> Items { get; }
    }
}