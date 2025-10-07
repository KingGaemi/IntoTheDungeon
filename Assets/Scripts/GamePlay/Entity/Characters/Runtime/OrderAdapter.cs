#if false
using UnityEngine;
using MyGame.GamePlay.Party.Abstractions;
using MyGame.GamePlay.Entity.Characters.Abstractions;

public class OrderAdapter : MonoBehaviour
{
    
    [Header("Wiring")]

    [SerializeField] CharacterCore core;

    [SerializeField] MonoBehaviour orderSourceMb;   // IPartyOrderSource 할당(없으면 부모에서 찾음)
    IPartyOrderSource source;

    void Awake()
    {

        if (!orderSourceMb) orderSourceMb = GetComponentInParent<MonoBehaviour>();
        source = orderSourceMb as IPartyOrderSource ?? GetComponentInParent<IPartyOrderSource>();
    }

    void OnEnable()
    {
        var core = GetComponentInParent<PartyCore>();
        if (core) core.onPartyOrder.AddListener(OnPartyOrderReceived);
    }

    // Update is called once per frame

    void OnDisable()
    {
        var core = GetComponentInParent<PartyCore>();
        if (core) core.onPartyOrder.RemoveListener(OnPartyOrderReceived);
    }

    //  Idle, Stop, Hold, Fight, Move }
    void OnPartyOrderReceived(PartyOrder order)
    {
        switch (order)
        {
            case PartyOrder.Idle: OnCharacterOrderReceived(CharacterOrder.Idle); break;
            case PartyOrder.Stop: OnCharacterOrderReceived(CharacterOrder.Idle); break;
            case PartyOrder.Right: OnCharacterOrderReceived(CharacterOrder.Right); break;
            case PartyOrder.Left: OnCharacterOrderReceived(CharacterOrder.Left); break;
            case PartyOrder.Hold: OnCharacterOrderReceived(CharacterOrder.Hold); break;
            case PartyOrder.Fight: OnCharacterOrderReceived(CharacterOrder.Idle); break;
            case PartyOrder.Move: OnCharacterOrderReceived(CharacterOrder.Move); break;
        }
    }

    void OnCharacterOrderReceived(CharacterOrder order)
    {
        var intent = order switch
        {
            CharacterOrder.Idle   => CharacterIntent.Stop(),
            CharacterOrder.Move   => CharacterIntent.Move(core.State.FacingDir), // 방향 추가 필요
            CharacterOrder.Attack => CharacterIntent.Attack(),
            CharacterOrder.Right => CharacterIntent.Turn(MyGame.GamePlay.Entity.Abstractions.Facing2D.Right),
            CharacterOrder.Left => CharacterIntent.Turn(MyGame.GamePlay.Entity.Abstractions.Facing2D.Left),
            _ => CharacterIntent.None
        };

        core.ApplyIntent(intent);
    }
   

}
#endif