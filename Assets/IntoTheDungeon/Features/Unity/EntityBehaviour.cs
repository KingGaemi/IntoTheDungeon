using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Runtime.World;
using IntoTheDungeon.Core.World.Abstractions;
using IntoTheDungeon.Features.Unity;
using UnityEngine;
public abstract class EntityBehaviour : MonoBehaviour, IEntityComponent
{
    public EntityRootBehaviour EntityRoot { get; set; }
    public IntoTheDungeon.Core.World.Abstractions.IWorld World { get; set; }
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
