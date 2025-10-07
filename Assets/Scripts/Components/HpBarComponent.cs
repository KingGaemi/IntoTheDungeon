#if false
using UnityEngine;

[DisallowMultipleComponent]
public class HpBarComponent : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] public HpBar hpBar;          // 자동으로 찾아 채움
    [SerializeField] private Transform target;     // 보통 캐릭터 루트(자기 자신)

    [Header("Placement")]
    [SerializeField] private float offsetY = 0.1f; // 머리 위 위치
    [SerializeField] private bool faceCamera = true;

    [Header("Status (연동 소스)")]
    [SerializeField] private int maxHp = 100;
    [SerializeField] private int currentHp = 100;
    // ↑ 실제 프로젝트에선 StatusComponent를 참조해서 값 읽어오면 됨

    void Reset()  { AutoWire(); }
    void Awake()  { AutoWire(); }

    void AutoWire()
    {
        if (!target) target = transform;

        // 자식에 UI_HpBar가 있으면 그 안의 HpBar 찾기
        if (!hpBar)
        {
            var t = transform.root.Find("UI_HpBar");
            if (t) hpBar = t.GetComponent<HpBar>();
        }

    }
    
    void LateUpdate()
    {
        if (!hpBar) return;

        // 위치 갱신 (World Space Canvas를 머리 위로)
        var hpTr = hpBar.transform;
        var pos = target.position;
        pos.y += offsetY;
        hpTr.position = pos;

        if (faceCamera && Camera.main)
            hpTr.forward = Camera.main.transform.forward;

        // 체력 퍼센트 갱신 (예시)
        float p = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        hpBar.Set(p);
    }

    // 외부에서 체력 갱신할 때 호출하면 편한 헬퍼
    public void SetHp(int current, int max)
    {
        currentHp = Mathf.Clamp(current, 0, max);
        maxHp = Mathf.Max(1, max);
    }
}
#endif
