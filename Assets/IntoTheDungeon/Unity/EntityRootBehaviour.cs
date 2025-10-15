using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.Unity;


[DisallowMultipleComponent]
[DefaultExecutionOrder(-8000)]
public class EntityRootBehaviour : MonoBehaviour, IEntityRoot
{
    public Entity Entity { set; get; }
    [SerializeField] VisualContainer visualContainer;
    [SerializeField] ScriptContainer scriptContainer;

    public VisualContainer Visual => visualContainer;

    public ScriptContainer Script => scriptContainer;

    public Transform Transform { get => transform; }

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
