using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.World;
namespace IntoTheDungeon.Features.Unity
{
    public interface IEntityComponent : IEntityRootDependent, IWorldDependent
    {
        Entity Entity { get; }  // 계약만 정의
    }
}
