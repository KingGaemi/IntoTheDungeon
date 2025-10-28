using UnityEngine;
using System;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Unity.Bridge.View.Abstractions
{
    public interface IViewRecipe
    {
        ViewId ViewId { get; }
        GameObject Prefab { get; }
        Type[] GetScriptContainerBehaviours();
        Type[] GetVisualContainerBehaviours();
    }
}