using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 이번 판의 봉인석·잼 배지(지도·정비 화면 오른쪽 위, 골드 왼쪽): 작은 봉인석 문양 + 단계 + 끼운 잼 아이콘.
    /// 봉인석 단계와 잼은 새 게임 직전(타이틀의 봉인석 고르기)에만 정하고 판 도중엔 바꿀 수 없다.
    /// 마우스를 올리면 봉인석 효과와 끼운 잼(레벨·효과)을 함께 보여 준다.
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
        Image _plateImage;
        readonly Image[] _gemIcons = new Image[MetaProgress.MaxSlots];
        RectTransform _tipRect;
        Dictionary<string, Sprite> _gemSprites;

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
            _label.text = $"봉인석 {t}단계";

            // 끼운 잼 아이콘(빈 소켓은 흐린 점)
            var meta = MetaProgress.Current;
            for (int i = 0; i < _gemIcons.Length; i++)
            {
                var icon = _gemIcons[i];
                bool slot = i < meta.SlotCount;
                if (icon.enabled != slot) icon.enabled = slot;
                if (!slot) continue;
                var sp = GemSprite(meta.InSlot(i));
                icon.sprite = sp;
                icon.color = sp ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            }
            _label.color = t == 0 ? new Color(0.85f, 0.87f, 0.95f) : Color.Lerp(col, Color.white, 0.45f);
            UiKit.SetRim(_plateImage, t == 0 ? UiKit.RimDim : Color.Lerp(col, UiKit.Rim, 0.35f));

            if (_tip.activeSelf != _hover.On) _tip.SetActive(_hover.On);
            if (_hover.On)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"봉인석 {t}단계 · {(t == 0 ? "봉인됨" : SealArt.TierTitle(t))} (이번 판 고정)\n").Append(SealArt.Describe(t));
                sb.Append($"\n\n잼 {meta.EquippedCount}/{meta.SlotCount} (판에 들어가기 전에만 바꿈)");
                int lines = 0;
                for (int i = 0; i < meta.SlotCount; i++)
                {
                    var gem = GemPool.Find(meta.InSlot(i));
                    if (gem == null) continue;
                    int lv = meta.Level(gem.Id);
                    sb.Append($"\n· {gem.Name} Lv{lv}: {gem.Describe(lv)}");
                    lines++;
                }
                if (lines == 0) sb.Append("\n· 끼운 잼 없음");
                _tipText.text = sb.ToString();
                _tipRect.sizeDelta = new Vector2(620f, 190f + 28f * System.Math.Max(1, lines));
            }
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
            var plate = _plateImage = UiKit.Panel(NewImage(canvasGo.transform, Color.white));
            plate.raycastTarget = true;
            _root = plate.gameObject;
            var rt = plate.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-296f, -30f);   // RunInfoHud 골드(오른쪽 끝, 폭 250) 왼쪽
            rt.sizeDelta = new Vector2(250f, 64f);
            _hover = _root.AddComponent<Hover>();

            _emblem = new SealEmblem(rt, 0.9f, big: false);
            _emblem.Rect.anchorMin = _emblem.Rect.anchorMax = new Vector2(0f, 0.5f);
            _emblem.Rect.anchoredPosition = new Vector2(34f, 0f);

            _label = NewText(rt, "", 24);
            _label.supportRichText = true;
            _label.alignment = TextAnchor.MiddleLeft;
            _label.lineSpacing = 0.9f;
            var lrt = _label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(68f, 26f); lrt.offsetMax = new Vector2(0f, -2f);   // 위 절반: 단계, 아래 절반: 잼 아이콘
            for (int i = 0; i < _gemIcons.Length; i++)
            {
                var gi = NewImage(rt, Color.white);
                gi.preserveAspect = true;
                var girt = gi.rectTransform;
                girt.anchorMin = girt.anchorMax = girt.pivot = new Vector2(0f, 0f);
                girt.anchoredPosition = new Vector2(68f + i * 30f, 6f);
                girt.sizeDelta = new Vector2(26f, 26f);
                _gemIcons[i] = gi;
            }
            var outline = _label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 설명(마우스를 올리면 아래)
            var tip = UiKit.Panel(NewImage(rt, Color.white), rim: new Color(0.72f, 0.45f, 1f, 1f));
            _tip = tip.gameObject;
            var trt = _tipRect = tip.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(1f, 0f);   // 오른쪽 끝에 붙어 있으므로 왼쪽 아래로 펼친다
            trt.pivot = new Vector2(1f, 1f);
            trt.anchoredPosition = new Vector2(0f, -6f);
            trt.sizeDelta = new Vector2(600f, 140f);
            _tipText = NewText(trt, "", 19);
            _tipText.alignment = TextAnchor.UpperLeft;
            var tt = _tipText.rectTransform;
            tt.anchorMin = Vector2.zero; tt.anchorMax = Vector2.one;
            tt.offsetMin = new Vector2(14f, 8f); tt.offsetMax = new Vector2(-14f, -10f);
            _tip.SetActive(false);
            _root.SetActive(false);
        }

        Sprite GemSprite(string id)
        {
            if (_gemSprites == null)
            {
                _gemSprites = new Dictionary<string, Sprite>();
                foreach (var sp in Resources.LoadAll<Sprite>("GemIcons")) _gemSprites[sp.name] = sp;
            }
            return id != null && _gemSprites.TryGetValue(id, out var s) ? s : null;
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
