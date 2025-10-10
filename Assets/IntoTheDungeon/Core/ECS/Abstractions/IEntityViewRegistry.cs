namespace IntoTheDungeon.Core.ECS.Abstractions
{
    public interface IEntityViewRegistry
    {
        void Register(Entity e, UnityEngine.GameObject go);
        bool TryGetView(Entity e, out UnityEngine.GameObject go);
        bool TryGetEntity(UnityEngine.GameObject go, out Entity e);
        bool Unregister(Entity e);
    }
}