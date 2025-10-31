using System.Collections.Generic;
using System.Diagnostics;
using IntoTheDungeon.Core.Physics.Abstractions;
namespace IntoTheDungeon.Core.Physics.Implementation
{
    public sealed class PhysicsBodyStore : IPhysicsBodyStore
    {
        private readonly List<IPhysicsBody> _bodies = new();
        private readonly List<ushort> _versions = new();
        private readonly Stack<int> _free = new();

        public PhysicsHandle Reserve()
        {
            if (_free.Count > 0)
            {
                var idx = _free.Pop();
                // 슬롯은 이미 null이어야 함
                return new PhysicsHandle(idx, _versions[idx]);
            }
            _bodies.Add(null);
            _versions.Add(0);
            return new PhysicsHandle(_bodies.Count - 1, 0);
        }
        public void Bind(PhysicsHandle h, IPhysicsBody body)
        {
            var i = h.Index;
            if (i < 0 || i >= _bodies.Count) throw new System.IndexOutOfRangeException();
            if (_bodies[i] != null) throw new System.InvalidOperationException("Slot already bound.");
            // 요청 핸들의 버전이 현재 버전과 같을 때만 바인드 허용
            if (_versions[i] != h.Version) throw new System.InvalidOperationException("Stale handle.");
            _bodies[i] = body;

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"Body {i}");
#endif
        }
        public PhysicsHandle Add(IPhysicsBody body)
        {
            var h = Reserve();
            _bodies[h.Index] = body;
            return h; // 버전은 Reserve 당시 값
        }

        public IPhysicsBody Get(PhysicsHandle h)
        {
            if (!TryGet(h, out var b)) throw new System.InvalidOperationException("Invalid handle.");
            return b;
        }


        public bool TryGet(PhysicsHandle h, out IPhysicsBody b)
        {
            b = null!;
            var i = h.Index;
            if (i < 0 || i >= _bodies.Count) return false;
            if (_versions[i] != h.Version) return false; // ABA 차단
            var x = _bodies[i];
            if (x == null) return false;
            b = x; return true;
        }

        public void Remove(PhysicsHandle h)
        {
            var i = h.Index;
            if (i < 0 || i >= _bodies.Count) return;
            // 버전 검증 후 제거
            if (_versions[i] != h.Version) return; // 이미 다른 세대가 사용 중
            _bodies[i] = null;
            unchecked { _versions[i]++; } // 세대 증가 → 이전 핸들 전부 무효화
            _free.Push(i);
        }
        public IEnumerable<PhysicsHandle> AllHandles()
        {
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                if (body == null) continue;
                yield return new PhysicsHandle(i, _versions[i]);
            }
        }
    }
}