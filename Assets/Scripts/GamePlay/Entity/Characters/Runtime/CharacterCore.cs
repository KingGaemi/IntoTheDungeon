using UnityEngine;
using MyGame.GamePlay.Entity.Characters.Abstractions;

[DisallowMultipleComponent]
[RequireComponent(typeof(StatusComponent),
                  typeof(StateComponent),
                  typeof(InformationComponent))]
public class CharacterCore : MonoBehaviour
{
    private StateComponent state;
    private StatusComponent status;
    private KinematicBehaviour kinematic;
    private InformationComponent information;


    public CharacterRoot CharacterRoot;
    public StateComponent State => state;
    public StatusComponent Status     => status;
    public KinematicBehaviour Kinematic => kinematic;

    public InformationComponent Information => information;

    void Start()
    {
        CharacterRegistry.Register(this);
    }

    void OnValidate()
    {
        // 자동 캐시
        if (!state) state = GetComponent<StateComponent>();
        if (!status) status = GetComponent<StatusComponent>();
        if (!kinematic) kinematic = GetComponent<KinematicBehaviour>();
        if (!information) information = GetComponent<InformationComponent>();
        if (!CharacterRoot) CharacterRoot = GetComponentInParent<CharacterRoot>();
    }
       // ... state/status/kinematic ...
    void OnEnable()  => CharacterRegistry.Register(this);
    void OnDisable() => CharacterRegistry.Unregister(this);

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
