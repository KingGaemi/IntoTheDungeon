using IntoTheDungeon.Core.Util.Physics;
using IntoTheDungeon.Core.ECS.Components;

namespace IntoTheDungeon.Features.Status
{
    public struct TransformComponent : IComponentData
    {
        public Vec2 Position;
        public Vec2 Direction;

        public float Rotation;
    }
}
