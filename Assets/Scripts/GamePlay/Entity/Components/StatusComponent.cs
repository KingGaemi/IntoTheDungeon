using UnityEngine;
using System;
using System.ComponentModel;



public enum AttackType
{
    Melee,
    Range
};

public class StatusComponent : MonoBehaviour
{

    ShakeEffectComponent shakeEffectComponent;

    [SerializeField] private int maxHp = 100;
    [SerializeField] private int currentHp = 100;

    public int MaxHp => maxHp;           // 읽기 전용 프로퍼티
    public int CurrentHp => currentHp;   // 읽기 전용 프로퍼티
    public AttackType attackType = AttackType.Melee;
    public event Action<int, int> OnHpChanged;
    public event Action<float> OnAttackSpeedChanged;
    public event Action<float> OnMovementSpeedChanged;
    public int armor = 0;
    public int damage = 10;
    public float attackSpeed = 1.0f;  // attack n times in a sec
    public float AttackSpeed
    {
        get => attackSpeed;
        set
        {
            if (Mathf.Approximately(attackSpeed, value)) return;
            attackSpeed = Mathf.Max(0f, value);
            OnAttackSpeedChanged?.Invoke(attackSpeed);
        }
    }
    public float movementSpeed = 1.0f;
    public float MovementSpeed
    {
        get => movementSpeed;
        set
        {
            if (Mathf.Approximately(movementSpeed, value)) return;
            movementSpeed = Mathf.Max(0f, value);
            OnMovementSpeedChanged?.Invoke(movementSpeed);
        }
    }

    public float height = 1.0f;
    public float width = 0.5f;
    public bool isDead = false;
    public event System.Action OnHit;

    public event System.Action OnDeath;



    void Awake()
    {
        var parent = transform.parent;
        if (parent)
        {
            foreach (Transform child in parent)
            {
                if (child.name.StartsWith("Visual_"))
                {
                    shakeEffectComponent = child.GetComponentInChildren<ShakeEffectComponent>();
                    break;
                }
            }
        }

        Raise();
        OnAttackSpeedChanged?.Invoke(attackSpeed);
    }
    void FixedUpdate()
    {

    }


    public void SetHp(int current, int max)
    {
        maxHp = Mathf.Max(1, max);
        currentHp = Mathf.Clamp(current, 0, MaxHp);
        Raise();
    }

    public float GetRatio()
    {
        float ratio = Mathf.Clamp01(currentHp / maxHp);
        return ratio;
    }

    public void TakeDamage(int dmg)
    {
        if (isDead) return;

        SetHp(currentHp - dmg, maxHp);
        Debug.Log(($"Hit! {dmg}"));
        Debug.Log(($"{currentHp}/{maxHp}"));

        if (shakeEffectComponent)
        {
            float ratio = (float)dmg / Mathf.Max(1, maxHp);     // 0~1
            shakeEffectComponent.StartShake(0.4f, ratio * 8f);
        }
        else
        {
            Debug.Log("shakeEffectComponent is empty");
        }

        if (currentHp <= 0)
        {
            isDead = true;
            Debug.Log("Dead");
            OnDeath?.Invoke();
            Destroy(transform.parent.gameObject, 1.2f);
        }
        else
        {
            OnHit?.Invoke();
            // animator.SetTrigger("Hurt");
        }
    }

    void Raise() => OnHpChanged?.Invoke(currentHp, maxHp);

    public void ApplyDifficulty(DifficultyMod mod)
    {
        int newMax = Mathf.RoundToInt(maxHp * mod.hpMul) + mod.flatHp;
        int newAtk = Mathf.RoundToInt(damage * mod.atkMul);
        float newSpd = movementSpeed * mod.spdMul;

        SetHp(Mathf.Min(currentHp, newMax), newMax);
        damage = newAtk;
        movementSpeed = newSpd;
    }
}
