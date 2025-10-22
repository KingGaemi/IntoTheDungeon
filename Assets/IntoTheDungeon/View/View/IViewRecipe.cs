using UnityEngine;
using System;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.View
{

    public interface IViewRecipe
    {
        ViewId ViewId { get; }
        GameObject Prefab { get; }
        Type[] GetScriptContainerBehaviours();
        Type[] GetVisualContainerBehaviours();
    }
}