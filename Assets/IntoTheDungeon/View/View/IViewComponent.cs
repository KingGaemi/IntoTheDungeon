using IntoTheDungeon.Unity.View;

namespace IntoTheDungeon.Features.View
{
    public interface IViewComponent
    {
        public ViewBridge ViewBridge { get; }
    }
}