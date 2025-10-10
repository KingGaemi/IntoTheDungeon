using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Unity
{
    public interface IEntityRoot
    {
        Entity Entity { set; get; }
        UnityEngine.Transform Transform { get; }
    }
}
