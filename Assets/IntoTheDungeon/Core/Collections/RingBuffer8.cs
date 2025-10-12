using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace IntoTheDungeon.Core.Collections
{
    /// <summary>
    /// 고정 크기 8 링버퍼 (제네릭, 값형 전용)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RingBuffer8<T> where T : unmanaged
    {
        const int Cap = 8;
        const int Mask = Cap - 1;

        int _count;
        int _head;

        T _s0, _s1, _s2, _s3, _s4, _s5, _s6, _s7;

        public readonly int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count == 0;
        }

        public readonly bool IsFull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count >= Cap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Enqueue(in T value)
        {
            if (_count >= Cap) return false;
            Set((_head + _count) & Mask, in value);
            _count++;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T value)
        {
            if (_count == 0)
            {
                value = default;
                return false;
            }
            Get(_head, out value);
            _head = (_head + 1) & Mask;
            _count--;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool TryPeek(out T value)
        {
            if (_count == 0)
            {
                value = default;
                return false;
            }
            Get(_head, out value);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            _count = 0;
            _head = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void Set(int i, in T v)
        {
            switch (i)
            {
                case 0: _s0 = v; break;
                case 1: _s1 = v; break;
                case 2: _s2 = v; break;
                case 3: _s3 = v; break;
                case 4: _s4 = v; break;
                case 5: _s5 = v; break;
                case 6: _s6 = v; break;
                default: _s7 = v; break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly void Get(int i, out T v)
        {
            v = i switch
            {
                0 => _s0,
                1 => _s1,
                2 => _s2,
                3 => _s3,
                4 => _s4,
                5 => _s5,
                6 => _s6,
                _ => _s7
            };
        }

        #if DEBUG
        public readonly int DebugHead => _head;
        public readonly int DebugCapacity => Cap;
        #endif
    }
}