using UnityEngine;
using MyGame.GamePlay.Entity.Characters.Abstractions;
using MyGame.GamePlay.Entity.Abstractions;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator),
                  typeof(SpriteRenderer))]
public class CharacterAnimator : MonoBehaviour
{
    [SerializeField] Animator animator;               // Visual의 Animator
    [SerializeField] SpriteRenderer sprite;
    [SerializeField] CharacterCore core;
    [SerializeField] float multiplier = 1.0f;           // 필요시 배수 / optional scale
    readonly string attackSpeedParam = "AttackSpeed";
    readonly string movementSpeedParam = "MovementSpeed";
    readonly string deathParam = "Death";
    readonly string attackParam = "Attack";
    readonly string runParam = "Run";

    int hashAttackSpeed;
    int hashMovementSpeed;
    int hashDeath;

    int hashAttack;
    int hashRun;

    void OnValidate()
    {
        if (!core)
        {
            var root = GetComponentInParent<EntityRoot>(true);

            if (root)
            { 
                Debug.Log($"{root.name}");
                core = root.GetComponentInChildren<CharacterCore>(true); // Scripts 쪽까지 내려가서 탐색
            }
            else
                Debug.LogError("CharacterRoot not found", this);    
        }
    }
    void Reset()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!sprite) sprite = GetComponent<SpriteRenderer>();
        if (!core)
        {
            var root = GetComponentInParent<EntityRoot>(true);

            if (root)
            { 
                Debug.Log($"{root.name}");
                core = root.GetComponentInChildren<CharacterCore>(true); // Scripts 쪽까지 내려가서 탐색
            }
            else
                Debug.LogError("CharacterRoot not found", this);    
        }

    }

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (!sprite) sprite = GetComponent<SpriteRenderer>();
        if (!core)
        {
            var root = GetComponentInParent<EntityRoot>(true);

            if (root)
            {
                Debug.Log($"{root.name}");
                core = root.GetComponentInChildren<CharacterCore>(true); // Scripts 쪽까지 내려가서 탐색
            }
            else
                Debug.LogError("CharacterRoot not found", this);
        }

        hashAttackSpeed = Animator.StringToHash(attackSpeedParam);
        hashMovementSpeed = Animator.StringToHash(movementSpeedParam);
        hashDeath = Animator.StringToHash(deathParam);
        hashAttack = Animator.StringToHash(attackParam);
        hashRun = Animator.StringToHash(runParam);
    }

    void OnEnable()
    {
        if (core.Status != null)
        {
            core.Status.OnAttackSpeedChanged += HandleAttackSpeedChanged;
            core.Status.OnMovementSpeedChanged += HandleMovementSpeedChanged;
            core.Status.OnDeath += HandleDeath;

            HandleAttackSpeedChanged(core.Status.AttackSpeed); // 초기 반영 / apply initial
            HandleMovementSpeedChanged(core.Status.MovementSpeed);
        }

        if (core.State != null)
        {
            core.State.OnFacingChanged += OnFacing;
            core.State.OnMovementChanged += OnMovement;
            core.State.OnActionChanged += OnAction;
        }
    }

    void OnDisable()
    {
        if (core.Status != null)
        {
            core.Status.OnAttackSpeedChanged -= HandleAttackSpeedChanged;
            core.Status.OnMovementSpeedChanged -= HandleMovementSpeedChanged;
        }

        if (core.State != null)
        {
            core.State.OnFacingChanged -= OnFacing;
            core.State.OnMovementChanged -= OnMovement;
            core.State.OnActionChanged -= OnAction;
        }
    }

    void OnFacing(Facing2D f)
    {
        // animator.SetInteger("Facing", f == Facing2D.Left ? -1 : 1);
        // or sprite.flipX = (f == Facing2D.Left);

        sprite.flipX = f == Facing2D.Left;
    }
    void OnAction(ActionState action)
    {
        switch (action)
        {
            case ActionState.Idle:
                // animator.SetBool("Run", false);
                // animator.SetTrigger("Idle");
                break;

            case ActionState.Attack:
                animator.SetTrigger(hashAttack);
                break;
        }

    }
    void OnMovement(MovementState movement)
    {
        switch (movement)
        {
            case MovementState.Idle:
                animator.SetBool(hashRun, false);
                // animator.SetTrigger("Idle");
                break;

            case MovementState.Move:
            case MovementState.Walk:
            case MovementState.Run:
                animator.SetBool(hashRun, true);
                // animator.SetTrigger("Run");
                break;

            case MovementState.Jump:
                Debug.Log("Jump 상태 → 점프 애니메이션 실행");
                // animator.SetTrigger("Jump");
                break;

            default:
                Debug.LogWarning($"알 수 없는 MovementState: {movement}");
                break;
        }
    }

    void HandleAttackSpeedChanged(float spd)
    {
        if (animator)
            animator.SetFloat(hashAttackSpeed, spd * multiplier);
    }

    void HandleMovementSpeedChanged(float spd)
    {
        if (animator)
            animator.SetFloat(hashMovementSpeed, spd * multiplier * 0.25f);
    }

    void HandleDeath()
    {
        if (animator)
            animator.SetTrigger(hashDeath);
    }
}
