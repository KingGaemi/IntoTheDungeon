using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Core.Abstractions.Messages.Spawn
{
    /// <summary>
    /// 스폰 성공 이벤트 - 생성된 엔티티 정보 포함
    /// </summary>
    public readonly struct SpawnEvent
    {
        public int RecipeId { get; }
        public Vec2 Pos { get; }
        public Vec2 Dir { get; }
        public uint Priority { get; }
        public Entity Context { get; }

        /// <summary>
        /// 생성된 엔티티
        /// </summary>
        public Entity SpawnedEntity { get; }

        /// <summary>
        /// 생성 시각 (프레임 또는 타임스탬프)
        /// </summary>

        public SpawnEvent(
            Entity spawnedEntity,
            int recipeId,
            Vec2 pos,
            Vec2 dir,
            uint priority,
            Entity context)
        {
            SpawnedEntity = spawnedEntity;
            RecipeId = recipeId;
            Pos = pos;
            Dir = dir;
            Priority = priority;
            Context = context;
        }

        /// <summary>
        /// SpawnCommand로부터 생성
        /// </summary>
        public static SpawnEvent FromCommand(in SpawnCommand cmd, Entity spawnedEntity)
        {
            return new SpawnEvent(
                spawnedEntity,
                cmd.RecipeId,
                cmd.Pos,
                cmd.Dir,
                cmd.Priority,
                cmd.Context
            );
        }
    }

    /// <summary>
    /// 스폰 실패 이벤트 - 실패 원인 포함
    /// </summary>
    public readonly struct SpawnFailedEvent
    {
        public int RecipeId { get; }
        public Vec2 Pos { get; }
        public Vec2 Dir { get; }
        public uint Priority { get; }
        public Entity Context { get; }

        /// <summary>
        /// 실패 사유
        /// </summary>
        public SpawnFailReason Reason { get; }

        /// <summary>
        /// 추가 정보 (옵션)
        /// </summary>
        public string Message { get; }

        public SpawnFailedEvent(
            in SpawnCommand cmd,
            SpawnFailReason reason,
            string message = null)
        {
            RecipeId = cmd.RecipeId;
            Pos = cmd.Pos;
            Dir = cmd.Dir;
            Priority = cmd.Priority;
            Context = cmd.Context;
            Reason = reason;
            Message = message;
        }

        public static SpawnFailedEvent FromCommand(in SpawnCommand cmd, SpawnFailReason reason, string message = null)
        {
            return new SpawnFailedEvent(cmd, reason, message);
        }
    }

    /// <summary>
    /// 스폰 실패 사유
    /// </summary>
    public enum SpawnFailReason : byte
    {
        None = 0,
        RecipeNotFound = 1,
        InvalidContext = 2,
        InsufficientResources = 3,
        Cooldown = 4,
        PermissionDenied = 5,
        BufferFull = 6,
        TTLExpired = 7,
        DuplicateRequest = 8,
        ValidationFailed = 9,
        SystemError = 10
    }

    /// <summary>
    /// 스폰 취소 명령
    /// </summary>
    public readonly struct CancelSpawnCommand
    {
        public uint RequestId { get; }

        public CancelSpawnCommand(uint requestId)
        {
            RequestId = requestId;
        }
    }
}