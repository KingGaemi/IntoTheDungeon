using System.Collections.Generic;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Physics.Abstractions;

namespace IntoTheDungeon.Core.Physics.Implementation
{
    public sealed class HandleOwnerIndex : IHandleOwnerIndex
    {

        static long EKey(Entity e) => ((long)e.Version << 32) | (uint)e.Index;
        static long HKey(PhysicsHandle h) => ((long)h.Version << 32) | (uint)h.Index;

        readonly Dictionary<long, Entity> _h2e = new(512);
        readonly Dictionary<long, PhysicsHandle> _e2h = new(512);

        public void Register(PhysicsHandle h, Entity e)
        {
            _h2e[HKey(h)] = e;
            _e2h[EKey(e)] = h;
        }

        public bool TryGetOwner(PhysicsHandle h, out Entity e)
        {
            if (_h2e.TryGetValue(HKey(h), out e))
            {
                return true;
            }
            e = default; return false;
        }

        public bool TryGetHandle(Entity e, out PhysicsHandle h)
        {
            if (_e2h.TryGetValue(EKey(e), out h))
            {
                // 핸들 검증(선택: BodyStore에 질의)
                return true;
            }
            h = default; return false;
        }


        public void UnregisterByHandle(PhysicsHandle h)
        {
            var hk = HKey(h);
            if (_h2e.TryGetValue(hk, out var e))
            {
                _h2e.Remove(hk);
                _e2h.Remove(EKey(e));
            }
        }
        public void UnregisterByEntity(Entity e)
        {
            var ek = EKey(e);
            if (_e2h.TryGetValue(ek, out var h))
            {
                _e2h.Remove(ek);
                _h2e.Remove(HKey(h));
            }
        }
    }

}