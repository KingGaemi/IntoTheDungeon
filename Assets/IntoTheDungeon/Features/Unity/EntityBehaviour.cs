using UnityEngine;
using IntoTheDungeon.Core.ECS.Entities;
using IntoTheDungeon.Features.Unity;
using IntoTheDungeon.Core.Runtime.World;
public abstract class EntityBehaviour : MonoBehaviour, IEntityComponent
{
    public EntityRootBehaviour EntityRoot { get; set; }
    public GameWorld World { get; set; }
    private Entity _entity;
    private bool _entityCached;

    public Entity Entity
    {
        get
        {
            if (!_entityCached && EntityRoot != null)
            {
                _entity = EntityRoot.Entity;
                _entityCached = !_entity.Equals(default);
            }
            return _entity;
        }
    }

    private void Awake()
    {
        World = this.GetWorld();
        this.InitializeEntityRoot();
        OnAwake();
    }

    protected virtual void OnAwake() { }

    protected bool IsValid()
        => EntityRoot != null
           && !Entity.Equals(default)
           && World?.EntityManager.Exists(Entity) == true;

}