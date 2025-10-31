using System.Collections.Generic;

namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface IPhysicsBodyStore
    {
        PhysicsHandle Reserve();
        PhysicsHandle Add(IPhysicsBody body);          // 핸들 발급
        IPhysicsBody Get(PhysicsHandle handle);        // O(1)

        void Bind(PhysicsHandle h, IPhysicsBody body);
        bool TryGet(PhysicsHandle handle, out IPhysicsBody body);
        void Remove(PhysicsHandle handle);
        public IEnumerable<PhysicsHandle> AllHandles();
    }
}
