using IntoTheDungeon.Core.ECS.Abstractions;
namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface IOverlapState
    {
        bool IsOverlapping(Entity a, Entity b);
        System.Collections.Generic.IEnumerable<Entity> GetOverlaps(Entity a, uint layerMask);
    }
}
