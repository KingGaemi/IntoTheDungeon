using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DoorTrigger2D : MonoBehaviour
{
    [SerializeField] private DoorController door;     // 같은 오브젝트나 부모에 있는 DoorController
    [SerializeField] private bool openOnlyOnce = true;



    void Reset()
    {
        // 자동 배선
        GetComponent<Collider2D>().isTrigger = true;
        if (!door) door = GetComponent<DoorController>() ?? GetComponentInParent<DoorController>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {

        door.SetState(DoorState.Open);

        if (openOnlyOnce)
        {
            // 한 번만 열고, 더 이상 트리거 동작 안 하려면…
            GetComponent<Collider2D>().enabled = false;
        }
    }


}
