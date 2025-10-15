using System;
using IntoTheDungeon.Core.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.ECS.Abstractions
{
    /// <summary>
    /// 엔티티 생성 명령 - 불변 값 타입
    /// </summary>
    public struct SpawnCommand
    {

        public int RecipeId;
        public Vec2 Pos, Dir;
        public uint Priority;      // 사용 안 하면 0
        public Entity Context;     // 소유자 등
        public byte[] Payload;     // 소형만

    }
}