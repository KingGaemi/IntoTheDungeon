using UnityEngine;
using UnityEngine.UI;
public class HpBar : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image fill; // Filled Image
    [SerializeField] CanvasGroup canvasGroup; 
    
    [Header("Binding")]
    [SerializeField] Transform target;           // 따라갈 대상(보통 Visual의 SpriteRoot)
    [SerializeField] StatusComponent status;     // HP 참조
    [SerializeField] Vector3 worldOffset = new Vector3(0f, 1.8f, 0f);

    Camera cam;

    public void Bind(Transform followTarget, StatusComponent stats, Vector3 offset, Camera cameraOverride = null)
    {
        target = followTarget;
        status = stats;
        worldOffset = offset;
        cam = cameraOverride ? cameraOverride : Camera.main;
        SetVisible(false);                       // 기본은 숨김
        RefreshFill();
        LateTick();                              // 즉시 한 번 위치 보정
    }

    public void SetVisible(bool visible)
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = visible;
        canvasGroup.interactable = visible;
    }

    void Update()
    {
        RefreshFill();
    }

    void LateUpdate()
    {
        LateTick();
    }

    void RefreshFill()
    {
        if (!status || !fill) return;
        fill.fillAmount =  status.GetRatio();
    }

    void LateTick()
    {
        if (!target) return;
        if (!cam) cam = Camera.main;

        Vector3 worldPos = target.position + worldOffset;
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
        ((RectTransform)transform).position = screenPos;
    }
}
