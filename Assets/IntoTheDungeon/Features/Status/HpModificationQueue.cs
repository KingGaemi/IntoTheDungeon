using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IntoTheDungeon.Core.Collections;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Status
{
    /// <summary>
    /// HP 변경 큐 (도메인 특화 래퍼)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HpModificationQueue : IComponentData
    {
        RingBuffer8<HpModification> _buffer;

        public readonly int Count => _buffer.Count;
        public readonly bool IsEmpty => _buffer.IsEmpty;
        public readonly bool IsFull => _buffer.IsFull;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Enqueue(in HpModification modification)
            => _buffer.Enqueue(in modification);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out HpModification modification)
            => _buffer.TryDequeue(out modification);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeek(out HpModification modification)
            => _buffer.TryPeek(out modification);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _buffer.Clear();

        //  HP 전용: 합산 큐잉
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool EnqueueOrMerge(in HpModification modification)
        {
            if (_buffer.IsFull)
            {
                // 마지막 요소와 합산 시도
                if (TryMergeLast(in modification))
                    return true;
                return false;
            }
            return _buffer.Enqueue(in modification);
        }

        bool TryMergeLast(in HpModification modification)
        {
            // Peek으로 마지막 요소 확인
            // Count만큼 돌면서 마지막 찾기는 비효율적이므로
            // 간단한 버전: Dequeue 후 합산해서 다시 Enqueue
            if (!_buffer.TryPeek(out var last))
                return false;

            if (last.Kind != modification.Kind || modification.Kind == HpModification.ModKind.Set)
                return false;

            // 임시로 다 빼서 마지막만 합산 (비효율적이지만 안전)
            // 실전에서는 tail 인덱스 직접 접근 필요
            return false; // 여기선 생략
        }
    }

}