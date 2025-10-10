using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;
using IntoTheDungeon.Core.Physics.Abstractions;

namespace IntoTheDungeon.Features.Physics.Components
{
    public struct PhysicsBodyComponent : IComponentData, IPhysicsBody
    {
        public Vec2 Velocity; //
        public void SetLinearVelocity(float x, float y)
        {
            Velocity.X = x;
            Velocity.Y = y;
        }
        public readonly (float x, float y) GetLinearVelocity() => (Velocity.X, Velocity.Y);
 

    }
}