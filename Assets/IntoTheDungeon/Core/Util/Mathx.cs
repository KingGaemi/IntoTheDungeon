namespace IntoTheDungeon.Core.Util.Maths
{
    public static class Mathx
    {
        public const float Epsilon = 1e-5f;
        public const float Rad2Deg = 57.29577951308232f;  // 180 / π
        public const float Deg2Rad = 0.017453292519943295f; 

        // --- Approximately ---
        public static bool Approximately(float a, float b, float eps = Epsilon)
            => System.MathF.Abs(a - b) <= eps *
               System.MathF.Max(1f, System.MathF.Max(System.MathF.Abs(a), System.MathF.Abs(b)));

        public static bool Approximately(double a, double b, double eps = 1e-9)
            => System.Math.Abs(a - b) <= eps *
               System.Math.Max(1d, System.Math.Max(System.Math.Abs(a), System.Math.Abs(b)));

        // --- Clamp (정수/실수 분리) ---

        public static float Sqrt(float v)
            => System.MathF.Sqrt(v);
        public static float Acos(float v)
            => System.MathF.Acos(v);
        public static float Sin(float r)
            => System.MathF.Sin(r);
        public static float Cos(float r)
            => System.MathF.Cos(r);
        public static float Atan2(float a, float b)
            => System.MathF.Atan2(a, b);


        public static float Abs(float v)
            => System.MathF.Abs(v);
        public static int Abs(int v)
            => System.Math.Abs(v);
        public static int Clamp(int v, int min, int max)
            => System.Math.Min(System.Math.Max(v, min), max);

        public static float Clamp(float v, float min, float max)
            => System.MathF.Min(System.MathF.Max(v, min), max);

        public static double Clamp(double v, double min, double max)
            => System.Math.Min(System.Math.Max(v, min), max);

        // 자주 쓰는 유틸
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static int   Saturate01(int v) => Clamp(v, 0, 1);

        public static float Max(float a, float b) => System.MathF.Max(a, b);
        public static int   Max(int a, int b)     => System.Math.Max(a, b);

        public static float Min(float a, float b) => System.MathF.Min(a, b);
        public static int   Min(int a, int b)     => System.Math.Min(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float InverseLerp(float a, float b, float v)
            => (b - a) != 0f ? Clamp01((v - a) / (b - a)) : 0f;
    }
}
