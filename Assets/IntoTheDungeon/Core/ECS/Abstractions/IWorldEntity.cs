using IntoTheDungeon.Core.World.Abstractions;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IWorldEntity
    {
        void Initialize(IWorld world, Entity entity);
    }
}