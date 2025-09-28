using System.Collections.Generic;
using UnityEngine;

public class HpBarManager : MonoBehaviour
{
    public static HpBarManager Instance { get; private set; }

    [Header("Wiring")]
    [SerializeField] Canvas uiCanvas;          // Screen Space - Camera 권장
    [SerializeField] HpBar healthBarPrefab;

    readonly Queue<HpBar> pool = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (!uiCanvas) uiCanvas = FindFirstObjectByType<Canvas>();
    }

    public HpBar Attach(Transform target, StatusComponent status, Vector3 offset)
    {
        if (!uiCanvas || !healthBarPrefab || !target || !status) return null;

        HpBar hb = (pool.Count > 0) ? pool.Dequeue() : Instantiate(healthBarPrefab, uiCanvas.transform);
        hb.gameObject.SetActive(true);
        hb.Bind(target, status, offset, Camera.main);
        return hb;
    }

    public void Detach(HpBar hb)
    {
        if (!hb) return;
        hb.SetVisible(false);
        hb.gameObject.SetActive(false);
        pool.Enqueue(hb);
    }
}
