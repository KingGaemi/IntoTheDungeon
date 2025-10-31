using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface IHandleOwnerIndex
    {
        void Register(PhysicsHandle handle, Entity entity);
        bool TryGetOwner(PhysicsHandle handle, out Entity entity);
        bool TryGetHandle(Entity entity, out PhysicsHandle handle);
        void UnregisterByHandle(PhysicsHandle handle);
        void UnregisterByEntity(Entity entity);
    }
}