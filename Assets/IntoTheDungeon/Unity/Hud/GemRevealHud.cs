using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 잼 획득 연출: 잼을 얻을 때마다(보스 처치·정예·판 끝 보상) 화면 가운데에 잼이 튀어나오며
    /// 뒤로 잼 색 빛살이 돌고 반짝이가 터진다. 보스 잼은 더 크고 길게 「보스 잼!」, 등급이 오르면 「Lv.2 달성!」.
    /// 끝나면 잼이 오른쪽 위(봉인석·잼 배지)로 날아간다. 누르면 다음으로 넘어간다.
    /// 판이 끝난 결과 화면엔 이번 판에 얻은 잼을 아이콘 줄로 보여 준다(글자 목록 대신).
    /// </summary>
    public sealed class GemRevealHud : MonoBehaviour, IWorldInjectable
    {
        const float NormalTime = 1.5f, BossTime = 2.6f, FlyTime = 0.35f;
        const int Sparkles = 16;

        BattleProgress _progress;
        Font _font;
        GameObject _canvas, _reveal;
        Image _dim, _rays, _rays2, _gem, _flash;
        Text _name, _tag, _sub;
        readonly Image[] _sparks = new Image[Sparkles];
        readonly Vector2[] _sparkVel = new Vector2[Sparkles];
        RectTransform _summary;
        readonly List<Image> _summaryIcons = new();
        readonly List<Text> _summaryCounts = new();

        RunData _run;
        int _seen;
        readonly Queue<string> _queue = new();
        string _current;
        float _age, _duration;
        bool _levelUp;
        int _countAfter;
        static Sprite s_rays;
        Dictionary<string, Sprite> _icons;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            if (!_canvas) Build();
        }

        Sprite Icon(string id)
        {
            if (_icons == null)
            {
                _icons = new Dictionary<string, Sprite>();
                foreach (var s in Resources.LoadAll<Sprite>("GemIcons")) _icons[s.name] = s;
            }
            return id != null && _icons.TryGetValue(id, out var sp) ? sp : null;
        }

        void Update()
        {
            if (!_canvas) return;
            var run = RunState.Current;
            if (run != _run) { _run = run; _seen = run != null ? run.GemsFound.Count : 0; _queue.Clear(); _current = null; }   // 새 판·이어하기는 지난 것을 다시 틀지 않는다
            if (run == null) return;
            while (_seen < run.GemsFound.Count) _queue.Enqueue(run.GemsFound[_seen++]);

            float dt = Time.unscaledDeltaTime;
            if (_current == null && _queue.Count > 0) Begin(_queue.Dequeue());
            if (_current != null)
            {
                _age += dt;
                if (Input.GetMouseButtonDown(0) && _age < _duration - FlyTime) _age = _duration - FlyTime;   // 누르면 바로 날아감
                Animate();
                if (_age >= _duration) { _current = null; _reveal.SetActive(false); }
            }

            // 결과 화면: 이번 판 잼 아이콘 줄
            bool over = run.Phase is RunPhase.Cleared or RunPhase.Failed;
            bool showSummary = over && _current == null && _queue.Count == 0 && run.GemsFound.Count > 0;
            if (_summary.gameObject.activeSelf != showSummary) { _summary.gameObject.SetActive(showSummary); if (showSummary) BuildSummary(run); }
        }

        void Begin(string id)
        {
            var gem = GemPool.Find(id);
            if (gem == null) return;
            _current = id;
            _age = 0f;
            int count = MetaProgress.Current.Count(id);
            _countAfter = count;
            _levelUp = GemPool.LevelOf(count) > GemPool.LevelOf(count - 1);
            _duration = (gem.IsBoss ? BossTime : NormalTime) + (_queue.Count > 2 ? -0.4f : 0f);   // 판 끝에 여러 개면 조금 빨리
            var col = new Color(gem.R, gem.G, gem.B, 1f);
            _gem.sprite = Icon(id);
            _rays.color = new Color(col.r, col.g, col.b, 0f);
            _rays2.color = new Color(1f, 1f, 1f, 0f);
            _name.text = gem.Name;
            _name.color = Color.Lerp(col, Color.white, 0.35f);
            _tag.text = gem.IsBoss ? "보스 잼!" : "잼 획득";
            _tag.color = gem.IsBoss ? UiKit.Gold : new Color(1f, 1f, 1f, 0.75f);
            int lv = GemPool.LevelOf(count), next = GemPool.NextThreshold(count);
            _sub.text = _levelUp && lv > 1 ? $"Lv.{lv} 달성!  {gem.Describe(lv)}"
                      : $"{gem.Describe(Mathf.Max(1, lv))}   <size=18>({(next > 0 ? $"{count}/{next}" : "최고 등급")})</size>";
            _sub.color = _levelUp && lv > 1 ? new Color(0.6f, 1f, 0.6f, 1f) : new Color(1f, 1f, 1f, 0.85f);
            for (int i = 0; i < Sparkles; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(260f, 620f) * (gem.IsBoss ? 1.3f : 1f);
                _sparkVel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp;
                _sparks[i].rectTransform.anchoredPosition = Vector2.zero;
                _sparks[i].color = Color.Lerp(col, Color.white, Random.value * 0.6f);
                _sparks[i].rectTransform.sizeDelta = Vector2.one * Random.Range(8f, 18f);
            }
            _reveal.SetActive(true);
        }

        void Animate()
        {
            var gem = GemPool.Find(_current);
            bool boss = gem.IsBoss;
            float t = _age;
            float dt = Time.unscaledDeltaTime;
            float flyStart = _duration - FlyTime;
            float fly = Mathf.Clamp01((t - flyStart) / FlyTime);
            float scaleBase = boss ? 1.25f : 1f;

            // 어둡게 깔렸다가 날아갈 때 걷힘
            float dimA = Mathf.Clamp01(t / 0.2f) * (1f - fly) * (boss ? 0.65f : 0.5f);
            _dim.color = new Color(0f, 0f, 0f, dimA);

            // 잼: 0에서 크게 튀어나왔다가 자리 잡고 둥실, 마지막엔 오른쪽 위로 작아지며 날아감
            float pop = t < 0.18f ? Mathf.Lerp(0f, 1.35f, t / 0.18f) : t < 0.32f ? Mathf.Lerp(1.35f, 1f, (t - 0.18f) / 0.14f) : 1f;
            float bob = Mathf.Sin(t * 3f) * 6f;
            var target = new Vector2(820f, 440f);   // 오른쪽 위 봉인석·잼 배지 쪽
            var gp = Vector2.Lerp(new Vector2(0f, 40f + bob), target, fly * fly);
            _gem.rectTransform.anchoredPosition = gp;
            _gem.rectTransform.localScale = Vector3.one * pop * scaleBase * Mathf.Lerp(1f, 0.25f, fly);
            _gem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2.2f) * 6f * (1f - fly));
            _gem.color = new Color(1f, 1f, 1f, 1f - fly * 0.3f);

            // 빛살 두 겹(반대로 돈다)
            float raysA = Mathf.Clamp01(t / 0.25f) * (1f - fly);
            var col = new Color(gem.R, gem.G, gem.B, 1f);
            _rays.color = new Color(col.r, col.g, col.b, 0.75f * raysA);
            _rays2.color = new Color(1f, 1f, 1f, 0.35f * raysA);
            _rays.rectTransform.anchoredPosition = _rays2.rectTransform.anchoredPosition = gp;
            _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 25f);
            _rays2.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -t * 40f);
            float raySize = (boss ? 760f : 560f) * (0.6f + 0.4f * Mathf.Clamp01(t / 0.3f));
            _rays.rectTransform.sizeDelta = Vector2.one * raySize;
            _rays2.rectTransform.sizeDelta = Vector2.one * raySize * 0.75f;

            // 처음 순간 번쩍
            float flash = Mathf.Clamp01(1f - t / 0.25f);
            _flash.color = new Color(1f, 1f, 1f, flash * (boss ? 0.55f : 0.3f));

            // 반짝이: 터져 나가며 느려지고 사라짐
            for (int i = 0; i < Sparkles; i++)
            {
                _sparkVel[i] *= Mathf.Exp(-3f * dt);
                var rt = _sparks[i].rectTransform;
                rt.anchoredPosition += _sparkVel[i] * dt;
                rt.localRotation = Quaternion.Euler(0f, 0f, t * 200f + i * 30f);
                var c = _sparks[i].color;
                _sparks[i].color = new Color(c.r, c.g, c.b, Mathf.Clamp01(1f - t / 1.1f));
            }

            // 글자: 잼 아래로 차례로 올라옴
            float ta = Mathf.Clamp01((t - 0.2f) / 0.2f) * (1f - fly);
            _tag.color = new Color(_tag.color.r, _tag.color.g, _tag.color.b, ta);
            _name.color = new Color(_name.color.r, _name.color.g, _name.color.b, ta);
            float sa = Mathf.Clamp01((t - 0.4f) / 0.2f) * (1f - fly);
            _sub.color = new Color(_sub.color.r, _sub.color.g, _sub.color.b, sa);
            float nameScale = _levelUp ? 1f + 0.12f * Mathf.Abs(Mathf.Sin(t * 6f)) * (1f - fly) : 1f;
            _name.rectTransform.localScale = Vector3.one * nameScale;
        }

        void BuildSummary(RunData run)
        {
            foreach (var i in _summaryIcons) Destroy(i.gameObject);
            _summaryIcons.Clear();
            _summaryCounts.Clear();
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var id in run.GemsFound)
            {
                if (!counts.ContainsKey(id)) { counts[id] = 0; order.Add(id); }
                counts[id]++;
            }
            const float size = 64f, gap = 14f;
            float total = order.Count * (size + gap) - gap;
            for (int k = 0; k < order.Count; k++)
            {
                var gem = GemPool.Find(order[k]);
                var img = NewImage(_summary, Color.white);
                img.sprite = Icon(order[k]);
                img.preserveAspect = true;
                var rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(-total * 0.5f + size * 0.5f + k * (size + gap), 0f);
                rt.sizeDelta = new Vector2(size, size);
                if (gem != null && gem.IsBoss) img.gameObject.AddComponent<Outline>().effectColor = UiKit.Gold;
                if (counts[order[k]] > 1)
                {
                    var c = UiKit.Soft(NewText(rt, $"×{counts[order[k]]}", 22));
                    c.alignment = TextAnchor.LowerRight;
                    var crt = c.rectTransform;
                    crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one; crt.offsetMin = crt.offsetMax = Vector2.zero;
                    _summaryCounts.Add(c);
                }
                _summaryIcons.Add(img);
            }
        }

        void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _canvas = new GameObject("GemRevealCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas.transform.SetParent(transform, false);
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 260;   // 결과 화면·정비 화면 위
            var scaler = _canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = (RectTransform)_canvas.transform;

            _reveal = new GameObject("Reveal", typeof(RectTransform));
            _reveal.transform.SetParent(root, false);
            var rrt = (RectTransform)_reveal.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one; rrt.offsetMin = rrt.offsetMax = Vector2.zero;

            _dim = NewImage(rrt, new Color(0f, 0f, 0f, 0f));
            _dim.raycastTarget = true;   // 연출 중엔 뒤를 누르지 않게(누르면 다음으로)
            Stretch(_dim.rectTransform);
            if (!s_rays) s_rays = MakeRays();
            _rays = NewImage(rrt, Color.clear); _rays.sprite = s_rays;
            _rays2 = NewImage(rrt, Color.clear); _rays2.sprite = s_rays;
            for (int i = 0; i < Sparkles; i++)
            {
                _sparks[i] = NewImage(rrt, Color.white);
                _sparks[i].sprite = FxSprites.StarSprite;
            }
            _gem = NewImage(rrt, Color.white);
            _gem.preserveAspect = true;
            _gem.rectTransform.sizeDelta = new Vector2(200f, 200f);
            _gem.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);

            _tag = UiKit.Soft(NewText(rrt, "", 30));
            _tag.rectTransform.anchoredPosition = new Vector2(0f, -110f);
            _name = UiKit.Soft(NewText(rrt, "", 54));
            _name.rectTransform.anchoredPosition = new Vector2(0f, -160f);
            _sub = UiKit.Soft(NewText(rrt, "", 24));
            _sub.supportRichText = true;
            _sub.rectTransform.anchoredPosition = new Vector2(0f, -215f);

            _flash = NewImage(rrt, new Color(1f, 1f, 1f, 0f));
            Stretch(_flash.rectTransform);
            _reveal.SetActive(false);

            _summary = new GameObject("GemSummary", typeof(RectTransform)).GetComponent<RectTransform>();
            _summary.SetParent(root, false);
            _summary.anchoredPosition = new Vector2(0f, 70f);   // 결과 배너 아래(예전 「이번 판에 얻은 잼」 글자 자리)
            _summary.gameObject.SetActive(false);
        }

        // 빛살: 가운데에서 뻗는 쐐기 12개, 바깥으로 갈수록 옅게
        static Sprite MakeRays()
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float wedge = Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 6f)), 6f);
                    float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 6f);
                    float core = Mathf.Clamp01(1f - r * 2.2f) * 0.6f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(wedge * fade + core)));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        }

        Image NewImage(Transform parent, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        Text NewText(Transform parent, string text, int size)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            t.rectTransform.sizeDelta = new Vector2(1400f, 60f);
            return t;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
