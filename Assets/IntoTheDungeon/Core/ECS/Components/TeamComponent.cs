using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.ECS.Components
{
    public struct TeamComponent : IComponentData
    {
        public TeamFlag TeamFlag;
    }

}
