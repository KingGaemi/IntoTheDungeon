namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IServiceRegistry
    {
        void Set<T>(T service) where T : class;
        bool TryGet<T>(out T service) where T : class;
        T Require<T>() where T : class;
    }
}