namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IManagedComponentStore
    {
        void AddManagedComponent<T>(Entity e, T c) where T : class, IManagedComponent;
        T GetManagedComponent<T>(Entity e) where T : class, IManagedComponent;
        bool TryGetManagedComponent<T>(Entity e, out T c) where T : class;
        bool HasManagedComponent<T>(Entity e) where T : class, IManagedComponent;
        void RemoveManagedComponent<T>(Entity e) where T : class, IManagedComponent;
    }
}