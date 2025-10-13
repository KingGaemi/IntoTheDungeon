using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Unity;


[DisallowMultipleComponent]
[DefaultExecutionOrder(-8000)]
public class EntityRootBehaviour : MonoBehaviour, IEntityRoot, IWorldInjectable
{
    public Entity Entity { set; get; }
    public IWorld World { get; private set; }
    [SerializeField] VisualContainer visualContainer;
    [SerializeField] ScriptContainer scriptContainer;

    public VisualContainer Visual => visualContainer;

    public ScriptContainer Script => scriptContainer;

    public Transform Transform { get => transform; }

    public void Init(IWorld world)
    {
        World = world;
        InjectWorldToDependents();
    }

    private void InjectWorldToDependents()
    {
        var dependents = GetComponentsInChildren<IWorldDependent>(true);

        foreach (var dependent in dependents)
        {
            dependent.World = World;
        }

#if UNITY_EDITOR
        Debug.Log($"[EntityRoot] Injected World to {dependents.Length} components");
#endif
    }

    // Prefab 동적 생성시 재주입
    public void ReinjectWorld()
    {
        InjectWorldToDependents();
    }


    void OnValidate()
    {
        if (visualContainer != null && scriptContainer != null)
            return;

        // null인 것만 찾기
        if (visualContainer == null)
            visualContainer = GetComponentInChildren<VisualContainer>();

        if (scriptContainer == null)
            scriptContainer = GetComponentInChildren<ScriptContainer>();

    }





}
