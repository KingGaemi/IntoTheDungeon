using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityViewMapRegistry
    {
        void Register(RecipeId e, ViewId go);
        bool TryGetView(RecipeId e, out ViewId go);
        bool TryGetEntity(ViewId go, out RecipeId e);
        bool Unregister(RecipeId e);
    }
}