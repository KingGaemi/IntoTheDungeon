#if false
using UnityEngine;


using MyGame.Core.Abstractions;
public static class TransformExtensions
{
    public static Transform FindChildStartsWith(this Transform parent, string prefix)
    {
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith(prefix))
                return child;
        }
        return null;
    }
}

public class AutoFight : MonoBehaviour
{

    [Header("Dependencies")]
    [SerializeField] StatusComponent status;
    [SerializeField] ActionStateComponent actionState;
    [SerializeField] LockOnManager lockOnManager;
    [SerializeField] CharacterAnimator animator;



    [Header("Runtime State")]
    public Facing2D facing = Facing2D.Right;
    public bool encounter = false;


    GameObject lockedOnEnemy;
    public StatusComponent enemyStatusComponent;
    public float attackTimer = 0.0f;

    void Reset() => AutoWire();
    void OnValidate() { if (!Application.isPlaying) AutoWire(); }

    private void AutoWire()
    {
        // Scripts 컨테이너 안에 있을 경우
        if (!status) status = GetComponentInChildren<StatusComponent>(true);
        if (!lockOnManager) lockOnManager = GetComponentInChildren<LockOnManager>(true);

        // Visual_XXX 밑에서 Animator 찾기
        if (!animator)
        {
            var visual = transform.FindChildStartsWith("Visual_"); // 이름 규칙이 일정하다면
            if (visual) animator = visual.GetComponentInChildren<CharacterAnimator>(true);
            if (!animator) animator = GetComponentInChildren<CharacterAnimator>(true);
        }
    }

    void Start()
    {

        LockOnNearEnemy();

    }

    void FixedUpdate()
    {
        // 아무 것도 하지 않는 상태 / do nothing when idle
    
        // 물리 틱 델타타임 / physics tick deltaTime
        float dt = Time.fixedDeltaTime;

        // 조우 전 로밍 / roaming before encounter
        if (!encounter)
        {
            Roaming();
            LockOnNearEnemy();
            // 필요 시 달리기 플래그와 동기화 / optionally sync run flag with roaming
            // animator.SetBool("Run", true);
        }

        // 공격 쿨다운 감소 / decrease attack cooldown
        if (attackTimer > 0f)
        {
            attackTimer -= dt;                   // FixedUpdate에서는 fixedDeltaTime 사용
            if (attackTimer < 0f) attackTimer = 0f;  // 하한 클램프 / clamp to zero
        }
        else
        {
            LockOnNearEnemy();
            attackTimer = 0f; // 명시적 0 유지 / keep explicit zero
        }

        // 공격 조건: 조우 중 + 쿨다운 종료 + 타깃 존재 / attack when in encounter, cooldown finished, and target present
        // 부동소수 비교는 <= 0f 사용 / use <= 0f to avoid float equality
        if (encounter && attackTimer <= 0f && enemyStatusComponent != null)
        {
            AutoAttack();
            // 일반적으로 여기서 다음 공격을 위한 attackTimer 재설정이 필요하지만
            // 외부 규칙을 가정하지 않으므로 현 로직 유지 / do not assume/reset here per your constraint
        }
    }

    void LockOnNearEnemy()
    {
        lockedOnEnemy = lockOnManager.LockOnEnemy(facing);

        if (lockedOnEnemy)
        {
            enemyStatusComponent = lockedOnEnemy.GetComponentInChildren<StatusComponent>();

            encounter = !enemyStatusComponent.isDead;
        }
        else
        {
            encounter = false;
        }
    }
    void AutoAttack()
    {
        if (enemyStatusComponent)
        {
            int totalDamage = status.damage;
            enemyStatusComponent.TakeDamage(totalDamage);
            attackTimer = 1.0f / status.attackSpeed;
        }
    }


    void Roaming()
    {
        if (actionState.IsIdle) return;
        float speed = status.movementSpeed;
        Vector2 dir = (facing == Facing2D.Right)
                        ? Vector2.right
                        : Vector2.left;

        Transform warrior = transform.parent;

        warrior.position += (Vector3)(dir * speed * GameTime.DeltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Door"))
        {
            Debug.Log("Door 충돌 감지!");

        }
    }



}
#endif