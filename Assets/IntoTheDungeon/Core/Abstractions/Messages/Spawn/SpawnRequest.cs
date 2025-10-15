namespace IntoTheDungeon.Core.Abstractions.Messages.Spawn
{
    public struct SpawnRequest
    {
        public RecipeId RecipeId;
        public SpawnParams Params;
        // public Entity? AssignTo; // 선택: 미리 할당할 엔티티
    }
}