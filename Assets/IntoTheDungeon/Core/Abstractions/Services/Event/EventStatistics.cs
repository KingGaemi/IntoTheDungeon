namespace IntoTheDungeon.Core.Abstractions.Services
{
    public readonly struct EventStatistics
    {
        // ✅ 속성으로 변경 (init 사용 가능)
        public string TypeName { get; }
        public int Peak { get; }
        public float Average { get; }
        public long TotalFrames { get; }
        public int RecommendedCapacity { get; }

        // 생성자 추가
        public EventStatistics(string typeName, int peak, float average, long totalFrames, int recommendedCapacity)
        {
            TypeName = typeName;
            Peak = peak;
            Average = average;
            TotalFrames = totalFrames;
            RecommendedCapacity = recommendedCapacity;
        }

        public override string ToString()
        {
            return $"{TypeName}: Peak={Peak}, Avg={Average:F1}, " +
                   $"Frames={TotalFrames}, Recommended={RecommendedCapacity}";
        }
    }
}