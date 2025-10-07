#if false
using UnityEngine;


public class CharacterController : EntityBehaviour
{

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