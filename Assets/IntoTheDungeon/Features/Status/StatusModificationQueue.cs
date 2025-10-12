
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using IntoTheDungeon.Core.Collections;
using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Features.Status
{

    [StructLayout(LayoutKind.Sequential)]
    public struct StatusModificationQueue : IComponentData
    {
        RingBuffer8<StatusModification> _buffer;

        public readonly int Count => _buffer.Count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Enqueue(in StatusModification modification)
            => _buffer.Enqueue(in modification);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out StatusModification modification)
            => _buffer.TryDequeue(out modification);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _buffer.Clear();
    }

}