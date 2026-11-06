using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IntoTheDungeon.Unity.Hud
{
    public enum Ease { Linear, OutCubic, OutBack, InOutSine }

    /// <summary>
    /// UI 전용 간단 트윈(전부 unscaled 시간 — 배속·일시정지와 무관). 같은 대상·같은 채널(Scale/Fade/Move) 재호출은 교체.
    /// 대상이 파괴되면 항목을 버린다. 러너는 최초 호출 때 씬에 만들어지고(DontDestroyOnLoad 아님) 씬이 바뀌면 다시 만든다.
    /// </summary>
    public static class UiTween
    {
        enum Channel { Scale, Fade, Move, Punch, Delay }

        sealed class Item
        {
            public Object Target;      // Delay는 null
            public Channel Channel;
            public float Elapsed, Delay, Duration;
            public Ease Ease;
            public Vector3 From3, To3;
            public float FromF, ToF;
            public System.Action Done;
        }

        static readonly List<Item> s_items = new(32);
        static UiTweenRunner s_runner;

        static void Ensure()
        {
            if (s_runner) return;
            s_runner = new GameObject("UiTween").AddComponent<UiTweenRunner>();
            s_items.Clear();   // 이전 씬 항목은 대상이 사라졌으니 버린다
        }

        static Item Add(Object target, Channel ch, float t, float delay, Ease e)
        {
            Ensure();
            if (target) for (int i = s_items.Count - 1; i >= 0; i--) if (s_items[i].Target == target && s_items[i].Channel == ch) s_items.RemoveAt(i);
            var it = new Item { Target = target, Channel = ch, Duration = Mathf.Max(0.0001f, t), Delay = Mathf.Max(0f, delay), Ease = e };
            s_items.Add(it);
            return it;
        }

        public static void Scale(RectTransform rt, Vector3 from, Vector3 to, float t, Ease e = Ease.OutCubic, float delay = 0f)
        {
            if (!rt) return;
            var it = Add(rt, Channel.Scale, t, delay, e); it.From3 = from; it.To3 = to;
            if (delay <= 0f) rt.localScale = from;
        }

        public static void Fade(Graphic g, float from, float to, float t, float delay = 0f)
        {
            if (!g) return;
            var it = Add(g, Channel.Fade, t, delay, Ease.Linear); it.FromF = from; it.ToF = to;
            if (delay <= 0f) SetAlpha(g, from);
        }

        public static void Fade(CanvasGroup g, float from, float to, float t, float delay = 0f)
        {
            if (!g) return;
            var it = Add(g, Channel.Fade, t, delay, Ease.Linear); it.FromF = from; it.ToF = to;
            if (delay <= 0f) g.alpha = from;
        }

        public static void Move(RectTransform rt, Vector2 from, Vector2 to, float t, Ease e = Ease.OutCubic, float delay = 0f)
        {
            if (!rt) return;
            var it = Add(rt, Channel.Move, t, delay, e); it.From3 = from; it.To3 = to;
            if (delay <= 0f) rt.anchoredPosition = from;
        }

        /// <summary>1 → 1+amount → 1 (숫자 갱신·획득 강조).</summary>
        public static void Punch(RectTransform rt, float amount = 0.12f, float t = 0.25f)
        {
            if (!rt) return;
            var it = Add(rt, Channel.Punch, t, 0f, Ease.Linear); it.FromF = amount;
        }

        public static void Delay(float t, System.Action a)
        {
            if (a == null) return;
            var it = Add(null, Channel.Delay, Mathf.Max(0.0001f, t), 0f, Ease.Linear); it.Done = a;
        }

        /// <summary>그 대상의 트윈을 전부 멈춘다(끝값으로 맞추지 않음).</summary>
        public static void Kill(Object target)
        {
            if (!target) return;   // null·파괴된 대상은 지울 게 없다(파괴된 객체는 Step이 버린다). Unity의 ==는 파괴된 객체와 null을 같다고 봐서 Delay 항목까지 지워질 수 있다
            for (int i = s_items.Count - 1; i >= 0; i--) if (s_items[i].Channel != Channel.Delay && s_items[i].Target == target) s_items.RemoveAt(i);
        }

        static float Apply(Ease e, float p) => e switch
        {
            Ease.OutCubic => 1f - Mathf.Pow(1f - p, 3f),
            Ease.OutBack => 1f + 2.70158f * Mathf.Pow(p - 1f, 3f) + 1.70158f * Mathf.Pow(p - 1f, 2f),
            Ease.InOutSine => -(Mathf.Cos(Mathf.PI * p) - 1f) * 0.5f,
            _ => p
        };

        static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        internal static void Step(float dt)
        {
            for (int i = s_items.Count - 1; i >= 0; i--)
            {
                if (i >= s_items.Count) continue;   // Done 콜백이 같은 대상의 항목을 지웠으면(Add/Kill) 목록이 줄어 있을 수 있다
                var it = s_items[i];
                if (it.Channel != Channel.Delay && !it.Target) { s_items.RemoveAt(i); continue; }   // 대상이 파괴됨
                it.Elapsed += dt;
                float t = it.Elapsed - it.Delay;
                if (t < 0f) continue;
                float p = Mathf.Clamp01(t / it.Duration);
                float k = Apply(it.Ease, p);
                switch (it.Channel)
                {
                    case Channel.Scale: ((RectTransform)it.Target).localScale = Vector3.LerpUnclamped(it.From3, it.To3, k); break;
                    case Channel.Move: ((RectTransform)it.Target).anchoredPosition = Vector2.LerpUnclamped(it.From3, it.To3, k); break;
                    case Channel.Fade:
                        float a = Mathf.Lerp(it.FromF, it.ToF, k);
                        if (it.Target is CanvasGroup cg) cg.alpha = a; else if (it.Target is Graphic g) SetAlpha(g, a);
                        break;
                    case Channel.Punch:
                        float s = 1f + it.FromF * Mathf.Sin(p * Mathf.PI);
                        ((RectTransform)it.Target).localScale = new Vector3(s, s, 1f);
                        break;
                }
                if (p >= 1f)
                {
                    s_items.RemoveAt(i);
                    it.Done?.Invoke();
                }
            }
        }
    }

    /// <summary>UiTween 러너(최초 호출 때 생성, 씬마다 다시). unscaledDeltaTime으로 돌린다.</summary>
    public sealed class UiTweenRunner : MonoBehaviour
    {
        void Update() => UiTween.Step(Time.unscaledDeltaTime);
    }
}
