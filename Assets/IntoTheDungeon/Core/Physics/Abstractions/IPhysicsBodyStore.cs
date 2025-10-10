namespace IntoTheDungeon.Core.Physics.Abstractions
{
    public interface IPhysicsBodyStore
    {
        int Add(IPhysicsBody body);          // 핸들 발급
        IPhysicsBody Get(int handle);        // O(1)
        bool TryGet(int handle, out IPhysicsBody body);
        void Remove(int handle);
    }
}
