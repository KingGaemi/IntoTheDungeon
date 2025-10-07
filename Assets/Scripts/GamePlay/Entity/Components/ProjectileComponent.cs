using UnityEngine;

public class ProjectileComponent : MonoBehaviour
{
    
    public GameObject owner;
    public int damage;
    public int penetration;
    public float speed;
    public float lifetime;

    public float hitInterval;

    Vector2 direction;
    float timer;

    public void Initialize(GameObject owner, Vector2 direction, int damage, int penetration , float speed, float lifetime, float hitInterval = 0f)
    {
        this.owner = owner;
        this.direction = direction.normalized;
        this.damage = damage;
        this.penetration = penetration;
        this.speed = speed;
        this.lifetime = lifetime;
        this.timer = lifetime;
        this.hitInterval = hitInterval;
    }

    void Update()
    {
        transform.position += (Vector3)(direction * speed * Time.deltaTime);
        timer -= Time.deltaTime;
        if (timer <= 0) Destroy(gameObject);
    }
}
