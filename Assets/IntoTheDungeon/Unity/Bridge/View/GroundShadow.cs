using UnityEngine;

namespace IntoTheDungeon.Unity.Bridge.View
{
    /// <summary>
    /// 2.5D 바닥 그림자. 캐릭터 그림은 높이만큼 떠오르지만 그림자는 바닥(깊이 위치)에 남는다.
    /// 높이가 올라갈수록 작고 옅어져 공중에 떠 있는 게 보이게 한다. ViewBridge가 매 프레임 Place를 부른다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundShadow : MonoBehaviour
    {
        [Tooltip("바닥에 있을 때 그림자 크기(월드 단위, 가로×세로)")]
        [SerializeField] Vector2 size = new(1.0f, 0.3f);
        [Range(0f, 1f)]
        [SerializeField] float alpha = 0.35f;
        [Tooltip("이 높이에서 그림자가 절반 크기가 된다")]
        [SerializeField] float halfSizeHeight = 2f;

        static Sprite _sprite;
        SpriteRenderer _renderer;

        void Awake()
        {
            if (!TryGetComponent(out _renderer)) _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sprite = GetSprite();
        }

        /// <summary>바닥 위치(월드)·높이·정렬 레이어·순서를 받아 그림자를 놓는다.</summary>
        public void Place(Vector3 groundWorld, float height, int sortingLayerId, int sortingOrder)
        {
            if (!_renderer) return;
            float k = halfSizeHeight > 0f ? 1f / (1f + Mathf.Max(0f, height) / halfSizeHeight) : 1f;

            transform.position = groundWorld;
            // 부모 스케일(캐릭터마다 다름)과 무관하게 월드 크기로 맞춘다
            var parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(size.x * k / SafeDiv(parentScale.x), size.y * k / SafeDiv(parentScale.y), 1f);

            _renderer.color = new Color(0f, 0f, 0f, alpha * k);
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
        }

        static float SafeDiv(float v) => Mathf.Abs(v) < 1e-5f ? 1f : v;

        // 가장자리가 부드러운 타원(1×1 월드 크기 스프라이트). 한 번만 만들어 모두 공유
        static Sprite GetSprite()
        {
            if (_sprite) return _sprite;
            const int W = 64, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);          // 0(중심) ~ 1(가장자리)
                    float a = Mathf.Clamp01((1f - d) / 0.35f);         // 바깥 35%를 부드럽게
                    px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), W);
            _sprite.name = "GroundShadow";
            return _sprite;
        }
    }
}
