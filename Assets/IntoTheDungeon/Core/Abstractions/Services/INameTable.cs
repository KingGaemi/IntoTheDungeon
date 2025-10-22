using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public interface INameTable
    {
        NameId GetId(string name);
        string GetName(NameId id);
        bool TryGetId(string name, out NameId id);
        int Count { get; }
    }
}


