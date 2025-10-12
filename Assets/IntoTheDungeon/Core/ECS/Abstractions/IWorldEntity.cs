using IntoTheDungeon.Core.Abstractions.World;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IWorldEntity
    {
        void Initialize(IWorld world, Entity entity);
    }
}