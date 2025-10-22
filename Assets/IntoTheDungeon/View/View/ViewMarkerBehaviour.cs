using IntoTheDungeon.Core.Abstractions.View;
using IntoTheDungeon.Core.ECS.Components;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ViewMarkerBehaviour : MonoBehaviour, IViewMarkerProvider
{
    private const short INVALID_LAYER = -1;

    [SerializeField] private bool overrideSortingLayer;
    [SerializeField] private short sortingLayerId = INVALID_LAYER;

    [SerializeField] private bool overrideOrderInLayer;
    [SerializeField] private short orderInLayer = INVALID_LAYER;

    public ViewMarker BuildMarker() => new()
    {
        SortingLayerId = overrideSortingLayer ? sortingLayerId : INVALID_LAYER,
        OrderInLayer = overrideOrderInLayer ? orderInLayer : INVALID_LAYER
    };

}