using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 유물 메달(장비와 한눈에 구분되게): 장비는 네모 칸·등급 색 테두리, 유물은 둥근 금테 메달 + 뒤로 금빛 빛무리 +
    /// 몇 초마다 테두리를 한 바퀴 훑는 반짝임. 등급은 테두리 색이 아니라 메달 아래 보석 점(일반 1개 … 영웅 4개, 점 색 = 등급 색).
    /// 유물 줄·유물 페이지·상점 진열대·보상 카드가 같이 쓴다. Build로 만들고 Set으로 유물을 바꾼다.
    /// </summary>
    public sealed class RelicMedallion : MonoBehaviour
    {
        static readonly Color RingGold = new(1f, 0.82f, 0.4f, 1f);
        static Sprite s_ring, s_disc, s_aura, s_glint, s_pip;

        Image _aura, _disc, _ring, _icon;
        RectTransform _glintPivot;
        Image _glint;
        readonly Image[] _pips = new Image[4];
        float _t, _phase;

        public CardDef Relic { get; private set; }

        /// <summary>parent 안에 지름 size인 메달을 만든다(rect는 parent 기준으로 놓으면 됨).</summary>
        public static RelicMedallion Build(Transform parent, float size)
        {
            if (!s_ring) MakeSprites();
            var go = new GameObject("RelicMedallion", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(size, size);
            var m = go.AddComponent<RelicMedallion>();
            m._aura = Img(rt, s_aura, new Color(1f, 0.75f, 0.3f, 0.5f), size * 1.55f);
            m._disc = Img(rt, s_disc, new Color(0.16f, 0.11f, 0.2f, 1f), size * 0.86f);
            m._icon = Img(rt, null, Color.white, size * 0.58f);
            m._ring = Img(rt, s_ring, RingGold, size);
            // 반짝임: 메달 가운데를 축으로 도는 막대 끝의 빛 점
            var pivot = new GameObject("GlintPivot", typeof(RectTransform));
            pivot.transform.SetParent(rt, false);
            m._glintPivot = (RectTransform)pivot.transform;
            m._glintPivot.sizeDelta = Vector2.zero;
            m._glint = Img(m._glintPivot, s_glint, new Color(1f, 1f, 0.9f, 0f), size * 0.42f);
            m._glint.rectTransform.anchoredPosition = new Vector2(0f, size * 0.46f);
            for (int i = 0; i < m._pips.Length; i++)
            {
                m._pips[i] = Img(rt, s_pip, Color.white, size * 0.16f);
                m._pips[i].gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            }
            m._phase = Random.value * 3f;   // 여러 개가 동시에 반짝이지 않게
            go.SetActive(false);
            return m;
        }

        public void Set(CardDef relic)
        {
            Relic = relic;
            bool on = relic != null;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
            if (!on) return;
            _icon.sprite = RelicIcons.Of(relic.Id);
            int count = (int)relic.Rarity + 1;
            float size = ((RectTransform)transform).sizeDelta.x;
            float gap = size * 0.19f;
            var col = LootRenderer.RarityColor(relic.Rarity);
            for (int i = 0; i < _pips.Length; i++)
            {
                bool lit = i < count;
                _pips[i].enabled = lit;
                if (!lit) continue;
                _pips[i].color = col;
                _pips[i].rectTransform.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * gap, -size * 0.5f);
            }
            // 높은 등급일수록 빛무리가 진하다
            _aura.color = new Color(1f, 0.72f, 0.28f, 0.28f + 0.1f * (int)relic.Rarity);
        }

        void Update()
        {
            if (Relic == null) return;
            _t += Time.unscaledDeltaTime;
            float pulse = 0.5f + 0.5f * Mathf.Sin((_t + _phase) * 2.2f);
            _aura.rectTransform.localScale = Vector3.one * (0.95f + 0.08f * pulse);
            // 3초마다 1초 동안 반짝임이 테두리를 한 바퀴
            float cycle = (_t + _phase) % 3f;
            bool sweep = cycle < 1f;
            _glintPivot.localRotation = Quaternion.Euler(0f, 0f, -360f * cycle);
            _glint.color = new Color(1f, 1f, 0.9f, sweep ? Mathf.Sin(cycle * Mathf.PI) * 0.95f : 0f);
        }

        static Image Img(RectTransform parent, Sprite sprite, Color color, float size)
        {
            var go = new GameObject("Part", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        // 금테 고리(징 4개 + 위쪽 밝은 하이라이트)·속 원판·빛무리·반짝임·보석 점을 코드로 그린다
        static void MakeSprites()
        {
            const int n = 96;
            var ring = NewTex(n); var disc = NewTex(n); var aura = NewTex(n); var glint = NewTex(32); var pip = NewTex(16);
            float c = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / c;   // 0 가운데 ~ 1 가장자리
                    float ang = Mathf.Atan2(dy, dx);
                    // 고리: 0.82~0.98, 바깥·안쪽 가장자리는 어둡게, 위쪽은 밝게
                    float a = Mathf.Clamp01((0.985f - d) * 40f) * Mathf.Clamp01((d - 0.8f) * 40f);
                    float edge = Mathf.Abs(d - 0.9f) / 0.08f;
                    float shade = Mathf.Lerp(1.05f, 0.55f, edge * edge) * Mathf.Lerp(0.75f, 1.1f, (dy / c + 1f) * 0.5f);
                    // 징 4개(대각선): 고리 위 작은 원
                    for (int k = 0; k < 4; k++)
                    {
                        float sa = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                        float sx = Mathf.Cos(sa) * 0.9f * c, sy = Mathf.Sin(sa) * 0.9f * c;
                        float sd = Mathf.Sqrt((dx - sx) * (dx - sx) + (dy - sy) * (dy - sy)) / c;
                        if (sd < 0.1f) { a = 1f; shade = Mathf.Lerp(1.25f, 0.7f, sd / 0.1f); }
                    }
                    ring.SetPixel(x, y, new Color(Mathf.Min(1f, shade), Mathf.Min(1f, shade), Mathf.Min(1f, shade), a));
                    // 속 원판: 가운데가 조금 밝은 그라데이션
                    float da = Mathf.Clamp01((1f - d) * 30f);
                    float ds = Mathf.Lerp(1.25f, 0.8f, d);
                    disc.SetPixel(x, y, new Color(Mathf.Min(1f, ds), Mathf.Min(1f, ds), Mathf.Min(1f, ds), da));
                    // 빛무리: 가운데에서 바깥으로 부드럽게
                    float ga = Mathf.Clamp01(1f - d);
                    aura.SetPixel(x, y, new Color(1f, 1f, 1f, ga * ga));
                }
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - 16f) / 16f, dy = Mathf.Abs(y + 0.5f - 16f) / 16f;
                    float star = Mathf.Clamp01(1f - Mathf.Min(dx * 5f + dy, dy * 5f + dx));   // 십자 반짝
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.2f);
                    glint.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(star + core)));
                }
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    float dx = x + 0.5f - 8f, dy = y + 0.5f - 8f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / 8f;
                    float s = d < 0.45f && dx < 0 && dy > 0 ? 1.4f : 1f;   // 왼쪽 위 하이라이트
                    pip.SetPixel(x, y, new Color(Mathf.Min(1f, s), Mathf.Min(1f, s), Mathf.Min(1f, s), Mathf.Clamp01((1f - d) * 8f)));
                }
            ring.Apply(); disc.Apply(); aura.Apply(); glint.Apply(); pip.Apply();
            s_ring = Sprite.Create(ring, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s_disc = Sprite.Create(disc, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s_aura = Sprite.Create(aura, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            s_glint = Sprite.Create(glint, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
            s_pip = Sprite.Create(pip, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 100f);
        }

        static Texture2D NewTex(int n) => new(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
    }
}
