using IntoTheDungeon.Core.ECS.Abstractions.Scheduling;
using IntoTheDungeon.Core.ECS.Systems;
using IntoTheDungeon.Core.Physics.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Physics.Implementation
{
    /// <summary>
    /// PhysicsOpQueue를 읽어서 최적화/병합 후 PhysicsOpStore에 저장하는 시스템
    /// </summary>
    public sealed class PhysicsOpResolveSystem : IPhysicsOpResolveSystem
    {
        readonly IPhysicsOpQueue _queue;
        readonly PhysicsOpStore _store;

        // 임시 작업 공간
        readonly System.Collections.Generic.Dictionary<PhysicsHandle, int> _lastOpIndex;
        static readonly int[] OpPriority;

        static PhysicsOpResolveSystem()
        {
            OpPriority = new int[10];
            OpPriority[(int)PhysicsOpKind.AddForce] = 0;
            OpPriority[(int)PhysicsOpKind.AddImpulse] = 1;
            OpPriority[(int)PhysicsOpKind.AddTorque] = 2;
            OpPriority[(int)PhysicsOpKind.SetLinearVelocity] = 3;
            OpPriority[(int)PhysicsOpKind.SetAngularVelocity] = 4;
            OpPriority[(int)PhysicsOpKind.SetTransform] = 5;
            OpPriority[(int)PhysicsOpKind.Teleport] = 6;
            OpPriority[(int)PhysicsOpKind.SyncEcsToPhys] = 7;
            OpPriority[(int)PhysicsOpKind.SyncPhysToEcs] = 8;
        }

        public PhysicsOpResolveSystem(IPhysicsOpQueue queue, PhysicsOpStore store)
        {
            _queue = queue;
            _store = store;
            _lastOpIndex = new System.Collections.Generic.Dictionary<PhysicsHandle, int>(256);
        }



        public void Resolve()
        {
            _store.Count = 0;
            _lastOpIndex.Clear();

            // 1. Queue에서 Store로 복사
            DrainQueueToStore();

            // 2. 병합 최적화
            Coalesce();

            // 3. 우선순위 정렬
            SortByPriority();

            // 4. Queue 클리어
            _queue.ClearFrame();
        }

        void DrainQueueToStore()
        {
            var handles = _queue.Handles;
            var kinds = _queue.Kinds;
            var flags = _queue.Flags;
            var vecs = _queue.Vecs;
            var angles = _queue.Angles;

            int count = handles.Length;
            EnsureStoreCapacity(_store.Count + count);

            for (int i = 0; i < count; i++)
            {
                int idx = _store.Count++;
                _store.Handles[idx] = handles[i];
                _store.Kinds[idx] = kinds[i];
                _store.Flags[idx] = flags[i];
                _store.Vec[idx] = vecs[i];
                _store.Angle[idx] = angles[i];
            }
        }

        void Coalesce()
        {
            if (_store.Count <= 1) return;

            _lastOpIndex.Clear();
            int write = 0;

            for (int i = 0; i < _store.Count; i++)
            {
                var handle = _store.Handles[i];
                var kind = _store.Kinds[i];

                if (kind == PhysicsOpKind.None) continue;

                // 병합 가능한 작업은 최신 것만 유지
                if (CanCoalesce(kind))
                {
                    int key = CombineKey(handle, kind);
                    if (_lastOpIndex.TryGetValue(handle, out int prevIdx))
                    {
                        var prevKind = _store.Kinds[prevIdx];
                        if (prevKind == kind)
                        {
                            _store.Kinds[prevIdx] = PhysicsOpKind.None;
                        }
                    }
                }

                if (!_lastOpIndex.ContainsKey(handle))
                {
                    _lastOpIndex[handle] = i;
                }
            }

            // 압축
            for (int i = 0; i < _store.Count; i++)
            {
                if (_store.Kinds[i] == PhysicsOpKind.None) continue;

                if (i != write)
                {
                    _store.Handles[write] = _store.Handles[i];
                    _store.Kinds[write] = _store.Kinds[i];
                    _store.Flags[write] = _store.Flags[i];
                    _store.Vec[write] = _store.Vec[i];
                    _store.Angle[write] = _store.Angle[i];
                }
                write++;
            }

            _store.Count = write;
        }

        void SortByPriority()
        {
            if (_store.Count <= 1) return;

            // 같은 핸들끼리 우선순위 정렬 (Insertion Sort - 안정 정렬)
            for (int i = 1; i < _store.Count; i++)
            {
                var handle = _store.Handles[i];
                var kind = _store.Kinds[i];
                var flags = _store.Flags[i];
                var vec = _store.Vec[i];
                var angle = _store.Angle[i];

                int priority = OpPriority[(int)kind];
                int j = i - 1;

                while (j >= 0 &&
                       _store.Handles[j] == handle &&
                       OpPriority[(int)_store.Kinds[j]] > priority)
                {
                    _store.Handles[j + 1] = _store.Handles[j];
                    _store.Kinds[j + 1] = _store.Kinds[j];
                    _store.Flags[j + 1] = _store.Flags[j];
                    _store.Vec[j + 1] = _store.Vec[j];
                    _store.Angle[j + 1] = _store.Angle[j];
                    j--;
                }

                _store.Handles[j + 1] = handle;
                _store.Kinds[j + 1] = kind;
                _store.Flags[j + 1] = flags;
                _store.Vec[j + 1] = vec;
                _store.Angle[j + 1] = angle;
            }
        }

        bool CanCoalesce(PhysicsOpKind kind)
        {
            return kind == PhysicsOpKind.SetTransform
                || kind == PhysicsOpKind.Teleport
                || kind == PhysicsOpKind.SetLinearVelocity
                || kind == PhysicsOpKind.SetAngularVelocity;
        }

        int CombineKey(PhysicsHandle handle, PhysicsOpKind kind)
        {
            return handle.Index * 1000 + (int)kind;
        }

        void EnsureStoreCapacity(int required)
        {
            if (_store.Handles.Length >= required) return;

            int newCap = _store.Handles.Length * 2;
            if (newCap < required) newCap = required;

            System.Array.Resize(ref _store.Handles, newCap);
            System.Array.Resize(ref _store.Kinds, newCap);
            System.Array.Resize(ref _store.Flags, newCap);
            System.Array.Resize(ref _store.Vec, newCap);
            System.Array.Resize(ref _store.Angle, newCap);
        }
    }
}