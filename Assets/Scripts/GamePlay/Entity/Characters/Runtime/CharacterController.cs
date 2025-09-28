#if false
using UnityEngine;


public class CharacterController : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] StateComponent state;
    void Awake()
    {
        if (!state) state = GetComponent<StateComponent>();
    }
    public void ApplyIntent(in CharacterIntent it)
    {

        var cur = state.Current;
        if (state.TryResolveNext(in cur, in it, out var next, out var lockSec, out var reason))
        {
            state.Apply(in next);
        }
        else
        {

        }
    }
}
#endif