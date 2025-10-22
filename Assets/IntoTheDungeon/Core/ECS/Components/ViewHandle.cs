using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Components
{
    public struct ViewHandle : IComponentData
    {
        public readonly ViewId ViewId { get; }
        public bool Handled;
    }
}