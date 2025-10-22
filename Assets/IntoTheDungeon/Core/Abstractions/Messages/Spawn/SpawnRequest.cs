using IntoTheDungeon.Core.ECS.Abstractions;

namespace IntoTheDungeon.Core.Abstractions.Messages.Spawn
{
    public enum SpawnSource { System, Entity }
    public readonly struct SpawnOrder
    {
        public readonly RecipeId RecipeId;
        public readonly SpawnSpec Spec;
        public readonly SpawnSource Source;
        public readonly Entity Origin;
        public SpawnOrder(RecipeId id, in SpawnSpec spec, SpawnSource src, Entity origin = default)
        => (RecipeId, Spec, Source, Origin) = (id, spec, src, origin);
    }
}