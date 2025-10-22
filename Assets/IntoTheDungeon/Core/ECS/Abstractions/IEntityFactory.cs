
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityFactory
    {
        Entity Spawn(RecipeId id, in SpawnSpec p);
        bool TrySpawn(RecipeId id, in SpawnSpec p, out Entity e);
    }
}
