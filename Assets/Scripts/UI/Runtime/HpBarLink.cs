#if false
using UnityEngine;

public class HpBarLink : MonoBehaviour
{
    [SerializeField] Transform spriteRoot;          // Visual의 SpriteRoot
    [SerializeField] StatusComponent status;
    [SerializeField] Vector3 offset = new Vector3(0f, 1.8f, 0f);

    HpBar hb;

    void Start()
    {
        if (!spriteRoot) spriteRoot = transform;    // 안전장치
        hb = HpBarManager.Instance.Attach(spriteRoot, status, offset);
        // 전투 전: 숨김
        hb?.SetVisible(false);
    }

    public void ShowInCombat(bool show) => hb?.SetVisible(show);

    void OnDisable()
    {
        // 캐릭터가 사라질 때 반환
        if (hb) HpBarManager.Instance.Detach(hb);
        hb = null;
    }
}
#endif