namespace IntoTheDungeon.Core.Abstractions.Physics
{
    public interface IPhysicsBody
    {
        void SetLinearVelocity(float x, float y);
        (float x, float y) GetLinearVelocity();
    }
}