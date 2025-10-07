using UnityEngine;

public class TestMarker : MonoBehaviour
{
    public Sprite icon;
    public Color color = Color.white;

    void OnDrawGizmos()
    {
        if (icon)
        {
            Gizmos.color = color;
            Gizmos.DrawIcon(transform.position, icon.name, true);
        }
        else
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(transform.position, 0.5f); // 아이콘 없으면 구체로 표시
        }
    }
}
