using System;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface IEventHub
    {
        void Publish<T>(in T ev) where T : unmanaged;
        ReadOnlySpan<T> Consume<T>() where T : unmanaged;
        void ClearFrame();
        void Clear<T>() where T : unmanaged;
        int Count<T>() where T : unmanaged;
        bool HasEvents<T>() where T : unmanaged;
        

        #if DEBUG
        string GetStatistics();
        EventStatistics? GetStatistics<T>() where T : unmanaged;
        #endif
    }
}
