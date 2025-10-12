using System;
using System.Collections.Generic;
using IntoTheDungeon.Core.Abstractions.Services;


namespace IntoTheDungeon.Core.Runtime.Event
{
    public sealed class EventHub : IEventHub
    {
        readonly Dictionary<Type, IEventBuffer> _buffers = new(32);

        #if DEBUG
        readonly Dictionary<Type, EventStats> _stats = new();
        #endif

        public void Publish<T>(in T ev) where T : unmanaged
        {
            var buffer = GetBuffer<T>();
            buffer.Add(ev);

            #if DEBUG
            TrackStats<T>(buffer.Count);
            #endif
        }

        public ReadOnlySpan<T> Consume<T>() where T : unmanaged
        {
            return GetBuffer<T>().AsSpan();
        }

        public void ClearFrame()
        {
            foreach (var buffer in _buffers.Values)
                buffer.Clear();
        }

        public void Clear<T>() where T : unmanaged
        {
            if (_buffers.TryGetValue(typeof(T), out var buffer))
                buffer.Clear();
        }

        public int Count<T>() where T : unmanaged
        {
            return _buffers.TryGetValue(typeof(T), out var buffer) 
                ? buffer.Count 
                : 0;
        }

        public bool HasEvents<T>() where T : unmanaged
        {
            return Count<T>() > 0;
        }

        EventBuffer<T> GetBuffer<T>() where T : unmanaged
        {
            if (!_buffers.TryGetValue(typeof(T), out var buffer))
            {
                var typed = new EventBuffer<T>(256);
                _buffers[typeof(T)] = typed;
                return typed;
            }
            return (EventBuffer<T>)buffer;
        }

        #region Debug Statistics

        #if DEBUG
        void TrackStats<T>(int currentCount)
        {
            var type = typeof(T);
            if (!_stats.TryGetValue(type, out var stats))
            {
                stats = new EventStats();
                _stats[type] = stats;
            }
            
            stats.Update(currentCount);
        }

        public string GetStatistics()
        {
            if (_stats.Count == 0)
                return "No event statistics available.";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Event Buffer Statistics ===");
            
            foreach (var kvp in _stats)
            {
                var stats = kvp.Value;
                sb.AppendLine($"{kvp.Key.Name}:");
                sb.AppendLine($"  Peak: {stats.Peak}");
                sb.AppendLine($"  Average: {stats.Average:F1}");
                sb.AppendLine($"  Total Frames: {stats.TotalSamples}");
                sb.AppendLine($"  Recommended Capacity: {stats.RecommendedCapacity}");
                sb.AppendLine();
            }
            
            return sb.ToString();
        }

        public EventStatistics? GetStatistics<T>() where T : unmanaged
        {
            if (!_stats.TryGetValue(typeof(T), out var stats))
                return null;

            return new EventStatistics(
                typeof(T).Name,
                stats.Peak,
                stats.Average,
                stats.TotalSamples,
                stats.RecommendedCapacity
            );
        }

        sealed class EventStats
        {
            public int Peak;
            public long TotalSamples;
            public long SumCounts;
            
            public float Average => TotalSamples > 0 ? (float)SumCounts / TotalSamples : 0;
            public int RecommendedCapacity => NextPowerOfTwo((int)(Peak * 1.5f));

            public void Update(int count)
            {
                if (count > Peak) Peak = count;
                SumCounts += count;
                TotalSamples++;
            }

            static int NextPowerOfTwo(int n)
            {
                if (n <= 0) return 1;
                n--;
                n |= n >> 1;
                n |= n >> 2;
                n |= n >> 4;
                n |= n >> 8;
                n |= n >> 16;
                return n + 1;
            }
        }
        #endif

        #endregion

        #region Buffer Implementation

        interface IEventBuffer
        {
            int Count { get; }
            void Clear();
        }

        sealed class EventBuffer<T> : IEventBuffer where T : unmanaged
        {
            T[] _items;
            int _count;
            int _resizeCount;

            public int Count => _count;

            public EventBuffer(int initialCapacity)
            {
                _items = new T[initialCapacity];
            }

            public void Add(T item)
            {
                if (_count == _items.Length)
                {
                    int oldCapacity = _items.Length;
                    int newCapacity = oldCapacity * 2;
                    Array.Resize(ref _items, newCapacity);
                    _resizeCount++;
                    
                    #if DEBUG
                    EventHubDiagnostics.NotifyBufferResized(new BufferResizeInfo(
                        typeof(T).Name,
                        oldCapacity,
                        newCapacity,
                        _resizeCount
                    ));
                    #endif
                }
                
                _items[_count++] = item;
            }

            public ReadOnlySpan<T> AsSpan() 
                => new ReadOnlySpan<T>(_items, 0, _count);

            public void Clear() => _count = 0;
        }

        #endregion
    }

    #region Public API

    

    #endregion

    #region Debug Types

    #if DEBUG
    /// <summary>
    /// 이벤트 통계 정보 (읽기 전용 구조체)
    /// </summary>

    /// <summary>
    /// 버퍼 리사이즈 정보
    /// </summary>
    

    /// <summary>
    /// 버퍼 리사이즈 진단 (외부 로깅 시스템 연결용)
    /// </summary>

    #endif

    #endregion
}