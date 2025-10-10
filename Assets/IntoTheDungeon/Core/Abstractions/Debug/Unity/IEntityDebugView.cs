using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Debug
{
    public interface IEntityDebugView
    {
        int ArchetypeCount { get; }
        System.Collections.Generic.IReadOnlyList<IArchetype> GetArchetypes();

        IArchetype GetArchetype(Entity e);
        (int chunkIndex, int indexInChunk) GetEntityLocation(Entity e);

        
    }
}
