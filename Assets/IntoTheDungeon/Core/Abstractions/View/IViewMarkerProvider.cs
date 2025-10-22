using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Core.Abstractions.View
{
    public interface IViewMarkerProvider
    {
        ViewMarker BuildMarker();
    }
}
