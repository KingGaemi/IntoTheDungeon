using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] Vector3 cameraOffset = new Vector3(16f, 4f, 0f);

    public GameObject rootEntity; // 최상위 엔티티 (인스펙터 할당 또는 외부 주입)
    private GameObject visualTarget; // 실제 카메라가 따라갈 뷰 컨테이너

    float cameraWidth, cameraHeight;
    bool lockedOn = false;

    void Start()
    {
        cameraHeight = Camera.main.orthographicSize * 2f;
        cameraWidth = cameraHeight * Camera.main.aspect;
    }

    public void SetTarget()
    {
        // 1. 최상위 엔티티가 아직 할당되지 않았다면 검색 시도 중단 (Null 에러 방지)
        if (rootEntity == null) return;

        Transform visualTransform = rootEntity.transform.Find("VisualDefault");

        if (visualTransform != null)
        {
            visualTarget = visualTransform.gameObject; // 2. 변수를 덮어쓰지 않고 따로 저장
            lockedOn = true;
        }
        else
        {
            // 주의: FixedUpdate에서 실행될 때 로그를 켜두면 프레임 드랍이 발생할 수 있으므로 주석 처리 권장
            // Debug.LogWarning("VisualDefault 자식 객체를 찾을 수 없습니다.");
        }
    }

    void FixedUpdate()
    {
        if (!lockedOn)
        {
            SetTarget();
            if (!lockedOn) return; // 아직 visualTarget을 찾지 못했다면 카메라 이동 로직 스킵
        }

        // 3. rootEntity가 아닌 visualTarget의 위치를 추적
        Vector3 targetPos = visualTarget.transform.position + cameraOffset;
        targetPos.z = transform.position.z; // 카메라 z 고정

        transform.position = Vector3.Lerp(transform.position, targetPos, 0.1f);
    }
}