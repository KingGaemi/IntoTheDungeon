using IntoTheDungeon.Core.ECS.Entities;

namespace IntoTheDungeon.Features.Unity
{
    public interface IEntityRoot
    {
        Entity Entity { set; get; }
        UnityEngine.Transform Transform { get; }
    }
}