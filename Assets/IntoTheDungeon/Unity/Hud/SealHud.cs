using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 봉인석 단계 조절(지도·정비 화면 왼쪽 위, 잼 버튼 오른쪽): [-] 봉인석 N단계 [+].
    /// 마우스를 올리면 아래에 지금 단계의 몹 강화·보상 배율과 다음 단계 값을 보여 준다. 다음 전투부터 적용.
    /// </summary>
    public sealed class SealHud : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color low = new(0.55f, 0.6f, 0.7f, 1f);
        [SerializeField] Color high = new(0.85f, 0.25f, 0.9f, 1f);

        sealed class Hover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public bool On;
            public void OnPointerEnter(PointerEventData e) => On = true;
            public void OnPointerExit(PointerEventData e) => On = false;
        }

        BattleProgress _progress;
        Font _font;
        GameObject _root, _tip;
        Hover _hover;
        Image _body;
        Text _label, _tipText;
        Button _minus, _plus;

        public void Init(IWorld world)
        {
            world.TryGet(out _progress);
            if (!_root) Build();
        }

        void Update()
        {
            if (_progress == null || !_root) return;
            var run = RunState.Current;
            bool usable = _progress.Phase == BattlePhase.Map && run.Phase is RunPhase.ChoosingRoom or RunPhase.Camp;
            if (_root.activeSelf != usable) _root.SetActive(usable);
            if (!usable) return;

            int t = run.SealTier;
            float k = (float)t / SealStone.MaxTier;
            _body.color = Color.Lerp(low, high, k);
            _label.text = t == 0 ? "봉인석 0단계 (기본)" : $"봉인석 {t}단계";
            _minus.interactable = t > 0;
            _plus.interactable = t < SealStone.MaxTier;

            if (_tip.activeSelf != _hover.On) _tip.SetActive(_hover.On);
            if (_hover.On) _tipText.text = Describe(t) + (t < SealStone.MaxTier ? $"\n\n다음 단계({t + 1}): " + Short(t + 1) : "\n\n최고 단계");
        }

        static string Describe(int t) =>
            $"봉인석 {t}단계 (다음 전투부터)\n" +
            $"몹 체력 ×{SealStone.EnemyHpMul(t):0.00}   ·   몹 공격력 ×{SealStone.EnemyDamageMul(t):0.00}\n" +
            $"처치 경험치·골드 ×{SealStone.TotalRewardMul(t):0.00}   ·   드랍 확률 ×{SealStone.DropMul(t):0.0}\n" +
            $"좋은 등급 +{SealStone.RareBonus(t) * 100f:0}%p   ·   판 끝 잼 +{SealStone.EndGems(t)}";

        static string Short(int t) => $"체력 ×{SealStone.EnemyHpMul(t):0.00}, 공격 ×{SealStone.EnemyDamageMul(t):0.00}, 보상 ×{SealStone.TotalRewardMul(t):0.00}";

        void Change(int d)
        {
            var run = RunState.Current;
            run.SetSealTier(run.SealTier + d);
        }

        void Build()
        {
            var canvasGo = new GameObject("SealCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 241;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _body = NewImage(canvasGo.transform, low);
            _body.raycastTarget = true;
            _root = _body.gameObject;
            var rt = _body.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-520f, 478f);
            rt.sizeDelta = new Vector2(330f, 52f);
            _hover = _root.AddComponent<Hover>();

            _minus = SmallButton(rt, "-", new Vector2(-140f, 0f), () => Change(-1));
            _plus = SmallButton(rt, "+", new Vector2(140f, 0f), () => Change(1));
            _label = NewText(rt, "", 22);
            _label.color = new Color(0.08f, 0.06f, 0.1f, 1f);
            var lrt = _label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(46f, 0f); lrt.offsetMax = new Vector2(-46f, 0f);

            // 설명(마우스를 올리면 버튼 아래)
            var tip = NewImage(rt, new Color(0.06f, 0.05f, 0.1f, 0.96f));
            _tip = tip.gameObject;
            var trt = tip.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(0f, -6f);
            trt.sizeDelta = new Vector2(600f, 170f);
            var outline = tip.gameObject.AddComponent<Outline>();
            outline.effectColor = high;
            outline.effectDistance = new Vector2(2f, -2f);
            _tipText = NewText(trt, "", 19);
            _tipText.alignment = TextAnchor.UpperLeft;
            var tt = _tipText.rectTransform;
            tt.anchorMin = Vector2.zero; tt.anchorMax = Vector2.one;
            tt.offsetMin = new Vector2(14f, 8f); tt.offsetMax = new Vector2(-14f, -10f);
            _tip.SetActive(false);
            _root.SetActive(false);
        }

        Button SmallButton(RectTransform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var img = NewImage(parent, new Color(0.12f, 0.1f, 0.16f, 0.9f));
            img.raycastTarget = true;
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(42f, 42f);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            var t = NewText(r, text, 30);
            var trt = t.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            return b;
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

        Text NewText(Transform parent, string text, int fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = _font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }
    }
}
