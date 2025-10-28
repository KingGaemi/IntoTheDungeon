using IntoTheDungeon.Core.ECS.Abstractions;
using UnityEngine;

namespace IntoTheDungeon.Unity.Bridge.View.Abstractions
{
    public interface IViewPort
    {
        bool TryGetViewRoot(Entity e, out Transform root);
    }
}