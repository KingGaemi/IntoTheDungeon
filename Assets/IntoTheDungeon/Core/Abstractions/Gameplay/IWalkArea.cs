using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Abstractions.Gameplay
{
    /// <summary>
    /// 2.5D 바닥 평면에서 걸을 수 있는 영역. MotionSystem이 이동 후 위치를 이 안으로 제한한다.
    /// </summary>
    public interface IWalkArea
    {
        Vec2 Clamp(Vec2 groundPos);
    }

    /// <summary>가로·깊이 범위로 정한 사각형 영역.</summary>
    public sealed class RectWalkArea : IWalkArea
    {
        public readonly float MinX, MaxX, MinDepth, MaxDepth;

        public RectWalkArea(float minX, float maxX, float minDepth, float maxDepth)
        { MinX = minX; MaxX = maxX; MinDepth = minDepth; MaxDepth = maxDepth; }

        public Vec2 Clamp(Vec2 p)
            => new(Mathx.Clamp(p.X, MinX, MaxX), Mathx.Clamp(p.Y, MinDepth, MaxDepth));
    }
}
