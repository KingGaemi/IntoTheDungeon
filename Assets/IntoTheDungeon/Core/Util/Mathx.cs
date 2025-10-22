using System;

namespace IntoTheDungeon.Core.Util
{
    public static class Mathx
    {
        // 상수
        public const float Epsilon = 1e-5f;
        public const float Rad2Deg = 57.29577951308232f;
        public const float Deg2Rad = 0.017453292519943295f;

        public static float Clamp(float v, float min, float max)
        => MathF.Min(MathF.Max(v, min), max);

        public static int Clamp(int v, int min, int max)
            => Math.Min(Math.Max(v, min), max);
        public static float Clamp01(float v)
            => MathF.Min(MathF.Max(v, 0f), 1f);

        public static bool Approximately(float a, float b, float eps = Epsilon)
            => MathF.Abs(a - b) <= eps * MathF.Max(1f, MathF.Max(MathF.Abs(a), MathF.Abs(b)));

        public static float Lerp(float a, float b, float t)
            => a + (b - a) * Clamp01(t);

        public static float InverseLerp(float a, float b, float v)
            => (b - a) != 0f ? Clamp01((v - a) / (b - a)) : 0f;
        public static int RoundToInt(float f)
       => (int)MathF.Round(f, MidpointRounding.AwayFromZero);
    }
}
