using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Gameplay;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Features.Attack;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 화면 오른쪽 아래에 고정된 필살기 버튼(임시 UI, 코드로 만든다).
    /// 누르면 IInputService.PressSkill() → PlayerInputSystem이 플레이어 필살기를 요청한다. 키보드 Q도 같다.
    /// 재사용 대기 중엔 어두운 막이 아래에서부터 줄어들고 남은 초를 보여준다.
    /// </summary>
    public sealed class SkillButtonHud : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Vector2 size = new(170f, 170f);
        [SerializeField] Vector2 margin = new(60f, 60f);
        [SerializeField] Color readyColor = new(0.16f, 0.22f, 0.45f, 0.92f);
        [SerializeField] Color frameColor = new(1f, 0.82f, 0.3f, 1f);

        IWorld _world;
        IInputService _input;
        RectTransform _cooldownMask;
        Text _label, _cooldownText;
        Image _frame;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _input);
            if (!_label) Build();
        }

        void Update()
        {
            if (_world == null || !_label) return;
            float ratio = 0f, remain = 0f;
            foreach (var chunk in _world.EntityManager.GetChunks(typeof(PlayerTag), typeof(SpecialSkillComponent)))
            {
                if (chunk.Count == 0) continue;
                var sk = chunk.GetComponentArray<SpecialSkillComponent>()[0];
                ratio = sk.CooldownRatio;
                remain = sk.CooldownRemaining;
                break;
            }

            bool ready = remain <= 0f;
            _cooldownMask.anchorMax = new Vector2(1f, ratio);
            _cooldownText.text = ready ? "" : remain.ToString(remain < 1f ? "0.0" : "0");
            _label.color = ready ? frameColor : new Color(1f, 1f, 1f, 0.5f);
            _frame.color = ready ? frameColor : new Color(frameColor.r, frameColor.g, frameColor.b, 0.35f);
        }

        void OnClick() => _input?.PressSkill();

        void Build()
        {
            if (!FindFirstObjectByType<EventSystem>())
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 테두리(금색) → 안쪽 버튼
            _frame = NewImage("SkillButton", canvasGo.transform, frameColor);
            var frt = _frame.rectTransform;
            frt.anchorMin = frt.anchorMax = frt.pivot = new Vector2(1f, 0f);
            frt.anchoredPosition = new Vector2(-margin.x, margin.y);
            frt.sizeDelta = size;

            var body = NewImage("Body", frt, readyColor);
            Stretch(body.rectTransform, 6f);
            var button = _frame.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            button.onClick.AddListener(OnClick);

            // 재사용 대기 막: 아래에서부터 높이 = 남은 비율
            var mask = NewImage("Cooldown", body.rectTransform, new Color(0f, 0f, 0f, 0.6f));
            mask.raycastTarget = false;
            _cooldownMask = mask.rectTransform;
            _cooldownMask.anchorMin = Vector2.zero;
            _cooldownMask.anchorMax = new Vector2(1f, 0f);
            _cooldownMask.offsetMin = _cooldownMask.offsetMax = Vector2.zero;

            _label = NewText("Label", body.rectTransform, font, "필살기", 38);
            Stretch(_label.rectTransform, 0f);
            _cooldownText = NewText("CooldownText", body.rectTransform, font, "", 48);
            Stretch(_cooldownText.rectTransform, 0f);
            _cooldownText.color = Color.white;

            var key = NewText("Key", body.rectTransform, font, "Q", 24);
            var krt = key.rectTransform;
            krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(0f, 1f);
            krt.anchoredPosition = new Vector2(10f, -6f);
            krt.sizeDelta = new Vector2(40f, 32f);
            key.alignment = TextAnchor.UpperLeft;
            key.color = new Color(1f, 1f, 1f, 0.7f);
        }

        static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
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
