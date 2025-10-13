using System;

namespace IntoTheDungeon.Core.Util
{
    public struct Vec2 : IEquatable<Vec2>
    {
        public float X;
        public float Y;

        public Vec2(float x, float y) { X = x; Y = y; }

        // 크기
        public readonly float Magnitude => MathF.Sqrt(X * X + Y * Y);
        public readonly float SqrMagnitude => X * X + Y * Y;

        // 정규화
        public readonly Vec2 Normalized => Magnitude > 1e-5f ? this / Magnitude : new Vec2(0, 0);

        public static Vec2 Zero => new(0, 0);
        public static Vec2 One => new(1, 1);
        public static Vec2 Up => new(0, 1);
        public static Vec2 Down => new(0, -1);
        public static Vec2 Left => new(-1, 0);
        public static Vec2 Right => new(1, 0);

        // -------- 산술 연산자 --------

        public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float d) => new(a.X * d, a.Y * d);
        public static Vec2 operator *(float d, Vec2 a) => new(a.X * d, a.Y * d);
        public static Vec2 operator /(Vec2 a, float d) => new(a.X / d, a.Y / d);
        public static Vec2 operator -(Vec2 a) => new(-a.X, -a.Y);

        // -------- 비교 연산자 --------

        /// <summary>
        /// 정확한 동등 비교 (부동소수점 오차 무시)
        /// </summary>
        public static bool operator ==(Vec2 lhs, Vec2 rhs)
        {
            return lhs.X == rhs.X && lhs.Y == rhs.Y;
        }

        public static bool operator !=(Vec2 lhs, Vec2 rhs)
        {
            return !(lhs == rhs);
        }

        // -------- IEquatable<Vec2> 구현 --------

        public readonly bool Equals(Vec2 other)
        {
            return X == other.X && Y == other.Y;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Vec2 other && Equals(other);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        // -------- 근사 비교 (부동소수점 오차 허용) --------

        /// <summary>
        /// 오차 범위 내에서 같은지 비교 (기본 epsilon: 0.00001f)
        /// </summary>
        public readonly bool ApproximatelyEquals(Vec2 other, float epsilon = 0.00001f)
        {
            return MathF.Abs(X - other.X) < epsilon &&
                   MathF.Abs(Y - other.Y) < epsilon;
        }

        /// <summary>
        /// 거리 기반 근사 비교
        /// </summary>
        public readonly bool IsNear(Vec2 other, float threshold = 0.01f)
        {
            return DistanceSquared(this, other) < threshold * threshold;
        }

        // -------- 유틸리티 메서드 --------

        public static float Distance(Vec2 a, Vec2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public static float DistanceSquared(Vec2 a, Vec2 b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            return dx * dx + dy * dy;
        }

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static float Cross(Vec2 a, Vec2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }

        public static float Angle(Vec2 a, Vec2 b)
        {
            float dot = Dot(a.Normalized, b.Normalized);
            return MathF.Acos(Mathx.Clamp(dot, -1f, 1f));
        }

        public static float AngleDegrees(Vec2 a, Vec2 b)
        {
            return Angle(a, b) * Mathx.Rad2Deg;
        }

        public readonly float ToAngle()
        {
            return MathF.Atan2(Y, X);
        }

        public static Vec2 FromAngle(float radians)
        {
            return new Vec2(MathF.Cos(radians), MathF.Sin(radians));
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

        public static Vec2 LerpUnclamped(Vec2 a, Vec2 b, float t)
        {
            return new Vec2(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t
            );
        }

        // ============ 투영 ============

        public static Vec2 Project(Vec2 a, Vec2 b)
        {
            float sqrMag = b.SqrMagnitude;
            if (sqrMag < 0.00001f) return Zero;

            float dot = Dot(a, b);
            return b * (dot / sqrMag);
        }

        // ============ 반사 ============

        public static Vec2 Reflect(Vec2 direction, Vec2 normal)
        {
            float dot = Dot(direction, normal);
            return direction - normal * (2f * dot);
        }

        // ============ 클램핑 ============

        /// <summary>
        /// 벡터의 각 성분을 min~max 범위로 제한
        /// </summary>
        public static Vec2 Clamp(Vec2 value, Vec2 min, Vec2 max)
        {
            return new Vec2(
                Mathx.Clamp(value.X, min.X, max.X),
                Mathx.Clamp(value.Y, min.Y, max.Y)
            );
        }

        /// <summary>
        /// 벡터의 크기를 maxLength로 제한
        /// </summary>
        public static Vec2 ClampMagnitude(Vec2 vector, float maxLength)
        {
            float sqrMag = vector.SqrMagnitude;
            if (sqrMag > maxLength * maxLength)
            {
                float mag = MathF.Sqrt(sqrMag);
                return vector * (maxLength / mag);
            }
            return vector;
        }

        // ============ 최소/최대 ============

        public static Vec2 Min(Vec2 a, Vec2 b)
        {
            return new Vec2(
                MathF.Min(a.X, b.X),
                MathF.Min(a.Y, b.Y)
            );
        }

        public static Vec2 Max(Vec2 a, Vec2 b)
        {
            return new Vec2(
                MathF.Max(a.X, b.X),
                MathF.Max(a.Y, b.Y)
            );
        }

        // ============ 회전 ============

        /// <summary>
        /// 벡터를 angle(라디안)만큼 회전
        /// </summary>
        public readonly Vec2 Rotate(float radians)
        {
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            return new Vec2(
                X * cos - Y * sin,
                X * sin + Y * cos
            );
        }

        /// <summary>
        /// 벡터를 90도 반시계방향 회전 (빠른 버전)
        /// </summary>
        public readonly Vec2 PerpendicularCCW() => new(-Y, X);

        /// <summary>
        /// 벡터를 90도 시계방향 회전 (빠른 버전)
        /// </summary>
        public readonly Vec2 PerpendicularCW() => new(Y, -X);

        // ============ 유틸리티 ============

        /// <summary>
        /// 벡터가 거의 0인지 확인
        /// </summary>
        public readonly bool IsZero(float epsilon = 0.00001f)
        {
            return SqrMagnitude < epsilon * epsilon;
        }

        /// <summary>
        /// 벡터가 정규화되어 있는지 확인
        /// </summary>
        public readonly bool IsNormalized(float epsilon = 0.0001f)
        {
            return MathF.Abs(SqrMagnitude - 1f) < epsilon;
        }

        public readonly override string ToString() => $"({X:F3}, {Y:F3})";
        public readonly string ToString(string format) => $"({X.ToString(format)}, {Y.ToString(format)})";
    }
}