using UnityEngine;

namespace IntoTheDungeon.Features.Character
{
    /// <summary>
    /// 스프라이트의 발 높이를 물리 콜라이더 바닥에 맞춘다.
    /// 콜라이더는 캐릭터 스케일과 무관한 크기라, 피벗만으로는 스케일마다 발이 뜨거나 묻힌다.
    /// SpriteRoot에 붙이고, 움직이는 건 부모(Visual 컨테이너)다. SpriteRoot는 HitShake가 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FootAnchor : MonoBehaviour
    {
        [Tooltip("스프라이트 셀 바닥에서 발바닥까지 픽셀 수(서 있는 프레임 기준)")]
        [SerializeField] float feetPixelFromBottom = 22f;

        SpriteRenderer _sprite;
        Transform _visual;
        Collider2D _collider;

        void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();
            _visual = transform.parent;
        }

        void LateUpdate()
        {
            if (!_visual || !_sprite.sprite) return;
            if (!_collider && !TryFindCollider()) return;

            // 물리가 꺼져 있으면 bounds를 믿을 수 없으니 마지막 위치를 유지
            var rb = _collider.attachedRigidbody;
            if (!_collider.enabled || (rb && !rb.simulated)) return;

            var s = _sprite.sprite;
            float feetLocal = (feetPixelFromBottom - s.pivot.y) / s.pixelsPerUnit;   // SpriteRoot 로컬 단위
            // SpriteRoot의 흔들림 오프셋은 빼고, 기준 위치(Visual 원점)에서 발까지를 잰다
            float feetWorldY = _visual.TransformPoint(new Vector3(0f, feetLocal * transform.localScale.y, 0f)).y;
            float delta = _collider.bounds.min.y - feetWorldY;
            if (Mathf.Abs(delta) > 0.0005f)
                _visual.position += new Vector3(0f, delta, 0f);
        }

        bool TryFindCollider()
        {
            var root = GetComponentInParent<EntityRootBehaviour>();
            if (!root) return false;
            _collider = root.GetComponentInChildren<Collider2D>();
            return _collider;
        }
    }
}
