using IntoTheDungeon.Core.Abstractions.Messages.Spawn;
using IntoTheDungeon.Core.ECS.Abstractions;


namespace IntoTheDungeon.Features.Unity.Abstractions
{
    public sealed class EntityViewMapRegistry : IEntityViewMapRegistry
    {
        readonly System.Collections.Generic.Dictionary<RecipeId, ViewId> e2g = new();
        readonly System.Collections.Generic.Dictionary<ViewId, RecipeId> g2e = new();

        public void Register(RecipeId e, ViewId go) { e2g[e] = go; g2e[go] = e; }
        public bool TryGetView(RecipeId e, out ViewId go) => e2g.TryGetValue(e, out go);
        public bool TryGetEntity(ViewId go, out RecipeId e) => g2e.TryGetValue(go, out e);
        public bool Unregister(RecipeId e) => e2g.Remove(e);
    }
}
