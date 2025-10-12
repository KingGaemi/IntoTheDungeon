namespace IntoTheDungeon.Core.Abstractions.Services
{public readonly struct BufferResizeInfo
    {
        public string TypeName { get; }
        public int OldCapacity { get; }
        public int NewCapacity { get; }
        public int ResizeCount { get; }

        // 생성자 추가
        public BufferResizeInfo(string typeName, int oldCapacity, int newCapacity, int resizeCount)
        {
            TypeName = typeName;
            OldCapacity = oldCapacity;
            NewCapacity = newCapacity;
            ResizeCount = resizeCount;
        }

        public override string ToString()
        {
            return $"[EventHub] {TypeName} buffer resized #{ResizeCount}: " +
                   $"{OldCapacity} → {NewCapacity}";
        }
    }
}