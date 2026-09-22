
using IntoTheDungeon.Core.Abstractions.Types;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityRecipe
    {
        RecipeId Id { get; }
        void Apply(IEntityManager em, Entity e);
    }
}
