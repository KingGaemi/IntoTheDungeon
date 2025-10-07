#if false
using System.Collections.Generic;

using IntoTheDungeon.Core.ECS;
using IntoTheDungeon.Core.Abstractions.Messages;

public class SystemRegistry<T> : IRegistry<T>
{
    private readonly List<IComponent> list = new();
    private readonly ILogger _log;
    public bool Register(T item)
    {
        if (item == null) return false;
        if (list.Contains(item))
        {
            _log.Log($"This item already registered. {item}");
            return false;
        }
        list.Add(item);
        _log.Log($"Registered {item}");
        return true;
    }
    public bool Unregister(IComponent item)
    {
        return true;
    }
    public bool Contains(IComponent item)
    {
            return true;
    }
    public System.Collections.Generic.IReadOnlyList<IComponent> Items { get; }

}
#endif