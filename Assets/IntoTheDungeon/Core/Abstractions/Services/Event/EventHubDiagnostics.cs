
using System;

namespace IntoTheDungeon.Core.Abstractions.Services
{
    public static class EventHubDiagnostics
    {
        // ✅ 이벤트 선언 추가
        public static event Action<BufferResizeInfo> BufferResized;

        internal static void NotifyBufferResized(BufferResizeInfo info)
        {
            BufferResized?.Invoke(info);
        }
    }
}