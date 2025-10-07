using IntoTheDungeon.Core.Physics;
using IntoTheDungeon.Core.Util.Physics;

namespace IntoTheDungeon.Core.ECS.Components.Physics
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