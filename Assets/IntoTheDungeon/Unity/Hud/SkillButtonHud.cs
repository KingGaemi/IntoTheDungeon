using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 화면 아래 가운데에 파티원별 필살기 버튼을 한 줄로 놓는다(임시 UI, 코드로 만든다).
    /// 슬롯 순서 = Party.Collect 순서, 숫자키 1~4와 같다. 누르면 IInputService.PressSkill(슬롯).
    /// 버튼마다 재사용 대기 막(아래에서부터 줄어듦)·남은 초·체력 바를 보여주고, 쓰러진 파티원은 어둡게 막는다.
    /// </summary>
    public sealed class SkillButtonHud : MonoBehaviour, IWorldInjectable
    {
        const int MaxSlots = 4;

        [SerializeField] Vector2 size = new(150f, 150f);
        [SerializeField] float spacing = 24f;
        [SerializeField] float bottomMargin = 40f;
        [SerializeField] Color readyColor = new(0.16f, 0.22f, 0.45f, 0.92f);
        [SerializeField] Color frameColor = new(1f, 0.82f, 0.3f, 1f);
        [SerializeField] Color hpColor = new(0.35f, 0.85f, 0.4f, 1f);

        sealed class Slot
        {
            public GameObject Root;
            public Image Frame, Body;
            public RectTransform CooldownMask, HpFill;
            public Text Label, CooldownText;
            public Button Button;
        }

        IWorld _world;
        IInputService _input;
        BattleProgress _progress;
        GameObject _canvas;
        readonly Slot[] _slots = new Slot[MaxSlots];
        readonly List<Party.Member> _party = new(8);
        bool _built;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _input);
            world.TryGet(out _progress);
            if (!_built) Build();
        }

        void Update()
        {
            if (_world == null || !_built) return;
            bool show = _progress == null || _progress.Phase != BattlePhase.Map;   // 지도·정비 화면에선 숨김
            if (_canvas.activeSelf != show) _canvas.SetActive(show);
            if (!show) return;
            var em = _world.EntityManager;
            Party.Collect(em, _party);

            for (int i = 0; i < MaxSlots; i++)
            {
                var s = _slots[i];
                bool exists = i < _party.Count && em.HasComponent<SpecialSkillComponent>(_party[i].Entity);
                if (s.Root.activeSelf != exists) s.Root.SetActive(exists);
                if (!exists) continue;

                var m = _party[i];
                var sk = em.GetComponent<SpecialSkillComponent>(m.Entity);
                float hp = m.MaxHp > 0 ? Mathf.Clamp01((float)m.Hp / m.MaxHp) : 0f;
                s.HpFill.anchorMax = new Vector2(hp, 1f);

                if (!m.Alive)
                {
                    s.CooldownMask.anchorMax = Vector2.one;
                    s.CooldownText.text = "";
                    s.Label.text = "쓰러짐";
                    s.Label.color = new Color(1f, 1f, 1f, 0.4f);
                    s.Frame.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
                    s.Button.interactable = false;
                    continue;
                }

                bool ready = sk.CooldownRemaining <= 0f && !sk.Casting;
                s.Button.interactable = true;
                s.CooldownMask.anchorMax = new Vector2(1f, sk.CooldownRatio);
                s.CooldownText.text = sk.CooldownRemaining > 0f ? sk.CooldownRemaining.ToString(sk.CooldownRemaining < 1f ? "0.0" : "0") : "";
                s.Label.text = "필살기";
                s.Label.color = ready ? frameColor : new Color(1f, 1f, 1f, 0.5f);
                s.Frame.color = ready ? frameColor : new Color(frameColor.r, frameColor.g, frameColor.b, 0.35f);
            }
        }

        void Build()
        {
            _built = true;
            if (!FindFirstObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("SkillCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo;
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 아래 가운데 한 줄. 슬롯이 덜 차도 자리는 고정(버튼 위치가 바뀌지 않게)
            float totalW = MaxSlots * size.x + (MaxSlots - 1) * spacing;
            for (int i = 0; i < MaxSlots; i++)
            {
                float x = -totalW * 0.5f + size.x * 0.5f + i * (size.x + spacing);
                _slots[i] = BuildSlot(canvasGo.transform, font, i, new Vector2(x, bottomMargin));
            }
        }

        Slot BuildSlot(Transform parent, Font font, int index, Vector2 pos)
        {
            var s = new Slot();
            s.Frame = NewImage($"Skill{index + 1}", parent, frameColor);
            s.Root = s.Frame.gameObject;
            var frt = s.Frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.anchoredPosition = pos;
            frt.sizeDelta = size;

            s.Body = NewImage("Body", frt, readyColor);
            Stretch(s.Body.rectTransform, 5f);
            s.Button = s.Root.AddComponent<Button>();
            s.Button.targetGraphic = s.Body;
            int slot = index;
            s.Button.onClick.AddListener(() => _input?.PressSkill(slot));

            var mask = NewImage("Cooldown", s.Body.rectTransform, new Color(0f, 0f, 0f, 0.6f));
            s.CooldownMask = mask.rectTransform;
            s.CooldownMask.anchorMin = Vector2.zero;
            s.CooldownMask.anchorMax = new Vector2(1f, 0f);
            s.CooldownMask.offsetMin = s.CooldownMask.offsetMax = Vector2.zero;

            s.Label = NewText("Label", s.Body.rectTransform, font, "필살기", 32);
            Stretch(s.Label.rectTransform, 0f);
            s.CooldownText = NewText("CooldownText", s.Body.rectTransform, font, "", 44);
            Stretch(s.CooldownText.rectTransform, 0f);

            var key = NewText("Key", s.Body.rectTransform, font, (index + 1).ToString(), 24);
            var krt = key.rectTransform;
            krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(0f, 1f);
            krt.anchoredPosition = new Vector2(8f, -4f);
            krt.sizeDelta = new Vector2(36f, 30f);
            key.alignment = TextAnchor.UpperLeft;
            key.color = new Color(1f, 1f, 1f, 0.7f);

            // 체력 바(버튼 아래쪽 안)
            var hpBack = NewImage("HpBack", s.Body.rectTransform, new Color(0f, 0f, 0f, 0.7f));
            var hrt = hpBack.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
            hrt.offsetMin = new Vector2(8f, 8f); hrt.offsetMax = new Vector2(-8f, 20f);
            var hpFill = NewImage("HpFill", hrt, hpColor);
            s.HpFill = hpFill.rectTransform;
            s.HpFill.anchorMin = Vector2.zero; s.HpFill.anchorMax = Vector2.one;
            s.HpFill.offsetMin = s.HpFill.offsetMax = Vector2.zero;

            s.Root.SetActive(false);
            return s;
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = name.StartsWith("Skill") || name == "Body";
            return img;
        }

        static Text NewText(string name, Transform parent, Font font, string text, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.color = Color.white;
            return t;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
