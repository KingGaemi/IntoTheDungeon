#if false
using UnityEngine;


using MyGame.Core.Abstractions;
public class LockOnManager : MonoBehaviour
{

    public Vector2 boxSize = new Vector2(5f, 2f);
    public LayerMask enemyLayer;

    public GameObject LockOnEnemy(Facing2D facing)
    {
        Vector2 origin = (Vector2)transform.parent.position;
        Vector2 direction = (facing == Facing2D.Right)
                            ? Vector2.right
                            : Vector2.left;

        Vector2 boxCenter = origin + direction * (boxSize.x * 0.5f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, boxSize, 0f, enemyLayer);

        GameObject closestEnemy = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            float dist = Vector2.Distance(origin, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closestEnemy = hit.gameObject;
            }
        }

        if (closestEnemy != null)
        {
            // Debug.Log($"Locked on to: {closestEnemy.name}");
            return closestEnemy;
        }
        else
        {
            return null;
        }
    }

}
#endif