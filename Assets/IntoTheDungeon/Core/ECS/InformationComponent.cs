using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.Core.Components
{
    public class InformationComponent : IComponentData
    {
        private readonly string displayName;
        public string DisplayName => displayName;

    }

}