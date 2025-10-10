using IntoTheDungeon.Core.ECS.Abstractions;


namespace IntoTheDungeon.Features.Unity.Abstractions
{
    
}
public sealed class EntityViewRegistry : IEntityViewRegistry
{
    readonly System.Collections.Generic.Dictionary<Entity, UnityEngine.GameObject> e2g = new();
    readonly System.Collections.Generic.Dictionary<UnityEngine.GameObject, Entity> g2e = new();

    public void Register(Entity e, UnityEngine.GameObject go) { e2g[e] = go; g2e[go] = e; }
    public bool TryGetView(Entity e, out UnityEngine.GameObject go) => e2g.TryGetValue(e, out go);
    public bool TryGetEntity(UnityEngine.GameObject go, out Entity e) => g2e.TryGetValue(go, out e);
    public bool Unregister(Entity e) => e2g.Remove(e);
}
