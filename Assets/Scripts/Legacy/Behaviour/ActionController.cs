#if false
using UnityEngine;

public class ActionController : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private StatusComponent status;
    [SerializeField] private StateComponent state;

    void OnValidate()
    {
        if (!status)
            status = GetComponent<StatusComponent>();
        if (!state)
            state = GetComponent<StateComponent>();

    }

    void OnEnable()
    {
        if (state != null)
            state.OnActionChanged.AddListener(HandleActionChanged);
    }

    void OnDisable()
    {
        if (state != null)
            state.OnActionChanged.RemoveListener(HandleActionChanged);
    }

    void HandleActionChanged(ActionState s)
    {
        if (s != ActionState.Attack) return; 
        if (state == null || status == null) return;
        if (!state.CanAttack) return;

        switch (status.attackType)
        {
            case AttackType.Melee:
                MeleeAttack();
                break;
            case AttackType.Range:
                RangeAttack();
                break;
        }

        // 한 번 실행형 기본 흐름: 즉시 Idle 복귀(애니 이벤트로 바꿀 예정이면 여기 제거)
        state.TrySetAction(ActionState.Idle);
    }
    void BasicAttack()
    {
        if (state == null) return;
        state.TrySetAction(ActionState.Attack);



    }


    void MeleeAttack()
    {
        Vector2 dir = state.facing == FacingDirection.Right ? Vector2.right : Vector2.left;

        // 3) 발사 원점 / origin (캐릭터 중심에서 약간 전방)
        //    필요하면 status.height 등을 활용해 미세 조정
        Transform root = transform; // 최상위 캐릭터 Transform 기준
        Vector3 origin = root.position + (Vector3)(dir * status.width);



        
    }

    void RangeAttack()
    {
        
    }

    bool HasComponents()
    {
        return (status != null && state != null);
    }
    
}
#endif