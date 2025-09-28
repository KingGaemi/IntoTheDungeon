#if false
using UnityEngine;

public class MovementController : MonoBehaviour
{

    [Header("Wiring")]
    [SerializeField] private StatusComponent status;
    [SerializeField] private StateComponent state;

    public bool Move { get; set; } = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    void OnValidate()
    {
        if (!status)
            status = GetComponent<StatusComponent>();
        if (!state)
            state = GetComponent<StateComponent>();
        
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void FixedUpdate()
    {
        if (!Move) return;
        float speed = status.movementSpeed;
        Vector2 dir = (state.facing == FacingDirection.Right)
                        ? Vector2.right
                        : Vector2.left;

        Transform character = transform.parent;

        character.position += (Vector3)(dir * speed * GameTime.DeltaTime);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
#endif