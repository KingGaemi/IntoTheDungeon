using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.View;

namespace IntoTheDungeon.Unity.View
{
    public sealed class ViewAuthoring : IAuthoring
    {
        public RecipeId recipeId;
        public int sortingLayerId;
        public int orderInLayer;

        sealed class Baker : IBaker
        {
            public void Execute(IAuthoring ua, IWorld world, Entity e)
            {
                var a = (ViewAuthoring)ua;
                world.EntityManager.AddComponent(e, new ViewMarker
                {
                    RecipeId = a.recipeId.Value,
                    SortingLayerId = a.sortingLayerId,
                    OrderInLayer = a.orderInLayer
                });
            }
        }
        public IBaker CreateBaker() => new Baker();
    }

}