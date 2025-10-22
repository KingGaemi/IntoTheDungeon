using IntoTheDungeon.Core.Abstractions.Messages.Spawn;

namespace IntoTheDungeon.Core.Abstractions.Types
{
    public static class RecipeIds
    {
        public static readonly RecipeId Default = new(0x1000);
        public static readonly RecipeId Character = new(0x1001);
        public static readonly RecipeId Projectile = new(0x1002);
        // 공용/핵심만 여기에
    }

}