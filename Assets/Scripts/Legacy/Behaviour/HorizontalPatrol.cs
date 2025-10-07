using UnityEngine;

public class HorizontalPatrol : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [Tooltip("시작 위치에서 ±X 만큼 왕복")]
    public float amplitude = 1f;
    [Tooltip("진동 속도 (라디안/sec)")]
    public float speed = 1f;
    
    Transform root;
    
    Vector3 _startPos;
    void Awake()
    {
        // 필드 초기화부가 아니라, 런타임에 transform 이 준비된 시점에!
        root = transform.root;           // 또는 transform.parent;
        _startPos = root.position;
    }
    void Start()
    {
        _startPos = transform.position;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float offset = Mathf.Sin(Time.time * speed) * amplitude;
        root.position = _startPos + Vector3.right * offset;
    }
}
