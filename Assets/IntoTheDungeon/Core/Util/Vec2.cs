namespace IntoTheDungeon.Core.Util.Physics
{
    public struct Vec2
    {
        public float X;
        public float Y;
        public Vec2(float x, float y) { X = x; Y = y; }

        // 크기
        public readonly float Magnitude => (float)System.Math.Sqrt(X * X + Y * Y);

        // 정규화
        public Vec2 Normalized => Magnitude > 1e-5f ? this / Magnitude : new Vec2(0, 0);

        // -------- 연산자 오버로드 --------

        // Vec2 + Vec2
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        // Vec2 - Vec2
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        // Vec2 * float
        public static Vec2 operator *(Vec2 a, float d) => new Vec2(a.X * d, a.Y * d);

        // float * Vec2
        public static Vec2 operator *(float d, Vec2 a) => new Vec2(a.X * d, a.Y * d);

        // Vec2 / float
        public static Vec2 operator /(Vec2 a, float d) => new Vec2(a.X / d, a.Y / d);

        // 부호 반전
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        // -------- 유틸리티 메서드 --------

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public readonly override string ToString() => $"({X}, {Y})";
    }
}