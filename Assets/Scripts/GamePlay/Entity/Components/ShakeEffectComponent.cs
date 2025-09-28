using UnityEngine;

public class ShakeEffectComponent : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform shakeTarget;  
    [SerializeField] string visualPrefix = "Visual_";


    [Header("Params")]
    public float shakeTime = 0f;    // 흔들림 남은 시간
    public float shakeAmount = 5f;  // 흔들림 세기 (픽셀/유닛)
    public float minAmount = 1f;
    public float maxAmount = 10f;
    Vector3 originalPos;
    bool hasTarget;
    void Awake()
    {
          if (!shakeTarget)
        {
            var parent = transform.parent;
            if (parent)
            {
                foreach (Transform child in parent)
                {
                    if (child.name.StartsWith(visualPrefix))
                    {
                        var sr = child.GetComponentInChildren<SpriteRenderer>(true);
                        if (sr) { shakeTarget = sr.transform; break; }
                    }
                }
            }
        }

        if (!shakeTarget) shakeTarget = transform; // 최후 fallback
        originalPos = shakeTarget.localPosition;
        hasTarget = shakeTarget != null;
    }

    void OnEnable()
    {
        if (hasTarget) originalPos = shakeTarget.localPosition;
    }

    void OnDisable()
    {
        // 비활성화 시 원위치 복구
        if (hasTarget) shakeTarget.localPosition = originalPos;
    }

    void Update()
    {
        if (!hasTarget) return;

        if (shakeTime > 0f)
        {
            shakeTime -= Time.deltaTime;

            // 선형 감쇠
            float t = Mathf.Clamp01(1f - shakeTime / Mathf.Max(0.0001f, 1f));
            float strength = Mathf.Lerp(shakeAmount, 0f, t);

            Vector2 off = Random.insideUnitCircle * strength * 0.1f;
            shakeTarget.localPosition = originalPos + new Vector3(off.x, off.y, 0f);

            if (shakeTime <= 0f)
                shakeTarget.localPosition = originalPos;
        }
    }

    public void StartShake(float duration = 0.1f, float amount = 2f)
    {
        amount = Mathf.Clamp(amount, minAmount, maxAmount);
        shakeTime = duration;
        shakeAmount = amount;
        if (hasTarget) originalPos = shakeTarget.localPosition;
    }
}
