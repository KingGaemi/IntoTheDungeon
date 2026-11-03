using UnityEngine;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>이펙트용 기본 그림(코드로 만듦): 부드러운 동그라미·고리·빛기둥·도트 별(기절). 처음 쓸 때 한 번 만든다.</summary>
    public static class FxSprites
    {
        static Sprite _dot, _ring, _beam, _star;

        public static Sprite DotSprite { get { if (!_dot) Make(); return _dot; } }
        public static Sprite RingSprite { get { if (!_dot) Make(); return _ring; } }
        public static Sprite BeamSprite { get { if (!_dot) Make(); return _beam; } }
        public static Sprite StarSprite { get { if (!_star) MakeStar(); return _star; } }

        // 기절 별: 16px 도트 오각별(노랑, 가운데 밝게, 어두운 테두리). 0.4 월드 단위
        static void MakeStar()
        {
            const int n = 16;
            var inside = new bool[n * n];
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float rad = i % 2 == 0 ? 7.6f : 3.2f;
                pts[i] = new Vector2(7.5f + Mathf.Cos(a) * rad, 7.2f + Mathf.Sin(a) * rad);
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inPoly = false;   // 짝홀 규칙
                    for (int i = 0, j = 9; i < 10; j = i++)
                        if ((pts[i].y > p.y) != (pts[j].y > p.y) && p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                            inPoly = !inPoly;
                    inside[y * n + x] = inPoly;
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var dark = new Color(0.25f, 0.15f, 0.02f, 1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Color c = new(0f, 0f, 0f, 0f);
                    if (inside[y * n + x])
                    {
                        float d = new Vector2(x + 0.5f - 7.5f, y + 0.5f - 7.2f).magnitude;
                        c = Color.Lerp(new Color(1f, 1f, 0.85f, 1f), new Color(1f, 0.78f, 0.15f, 1f), Mathf.Clamp01(d / 6f));
                    }
                    else
                    {
                        for (int dy = -1; dy <= 1 && c.a == 0f; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if ((dx == 0 || dy == 0) && xx >= 0 && xx < n && yy >= 0 && yy < n && inside[yy * n + xx]) { c = dark; break; }
                            }
                    }
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            _star = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n / 0.4f);
        }

        // 동그라미(부드러운 가장자리)·고리·아래가 진하고 위로 갈수록 옅은 기둥을 코드로 만든다
        static void Make()
        {
            const int n = 64;
            var dot = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var ring = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 6f)));
                    ring.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Mathf.Abs(d - 0.85f) * 12f)));
                }
            dot.Apply(); ring.Apply();

            var beam = new Texture2D(32, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 32; x++)
                {
                    float side = 1f - Mathf.Abs((x + 0.5f) / 32f * 2f - 1f);   // 가운데가 진함
                    float up = 1f - (y + 0.5f) / 64f;                          // 아래가 진함
                    beam.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(side * side * up * 1.6f)));
                }
            beam.Apply();

            dot.wrapMode = ring.wrapMode = beam.wrapMode = TextureWrapMode.Clamp;
            _dot = Sprite.Create(dot, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);      // 1×1 월드 단위
            _ring = Sprite.Create(ring, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            _beam = Sprite.Create(beam, new Rect(0, 0, 32, 64), new Vector2(0.5f, 0f), 64);  // 발에서 위로 0.5×1
        }
    }
}
