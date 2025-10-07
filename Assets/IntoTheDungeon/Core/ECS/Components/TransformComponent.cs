
namespace IntoTheDungeon.Core.ECS.Components
{
    public struct TransformComponent : IComponentData
    {
        public Vec2 Position;
        public Vec2 Direction;

        public float Rotation;
    }
}
