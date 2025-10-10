using IntoTheDungeon.Core.Physics.Abstractions;
namespace IntoTheDungeon.Core.Physics.Implementation
{
    public sealed class PhysicsBodyStore : IPhysicsBodyStore
    {
        private readonly System.Collections.Generic.List<IPhysicsBody> _arr = new();
        private readonly System.Collections.Generic.Stack<int> _free = new();

        public int Add(IPhysicsBody body)
        {
            if (_free.Count > 0) { var h = _free.Pop(); _arr[h] = body; return h; }
            _arr.Add(body); return _arr.Count - 1;
        }
        public IPhysicsBody Get(int h) => _arr[h]!;
        public bool TryGet(int h, out IPhysicsBody b) { var x = _arr[h]; b = x!; return x != null; }
        public void Remove(int h) { _arr[h] = null; _free.Push(h); }
    }
}