using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] Vector3 cameraOffset = new Vector3(16f, 4f, 0f);
    public GameObject target;
    float cameraWidth, cameraHeight;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        cameraHeight = Camera.main.orthographicSize * 2f;
        cameraWidth = cameraHeight * Camera.main.aspect;
    }

    public void SetTarget(GameObject trgt)
    {
        target = trgt;
    }

    // Update is called once per frame
    void FixedUpdate()
    {   
        Vector3 targetPos = target.transform.position + cameraOffset;
        targetPos.z = transform.position.z; // 카메라 z 고정
        
        transform.position = Vector3.Lerp(transform.position, targetPos, 0.1f);
    }
}
