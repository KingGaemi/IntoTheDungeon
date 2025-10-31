using UnityEngine;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.Unity;
using IntoTheDungeon.Core.Physics.Abstractions;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-8000)]
public class EntityRootBehaviour : MonoBehaviour, IEntityRoot
{
    public Entity Entity { set; get; }

    [SerializeField] VisualContainer visualContainer;
    [SerializeField] ScriptContainer scriptContainer;
    [SerializeField] Rigidbody2D rb;
    [SerializeField] Collider2D col;

    public VisualContainer Visual => visualContainer;
    public ScriptContainer Script => scriptContainer;
    public Transform Transform => transform;
    public Rigidbody2D Rigidbody => rb;
    public Collider2D Collider => col;
    public PhysicsHandle PhysicsHandle { get; set; }
    void Awake()
    {
        PhysicsHandle = new(-1, 0);
        EnsureReferences();
    }

    void EnsureReferences()
    {
        if (visualContainer == null)
            visualContainer = GetComponentInChildren<VisualContainer>();

        if (scriptContainer == null)
            scriptContainer = GetComponentInChildren<ScriptContainer>();

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (col == null)
            col = GetComponent<Collider2D>();
    }

    void OnValidate()
    {
        EnsureReferences();
    }
}