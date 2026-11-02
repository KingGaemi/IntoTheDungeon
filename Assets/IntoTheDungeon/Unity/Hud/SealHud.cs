using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 이번 판의 봉인석 배지(지도·정비 화면 왼쪽 위, 잼 버튼 오른쪽): 작은 봉인석 문양 + 단계.
    /// 단계는 새 게임 직전(타이틀의 봉인석 고르기)에만 정하고 판 도중엔 바꿀 수 없다. 마우스를 올리면 효과를 보여 준다.
    /// </summary>
    public sealed class SealHud : MonoBehaviour, IWorldInjectable
    {
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
        SealEmblem _emblem;
        Text _label, _tipText;
        Outline _plateOutline;

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
            if (_emblem.Tier != t) _emblem.SetTier(t, false);
            _emblem.Tick(Time.unscaledDeltaTime);

            var col = SealArt.TierColor(t);
            _label.text = t == 0 ? "봉인석 0단계\n<size=17>봉인됨</size>" : $"봉인석 {t}단계\n<size=17>{SealArt.TierTitle(t)}</size>";
            _label.color = t == 0 ? new Color(0.85f, 0.87f, 0.95f) : Color.Lerp(col, Color.white, 0.45f);
            _plateOutline.effectColor = t == 0 ? new Color(1f, 1f, 1f, 0.18f) : new Color(col.r, col.g, col.b, 0.55f);

            if (_tip.activeSelf != _hover.On) _tip.SetActive(_hover.On);
            if (_hover.On) _tipText.text = $"봉인석 {t}단계 (이번 판 고정)\n" + SealArt.Describe(t);
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

            // 어두운 바탕판(문양 + 글자, 마우스도 여기서 받음): 배경이 밝아도 잘 보이게
            var plate = NewImage(canvasGo.transform, new Color(0.05f, 0.05f, 0.09f, 0.85f));
            plate.raycastTarget = true;
            _root = plate.gameObject;
            _plateOutline = _root.AddComponent<Outline>();
            _plateOutline.effectDistance = new Vector2(2f, -2f);
            var rt = plate.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-525f, 474f);
            rt.sizeDelta = new Vector2(290f, 100f);
            _hover = _root.AddComponent<Hover>();

            _emblem = new SealEmblem(rt, 1.4f, big: false);
            _emblem.Rect.anchorMin = _emblem.Rect.anchorMax = new Vector2(0f, 0.5f);
            _emblem.Rect.anchoredPosition = new Vector2(52f, 0f);

            _label = NewText(rt, "", 24);
            _label.supportRichText = true;
            _label.alignment = TextAnchor.MiddleLeft;
            _label.lineSpacing = 0.9f;
            var lrt = _label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(106f, 0f); lrt.offsetMax = Vector2.zero;
            var outline = _label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 설명(마우스를 올리면 아래)
            var tip = NewImage(rt, new Color(0.06f, 0.05f, 0.1f, 0.96f));
            _tip = tip.gameObject;
            var trt = tip.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0f, 0f);
            trt.pivot = new Vector2(0f, 1f);
            trt.anchoredPosition = new Vector2(0f, -6f);
            trt.sizeDelta = new Vector2(600f, 140f);
            var tipOutline = tip.gameObject.AddComponent<Outline>();
            tipOutline.effectColor = new Color(0.72f, 0.45f, 1f, 1f);
            tipOutline.effectDistance = new Vector2(2f, -2f);
            _tipText = NewText(trt, "", 19);
            _tipText.alignment = TextAnchor.UpperLeft;
            var tt = _tipText.rectTransform;
            tt.anchorMin = Vector2.zero; tt.anchorMax = Vector2.one;
            tt.offsetMin = new Vector2(14f, 8f); tt.offsetMax = new Vector2(-14f, -10f);
            _tip.SetActive(false);
            _root.SetActive(false);
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
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }
    }
}
