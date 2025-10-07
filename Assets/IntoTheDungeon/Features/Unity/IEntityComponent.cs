using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Core.Runtime.World;
namespace IntoTheDungeon.Features.Unity
{
    public interface IEntityComponent : IEntityRootDependent, IWorldDependent
    {
        Entity Entity { get; }  // 계약만 정의
    }
}