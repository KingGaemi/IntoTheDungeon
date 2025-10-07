using IntoTheDungeon.Core.Util.Physics;
using IntoTheDungeon.Core.Util.Maths;
namespace IntoTheDungeon.Core.Physics.Collision
{
    public static class PhysicsUtility
    {
        // AABB 구조체
        public struct AABB
        {
            public Vec2 Min;  // 좌하단
            public Vec2 Max;  // 우상단

            public static AABB FromCenterSize(Vec2 center, Vec2 size)
            {
                Vec2 halfSize = size * 0.5f;
                return new AABB
                {
                    Min = center - halfSize,
                    Max = center + halfSize
                };
            }

            public float Width => Max.X - Min.X;
            public float Height => Max.Y - Min.Y;
        }

        // AABB vs AABB 충돌 검사
        public static bool AABBIntersects(Vec2 centerA, Vec2 sizeA, Vec2 centerB, Vec2 sizeB)
        {
            // Half-size (반지름 개념)
            Vec2 halfA = sizeA * 0.5f;
            Vec2 halfB = sizeB * 0.5f;

            // 각 축별로 겹치는지 확인
            bool xOverlap = Mathx.Abs(centerA.X - centerB.X) <= (halfA.X + halfB.X);
            bool yOverlap = Mathx.Abs(centerA.Y - centerB.Y) <= (halfA.Y + halfB.Y);

            return xOverlap && yOverlap;
        }

        // Circle vs AABB 충돌 검사
        public static bool CircleAABBIntersects(
            Vec2 circleCenter, float radius,
            Vec2 boxCenter, Vec2 boxSize)
        {
            Vec2 halfSize = boxSize * 0.5f;

            // 원의 중심에서 가장 가까운 박스 위의 점 찾기
            Vec2 closestPoint = new Vec2(
                Mathx.Clamp(circleCenter.X,
                            boxCenter.X - halfSize.X,
                            boxCenter.X + halfSize.X),
                Mathx.Clamp(circleCenter.Y,
                            boxCenter.Y - halfSize.Y,
                            boxCenter.Y + halfSize.Y)
            );

            // 그 점과 원 중심의 거리가 반지름보다 작으면 충돌
            float distSq = Vec2.DistanceSquared(circleCenter, closestPoint);
            return distSq <= radius * radius;
        }

        // Point in AABB 테스트
        public static bool PointInAABB(Vec2 point, Vec2 boxCenter, Vec2 boxSize)
        {
            Vec2 halfSize = boxSize * 0.5f;
            return point.X >= boxCenter.X - halfSize.X &&
                point.X <= boxCenter.X + halfSize.X &&
                point.Y >= boxCenter.Y - halfSize.Y &&
                point.Y <= boxCenter.Y + halfSize.Y;
        }
    }
}
