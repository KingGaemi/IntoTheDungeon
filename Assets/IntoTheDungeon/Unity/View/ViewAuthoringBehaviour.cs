using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Unity.View;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ViewAuthoringBehaviour : MonoBehaviour, IAuthoringProvider
{
    public RecipeId recipeId;
    public int overrideSortingLayerId;
    public int overrideOrderInLayer;

    public IAuthoring BuildAuthoring() => new ViewAuthoring
    {
        recipeId = recipeId,
        sortingLayerId = overrideSortingLayerId,
        orderInLayer = overrideOrderInLayer
    };
}