#if false

using UnityEngine;

public class EnemyBehaviour : MonoBehaviour
{
    [SerializeField] StatusComponent _statusComponent;

    [SerializeField] LockOnManager lockOnManager;

    [SerializeField] Animator animator;

    public FacingDirection facing;
    public bool encounter = false;
    public StatusComponent enemyStatusComponent;
    public float attackTimer = 0.0f;
    GameObject lockedOnEnemy;

    void Awake()
    {
        lockOnManager = GetComponent<LockOnManager>();
        _statusComponent = GetComponent<StatusComponent>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!_statusComponent.isDead)
        {
            if (attackTimer > 0)
                attackTimer -= GameTime.DeltaTime;
            else
            {
                LockOnNearEnemy();
                attackTimer = 0;
            }

            if (encounter && attackTimer == 0 && enemyStatusComponent != null)
                AutoAttack();
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
            int totalDamage = _statusComponent.damage;
            enemyStatusComponent.TakeDamage(totalDamage);
            attackTimer = 1.0f / _statusComponent.attackSpeed;
            animator.SetTrigger("Attack");
        }
    }

}
#endif