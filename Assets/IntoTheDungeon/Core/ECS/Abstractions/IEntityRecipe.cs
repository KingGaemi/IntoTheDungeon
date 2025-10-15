
using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityRecipe
    {
        RecipeId Id { get; }
        void Apply(IEntityManager em, Entity e);
    }
}
