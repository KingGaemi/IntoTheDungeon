using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 저지력 첫 안내(한 번씩만, PlayerPrefs): 처음 강타 예고를 볼 때 / 처음 끊었을 때 / 처음 저지력이 모자랄 때
    /// 화면 위쪽에 청록 망치 아이콘과 한 줄 안내가 잠깐 뜬다. 설명서 없이 플레이하면서 저지력을 익히게 하는 게 목적.
    /// </summary>
    public sealed class StopGuideHud : MonoBehaviour, IWorldInjectable
    {
        const float ShowTime = 5.5f, Fade = 0.35f;

        static readonly (string key, string text)[] Guides =
        {
            ("intro", "적이 강타를 준비합니다! 필살기 버튼 오른쪽 위 숫자가 <b>저지력</b> — 예고의 「저지 N」 이상이면 끊을 수 있어요"),
            ("success", "저지 성공! 패턴이 끊긴 적은 2초 동안 무력해집니다"),
            ("short", "저지력이 모자라 끊을 수 없는 패턴입니다 — 망치·철퇴 같은 장비로 필살기 <b>저지력</b>을 올리세요"),
        };

        IWorld _world;
        BattleProgress _progress;
        GameObject _panel;
        Text _text;
        CanvasGroup _group;
        readonly Queue<string> _queue = new();
        readonly HashSet<string> _queued = new();
        readonly List<Party.Member> _party = new(8);
        float _age = -1f;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            if (!_panel) Build();
            InterruptFxRenderer.Shown -= OnInterrupt;
            InterruptFxRenderer.Shown += OnInterrupt;
        }

        void OnDestroy() => InterruptFxRenderer.Shown -= OnInterrupt;

        void OnInterrupt(InterruptKind kind)
        {
            if (kind == InterruptKind.PatternBroken) Request("success");
            else if (kind == InterruptKind.StopFailed) Request("short");
        }

        void Request(string key)
        {
            if (_queued.Contains(key)) return;
            try { if (PlayerPrefs.GetInt("StopGuide_" + key, 0) == 1) return; } catch { return; }
            _queued.Add(key);
            _queue.Enqueue(key);
        }

        void Update()
        {
            if (!_panel || _world == null) return;

            // 강타 예고가 보이면: 파티가 끊을 수 있으면 사용법, 아니면 저지력 부족 안내
            if (_progress != null && _progress.Phase == BattlePhase.Fighting)
            {
                var em = _world.EntityManager;
                foreach (var chunk in em.GetChunks(typeof(BossSlamComponent)))
                {
                    var slams = chunk.GetComponentArray<BossSlamComponent>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        if (!slams[i].Charging || slams[i].Fired) continue;
                        Request(StopPowerUi.BestPartyStop(em, _party) >= slams[i].StopRequired ? "intro" : "short");
                    }
                }
            }

            if (_age < 0f)
            {
                if (_queue.Count == 0) { if (_panel.activeSelf) _panel.SetActive(false); return; }
                string key = _queue.Dequeue();
                foreach (var g in Guides) if (g.key == key) _text.text = g.text;
                try { PlayerPrefs.SetInt("StopGuide_" + key, 1); PlayerPrefs.Save(); } catch { }
                _age = 0f;
                _panel.SetActive(true);
            }

            _age += Time.unscaledDeltaTime;
            float a = _age < Fade ? _age / Fade : _age > ShowTime - Fade ? (ShowTime - _age) / Fade : 1f;
            _group.alpha = Mathf.Clamp01(a);
            _panel.transform.localScale = Vector3.one * (_age < Fade ? Mathf.Lerp(0.92f, 1f, _age / Fade) : 1f);
            if (_age >= ShowTime) _age = -1f;
        }

        void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo = new GameObject("StopGuideCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 236;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = new GameObject("Guide", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            _panel.transform.SetParent(canvasGo.transform, false);
            var bg = _panel.GetComponent<Image>();
            bg.color = new Color(0.03f, 0.07f, 0.09f, 0.92f);
            bg.raycastTarget = false;
            _group = _panel.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            var outline = _panel.AddComponent<Outline>();
            outline.effectColor = StopPowerUi.Color;
            outline.effectDistance = new Vector2(2f, -2f);
            var rt = (RectTransform)_panel.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 300f);
            rt.sizeDelta = new Vector2(1240f, 72f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(rt, false);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = StopPowerUi.Icon;
            icon.color = StopPowerUi.Color;
            icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(16f, 0f);
            irt.sizeDelta = new Vector2(44f, 44f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(rt, false);
            _text = textGo.GetComponent<Text>();
            _text.font = font;
            _text.fontSize = 24;
            _text.fontStyle = FontStyle.Bold;
            _text.alignment = TextAnchor.MiddleLeft;
            _text.color = new Color(0.9f, 0.98f, 0.97f, 1f);
            _text.supportRichText = true;
            _text.raycastTarget = false;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var trt = _text.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(74f, 4f); trt.offsetMax = new Vector2(-16f, -4f);

            _panel.SetActive(false);
        }
    }
}
