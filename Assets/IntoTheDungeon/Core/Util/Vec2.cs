namespace IntoTheDungeon.Core.Util
{
    public struct Vec2
    {
        public float X;
        public float Y;
        public Vec2(float x, float y) { X = x; Y = y; }

        // 크기
        public readonly float Magnitude => (float)System.Math.Sqrt(X * X + Y * Y);
        public readonly float SqrMagnitude => X * X + Y * Y;

        // 정규화
        public readonly Vec2 Normalized => Magnitude > 1e-5f ? this / Magnitude : new Vec2(0, 0);


        public static Vec2 Zero => new(0, 0);
        public static Vec2 One => new(1, 1);
        public static Vec2 Up => new(0, 1);
        public static Vec2 Down => new(0, -1);
        public static Vec2 Left => new(-1, 0);
        public static Vec2 Right => new(1, 0);

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

        // -------- 유틸리티 메서드 -------
        public static float Distance(Vec2 a, Vec2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            return Mathx.Sqrt(dx * dx + dy * dy);
        }

        // 거리 제곱 (제곱근 연산 생략 - 성능 최적화)
        public static float DistanceSquared(Vec2 a, Vec2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            return dx * dx + dy * dy;
        }

        // 두 벡터의 방향 관계
        // 결과 > 0: 예각, = 0: 직각, < 0: 둔각    
        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;


        // 2D에서는 스칼라 값 (Z축 성분)
        // 결과 > 0: b가 a의 왼쪽, < 0: b가 a의 오른쪽
        public static float Cross(Vec2 a, Vec2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }
        // 두 벡터 사이의 각도 (라디안)
        public static float Angle(Vec2 a, Vec2 b)
        {
            float dot = Dot(a.Normalized, b.Normalized);
            return Mathx.Acos(Mathx.Clamp(dot, -1f, 1f));
        }

        // 두 벡터 사이의 각도 (도)
        public static float AngleDegrees(Vec2 a, Vec2 b)
        {
            return Angle(a, b) * Mathx.Rad2Deg;
        }

        // 벡터의 절대 각도 (라디안)
        public readonly float ToAngle()
        {
            return Mathx.Atan2(Y, X);
        }

        // 각도로부터 벡터 생성 (라디안)
        public static Vec2 FromAngle(float radians)
        {
            return new Vec2(Mathx.Cos(radians), Mathx.Sin(radians));
        }

        // ============ 보간 ============

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t)
        {
            t = Mathx.Clamp01(t);
            return new Vec2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }

        // ============ 투영 ============

        // a를 b 방향으로 투영
        public static Vec2 Project(Vec2 a, Vec2 b)
        {
            float sqrMag = b.SqrMagnitude;
            if (sqrMag < 0.00001f) return Zero;

            float dot = Dot(a, b);
            return b * (dot / sqrMag);
        }

        // ============ 반사 ============

        // normal을 기준으로 반사
        public static Vec2 Reflect(Vec2 direction, Vec2 normal)
        {
            float dot = Dot(direction, normal);
            return direction - normal * (2f * dot);
        }

        public readonly override string ToString() => $"({X}, {Y})";
    }
}