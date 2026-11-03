using UnityEngine;
using UnityEngine.UI;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 아직 동료가 없는 파티 칸: 어두운 바탕 + 점선 테두리 + 큰 「+」 + 「빈 자리」.
    /// 파티가 최대 4명까지 늘어난다는 걸 설명 없이도 보이게 한다(전투 필살기 줄·정비 화면 파티 카드).
    /// </summary>
    public static class EmptySlotUi
    {
        public static readonly Color Line = new(0.78f, 0.74f, 0.62f, 0.55f);
        static readonly Color Fill = new(0.06f, 0.06f, 0.08f, 0.45f);

        /// <param name="hint">「+」 아래 작은 안내(영입 방법). null이면 생략</param>
        public static GameObject Build(Transform parent, Font font, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string hint, int plusSize = 64)
        {
            var go = new GameObject("EmptySlot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var bg = go.GetComponent<Image>();
            bg.color = Fill;
            bg.raycastTarget = false;

            Dashes(rt, size);

            var plus = Label(rt, font, "+", plusSize, new Color(Line.r, Line.g, Line.b, 0.8f));
            var prt = plus.rectTransform;
            prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(0f, hint != null ? size.y * 0.18f : 0f);
            prt.offsetMax = Vector2.zero;

            var title = Label(rt, font, "빈 자리", Mathf.Max(16, plusSize / 3), new Color(1f, 1f, 1f, 0.55f));
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0f); trt.pivot = new Vector2(0.5f, 0f);
            trt.sizeDelta = new Vector2(0f, plusSize * 0.5f);
            trt.anchoredPosition = new Vector2(0f, hint != null ? size.y * 0.2f : size.y * 0.1f);

            if (hint != null)
            {
                var h = Label(rt, font, hint, Mathf.Max(13, plusSize / 4), new Color(1f, 1f, 1f, 0.4f));
                h.horizontalOverflow = HorizontalWrapMode.Wrap;
                var hrt = h.rectTransform;
                hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
                hrt.sizeDelta = new Vector2(-16f, size.y * 0.2f);
                hrt.anchoredPosition = new Vector2(0f, 6f);
            }
            go.SetActive(false);
            return go;
        }

        // 테두리를 짧은 선분으로 끊어 그려 "비어 있는 자리" 느낌을 낸다
        static void Dashes(RectTransform parent, Vector2 size)
        {
            const float dash = 14f, gap = 9f, thick = 3f;
            for (float x = 0f; x < size.x; x += dash + gap)
            {
                float w = Mathf.Min(dash, size.x - x);
                Seg(parent, new Vector2(x, 0f), new Vector2(w, thick));
                Seg(parent, new Vector2(x, size.y - thick), new Vector2(w, thick));
            }
            for (float y = 0f; y < size.y; y += dash + gap)
            {
                float h = Mathf.Min(dash, size.y - y);
                Seg(parent, new Vector2(0f, y), new Vector2(thick, h));
                Seg(parent, new Vector2(size.x - thick, y), new Vector2(thick, h));
            }
        }

        static void Seg(RectTransform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject("Dash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = Line;
            img.raycastTarget = false;
        }

        static Text Label(RectTransform parent, Font font, string text, int size, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
