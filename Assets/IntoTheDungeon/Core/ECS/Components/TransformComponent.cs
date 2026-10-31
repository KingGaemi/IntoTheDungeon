using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.ECS.Components
{
    /// <summary>
    /// 2.5D 좌표. Position은 바닥 평면(X = 가로, Y = 깊이: 클수록 화면 안쪽/위),
    /// Height는 바닥에서 떠 있는 높이(점프·띄우기). 화면 투영은 View가 한다.
    /// </summary>
    public struct TransformComponent : IComponentData
    {
        public Vec2 Position;
        public float Height;
        public Vec2 Direction;

        public float Rotation; // Radian
        public bool Moved; // for View
    }
}
