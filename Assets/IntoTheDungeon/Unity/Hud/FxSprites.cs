using UnityEngine;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>이펙트용 기본 그림(코드로 만듦): 부드러운 동그라미·고리·빛기둥. 처음 쓸 때 한 번 만든다.</summary>
    public static class FxSprites
    {
        static Sprite _dot, _ring, _beam;

        public static Sprite DotSprite { get { if (!_dot) Make(); return _dot; } }
        public static Sprite RingSprite { get { if (!_dot) Make(); return _ring; } }
        public static Sprite BeamSprite { get { if (!_dot) Make(); return _beam; } }

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
