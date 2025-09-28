#if false

using UnityEngine;

public class ActionStateComponent : MonoBehaviour
{

    public ControlState controlState = ControlState.Normal;

    public bool IsIdle { get; set; } = false;

    public FacingDirection facing = FacingDirection.Right;

    public bool CanMove => controlState != ControlState.Stunned && controlState != ControlState.Rooted;
    public bool CanAttack => controlState != ControlState.Stunned && controlState != ControlState.Silenced && controlState != ControlState.Dead;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

}
#endif