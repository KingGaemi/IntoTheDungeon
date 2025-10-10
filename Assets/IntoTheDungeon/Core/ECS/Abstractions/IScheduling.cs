namespace IntoTheDungeon.Core.ECS.Abstractions.Scheduling
{
    public interface ITick       { void Tick(float dt); }        // Update
    public interface IFixedTick  { void FixedTick(float dt); }   // FixedUpdate
    public interface ILateTick   { void LateTick(float dt); }    // LateUpdate
}
