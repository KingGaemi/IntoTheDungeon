using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.Abstractions.Services;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Features.Run;
using IntoTheDungeon.Features.Stats;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Core.Util;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 화면 아래 가운데 파티원별 필살기 버튼(코드로 만든다). 버튼 번호 = 파티 슬롯(PartySlotComponent) = 숫자키 1~4. 누르면 IInputService.PressSkill(슬롯).
    /// 글 없이 상태를 말한다: 재사용 대기 = 어두운 막 + 테두리 금 라디얼 링 / 시전 중 = 흰 테두리 + 아래 흰 진행선 /
    /// 준비 완료 순간 = 금 링 확산 플래시 1회 + 살짝 커졌다 돌아옴 / 거부(대기 중 누름 = 남은 초 점멸 + 좌우 흔들림, 마나 부족 = 마나 글자 빨강 점멸 2회) /
    /// 끊기 기회 = 가운데 망치+요구 저지력 청록 + 테두리 청록 맥동 + 버튼 위 청록 삼각형(첫 판에만 「지금 끊기!」 글자) / 부족 = 망치+숫자 빨강.
    /// 마우스를 올리면 필살기 툴팁. 쓰러진 파티원은 어둡게 막는다. 지도·정비 화면과 판이 끝난 뒤(결산 자리)엔 숨긴다. 연출은 unscaled 시간.
    /// </summary>
    public sealed class SkillButtonHud : MonoBehaviour, IWorldInjectable
    {
        const int MaxSlots = 4;
        const float RejectTime = 0.2f, ManaBlinkTime = 0.5f, FlashTime = 0.35f;   // (임시값) 거부 흔들림·마나 점멸·준비 플래시

        [SerializeField] Vector2 size = new(150f, 150f);
        [SerializeField] float spacing = 24f;
        [SerializeField] float bottomMargin = 40f;
        [SerializeField] Color readyColor = new(0.16f, 0.22f, 0.45f, 0.92f);
        [SerializeField] Color frameColor = new(1f, 0.82f, 0.3f, 1f);
        [SerializeField] Color hpColor = new(0.35f, 0.85f, 0.4f, 1f);

        sealed class Slot
        {
            public GameObject Root;
            public Image Frame, Body, Portrait;
            public Image Ring;             // 재사용 대기 라디얼 링(테두리, 12시부터 시계 방향으로 찬다)
            public Image Glow;             // 준비 완료 플래시(금 링 확산)
            public Image Tri;              // 끊기 기회: 버튼 위 청록 삼각형
            public GameObject CastLine;    // 시전 진행선(아래)
            public RectTransform CooldownMask, HpFill, CastFill;
            public Text Label, CooldownText, Hint;
            public Button Button;
            public GameObject StopBadge;   // 오른쪽 위: 청록 망치 + 내 저지력
            public Text StopText;
            public Text ManaText;          // 아래쪽: 마나 아이콘 + 비용(모자라면 「지금/비용」 빨강)
            public GameObject Empty;       // 아직 동료가 없는 칸(점선 + 「+」)
            public GameObject ReqGroup;    // 가운데: 망치 + 요구 저지력(끊기 기회 청록 / 부족 빨강)
            public Image ReqIcon;
            public Text ReqText;
            public Vector2 BasePos;
            // 상태
            public Entity Entity = Entity.Null;
            public bool WasReady, Clicked;
            public float RejectUntil, ManaBlinkUntil;
            public int ShownReq = int.MinValue, ShownStop = -1, HintState;
        }

        IWorld _world;
        IInputService _input;
        BattleProgress _progress;
        CharacterCatalog _catalog;
        GameObject _canvas;
        readonly Slot[] _slots = new Slot[MaxSlots];
        readonly List<Party.Member> _party = new(8);
        bool _built;

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _input);
            world.TryGet(out _progress);
            world.TryGet(out _catalog);
            if (!_built) Build();
        }

        void Update()
        {
            if (_world == null || !_built) return;
            var run = RunState.Current;
            // 지도·정비 화면과 판이 끝난 뒤(결산 띠 자리)엔 숨김
            bool show = (_progress == null || _progress.Phase != BattlePhase.Map) && !(run.Phase is RunPhase.Cleared or RunPhase.Failed);
            if (_canvas.activeSelf != show) _canvas.SetActive(show);
            if (!show) return;
            var em = _world.EntityManager;
            Party.Collect(em, _party);
            float now = Time.unscaledTime;
            bool firstRun = MetaProgress.Current.FirstRun;   // 첫 판에만 글자를 병기한다

            int partySize = run.PartySize;
            for (int i = 0; i < MaxSlots; i++)
            {
                var s = _slots[i];
                // 이 슬롯 파티원이 이번 방에 있으면 그 상태, 쓰러져 안 나왔으면(판엔 있음) "쓰러짐", 빈 슬롯이면 숨김
                bool present = Party.TryGetSlot(_party, i, out var m) && em.HasComponent<SpecialSkillComponent>(m.Entity);
                bool exists = present || i < partySize;
                if (s.Root.activeSelf != exists) s.Root.SetActive(exists);
                if (s.Empty.activeSelf == exists) s.Empty.SetActive(!exists);
                bool pressed = s.Clicked || Input.GetKeyDown(KeyCode.Alpha1 + i);   // 버튼 클릭 또는 숫자키(UnityInputDriver와 같은 키)
                s.Clicked = false;
                if (!exists) continue;

                var portrait = i < partySize ? PortraitTable.Of(run.Members[i]) : null;
                if (s.Portrait.sprite != portrait) s.Portrait.sprite = portrait;
                s.Portrait.enabled = portrait;
                s.Portrait.color = present && m.Alive ? new Color(1f, 1f, 1f, 0.45f) : new Color(0.4f, 0.4f, 0.4f, 0.3f);

                float hp = present && m.MaxHp > 0 ? Mathf.Clamp01((float)m.Hp / m.MaxHp) : 0f;
                s.HpFill.anchorMax = new Vector2(hp, 1f);

                if (!present || !m.Alive) { SetDown(s); continue; }

                var sk = em.GetComponent<SpecialSkillComponent>(m.Entity);
                bool cooled = sk.CooldownRemaining <= 0f && !sk.Casting;
                bool enoughMana = !em.TryGetComponent<DerivedStatsComponent>(m.Entity, out var ds) || ds.Mana >= sk.ManaCost;
                bool ready = cooled && enoughMana;
                if (s.Entity != m.Entity) { s.Entity = m.Entity; s.WasReady = ready; }   // 새 파티원·새 방은 전이가 아니라 플래시 없음
                if (ready && !s.WasReady) ReadyFlash(s);                                 // 준비 완료 순간 1회
                s.WasReady = ready;

                // 거부 피드백: 대기 중 누름 → 남은 초 점멸 + 흔들림 / 마나 부족 → 마나 글자 빨강 점멸
                if (pressed)
                {
                    if (!cooled) s.RejectUntil = now + RejectTime;
                    else if (!enoughMana) s.ManaBlinkUntil = now + ManaBlinkTime;
                }

                s.Button.interactable = true;
                s.CooldownMask.anchorMax = new Vector2(1f, sk.CooldownRatio);
                bool onCd = sk.CooldownRemaining > 0f;
                if (s.Ring.enabled != onCd) s.Ring.enabled = onCd;
                if (onCd) s.Ring.fillAmount = 1f - Mathf.Clamp01(sk.CooldownRatio);
                string cd = onCd ? sk.CooldownRemaining.ToString(sk.CooldownRemaining < 1f ? "0.0" : "0") : "";
                if (s.CooldownText.text != cd) s.CooldownText.text = cd;
                bool rejecting = now < s.RejectUntil;
                s.CooldownText.color = rejecting ? (Mathf.Sin(now * 50f) > 0f ? Color.white : new Color(1f, 1f, 1f, 0.25f)) : Color.white;
                s.Frame.rectTransform.anchoredPosition = rejecting ? s.BasePos + new Vector2(Mathf.Sin(now * 80f) * 4f, 0f) : s.BasePos;

                // 마나: 아이콘 + 비용. 재사용 대기는 끝났는데 마나가 모자라면 「지금/비용」을 빨갛게
                bool manaShort = cooled && !enoughMana;
                string manaTxt = manaShort ? $"{ds.Mana:0}/{sk.ManaCost:0}" : $"{sk.ManaCost:0}";
                if (s.ManaText.text != manaTxt) s.ManaText.text = manaTxt;
                bool manaBlink = now < s.ManaBlinkUntil && Mathf.Sin(now * Mathf.PI * 8f) < 0f;   // 4Hz → 0.5초에 2번
                s.ManaText.color = manaBlink ? new Color(UiKit.Negative.r, UiKit.Negative.g, UiKit.Negative.b, 0.15f)
                                 : manaShort ? UiKit.Negative : new Color(0.7f, 0.85f, 1f, 1f);

                // 저지력 배지(내 저지력)
                bool hasStop = sk.StopPower > 0;
                if (s.StopBadge.activeSelf != hasStop) s.StopBadge.SetActive(hasStop);
                if (s.ShownStop != sk.StopPower) { s.ShownStop = sk.StopPower; s.StopText.text = sk.StopPower.ToString(); }

                s.Label.text = "";
                s.Label.color = ready ? frameColor : new Color(1f, 1f, 1f, 0.5f);
                var frame = ready ? frameColor : new Color(frameColor.r, frameColor.g, frameColor.b, 0.35f);

                // 시전 중: 흰 테두리 + 아래 흰 진행선
                bool casting = sk.Casting;
                if (s.CastLine.activeSelf != casting) s.CastLine.SetActive(casting);
                if (casting)
                {
                    frame = Color.white;
                    s.CastFill.anchorMax = new Vector2(sk.Duration > 0f ? Mathf.Clamp01(sk.Timer / sk.Duration) : 1f, 1f);
                }

                // 끊기 기회: 이 필살기가 닿는 곳에서 적이 패턴 힘을 모으는 중이면 망치 + 요구 저지력(충분 청록·맥동 / 부족 빨강)
                int req = sk.StopPower > 0 ? ChargingInReach(em, m.Position, sk) : -1;
                bool chance = ready && req >= 0 && sk.StopPower >= req;
                bool shortOf = req >= 0 && sk.StopPower < req;
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 14f);
                bool showReq = chance || shortOf;
                if (s.ReqGroup.activeSelf != showReq) s.ReqGroup.SetActive(showReq);
                if (showReq)
                {
                    if (s.ShownReq != req) { s.ShownReq = req; s.ReqText.text = req.ToString(); }
                    var rc = chance ? Color.Lerp(UiKit.Stop, Color.white, 0.5f * pulse) : UiKit.Negative;
                    s.ReqText.color = rc;
                    s.ReqIcon.color = rc;
                }
                if (s.Tri.gameObject.activeSelf != chance) s.Tri.gameObject.SetActive(chance);
                if (chance)
                {
                    s.Tri.rectTransform.anchoredPosition = new Vector2(0f, 14f + 4f * Mathf.Sin(now * 10f));   // 위아래로 까딱
                    s.Tri.color = Color.Lerp(UiKit.Stop, Color.white, 0.4f * pulse);
                    frame = Color.Lerp(UiKit.Stop, Color.white, pulse);
                }
                // 첫 판에만 글자 병기(「지금 끊기!」 / 「저지 N 필요」)
                int hintState = !firstRun ? 0 : chance ? 1 : shortOf ? 2 + req : 0;
                if (hintState != s.HintState)
                {
                    s.HintState = hintState;
                    s.Hint.text = hintState == 0 ? "" : hintState == 1 ? "지금 끊기!" : $"저지 {req} 필요";
                }
                if (hintState != 0) s.Hint.color = chance ? Color.Lerp(UiKit.Stop, Color.white, pulse) : UiKit.Negative;
                s.StopText.color = shortOf ? Color.Lerp(UiKit.Negative, Color.white, 0.3f * pulse) : UiKit.Stop;

                s.Frame.color = frame;
                s.Frame.rectTransform.localScale = Vector3.one * (chance ? 1.04f + 0.06f * pulse : 1f);
            }
        }

        // 쓰러진 파티원: 어둡게 막고 상태 표시는 전부 끈다
        void SetDown(Slot s)
        {
            if (s.StopBadge.activeSelf) s.StopBadge.SetActive(false);
            if (s.ReqGroup.activeSelf) s.ReqGroup.SetActive(false);
            if (s.Tri.gameObject.activeSelf) s.Tri.gameObject.SetActive(false);
            if (s.CastLine.activeSelf) s.CastLine.SetActive(false);
            if (s.Ring.enabled) s.Ring.enabled = false;
            s.CooldownMask.anchorMax = Vector2.one;
            s.CooldownText.text = "";
            s.Label.text = "쓰러짐";
            s.ManaText.text = "";
            if (s.HintState != 0) { s.HintState = 0; s.Hint.text = ""; }
            s.Label.color = new Color(1f, 1f, 1f, 0.4f);
            s.Frame.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            s.Frame.rectTransform.localScale = Vector3.one;
            s.Frame.rectTransform.anchoredPosition = s.BasePos;
            s.Button.interactable = false;
            s.Entity = Entity.Null;
        }

        // 준비 완료 플래시: 금 링이 1.0 → 1.35배로 퍼지며 사라지고, 버튼 속이 살짝 커졌다 돌아온다
        void ReadyFlash(Slot s)
        {
            if (!s.Glow.gameObject.activeSelf) s.Glow.gameObject.SetActive(true);
            UiTween.Scale(s.Glow.rectTransform, Vector3.one, new Vector3(1.35f, 1.35f, 1f), FlashTime);
            UiTween.Fade(s.Glow, 0.9f, 0f, FlashTime);
            UiTween.Punch((RectTransform)s.Body.transform, 0.08f, 0.18f);
        }

        // 이 필살기가 닿는 곳에서 힘을 모으는 적 패턴(BossSlamComponent) 중 가장 낮은 요구 저지력(없으면 -1)
        // 도발: 가로 반경(유물 뿔피리 배율 반영, 깊이는 절반) / 검기·화살: 같은 줄(깊이 판정 안)에서 사거리 안
        static int ChargingInReach(IEntityManager em, Vec2 pos, in SpecialSkillComponent sk)
        {
            int best = -1;
            float rx, ry;
            if (sk.TauntRadius > 0f) { rx = sk.TauntRadius * CombatPerks.TauntRadiusMul; ry = rx * 0.5f; }
            else { rx = sk.WaveSpeed * sk.WaveLifetime + sk.WaveHalfExtents.X; ry = sk.WaveHalfExtents.Y + 0.25f; }
            foreach (var chunk in em.GetChunks(typeof(BossSlamComponent), typeof(TransformComponent)))
            {
                var slams = chunk.GetComponentArray<BossSlamComponent>();
                var transforms = chunk.GetComponentArray<TransformComponent>();
                for (int k = 0; k < chunk.Count; k++)
                {
                    if (!slams[k].Charging || slams[k].Fired) continue;
                    var d = transforms[k].Position - pos;
                    if (System.MathF.Abs(d.X) <= rx && System.MathF.Abs(d.Y) <= ry && (best < 0 || slams[k].StopRequired < best))
                        best = slams[k].StopRequired;
                }
            }
            return best;
        }

        // 툴팁: 지금 그 슬롯 파티원의 필살기(교체 특성이 있으면 교체 필살기 이름)
        TooltipContent SkillTip(int slot)
        {
            if (_world == null || !Party.TryGetSlot(_party, slot, out var m)) return null;
            var em = _world.EntityManager;
            if (!em.Exists(m.Entity) || !em.TryGetComponent<SpecialSkillComponent>(m.Entity, out var sk)) return null;
            return Tooltips.Skill(sk, PresetOf(slot));
        }

        SkillPreset PresetOf(int slot)
        {
            var run = RunState.Current;
            if (_catalog == null || run == null || slot < 0 || slot >= run.Members.Count) return SkillPreset.SwordWave;
            var def = _catalog.Get(run.Members[slot]);
            return def.AltSkill != def.Skill && run.BuildModifiers(slot).AltSpecial ? def.AltSkill : def.Skill;
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
            s.Empty = EmptySlotUi.Build(parent, font, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, size, "동료 영입", 56);
            s.Frame = NewImage($"Skill{index + 1}", parent, frameColor);
            s.Root = s.Frame.gameObject;
            var frt = s.Frame.rectTransform;
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0f);
            frt.pivot = new Vector2(0.5f, 0f);
            frt.anchoredPosition = pos;
            frt.sizeDelta = size;
            s.BasePos = pos;

            s.Frame.sprite = UiKit.FillSprite;   // 둥근 테(색이 깜빡이는 바깥 판)
            s.Frame.type = Image.Type.Sliced;
            s.Frame.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.55f);
            s.Frame.GetComponent<Shadow>().effectDistance = new Vector2(0f, -5f);

            // 준비 완료 플래시용 금 링(맨 뒤, 평소엔 꺼짐)
            s.Glow = NewImage("Glow", frt, frameColor);
            s.Glow.sprite = UiKit.GlowSprite;
            s.Glow.type = Image.Type.Sliced;
            Stretch(s.Glow.rectTransform, -12f);
            s.Glow.gameObject.SetActive(false);

            s.Body = NewImage("Body", frt, readyColor);
            s.Body.sprite = UiKit.FillSprite;
            s.Body.type = Image.Type.Sliced;
            Stretch(s.Body.rectTransform, 4f);
            s.Button = s.Root.AddComponent<Button>();
            s.Button.targetGraphic = s.Body;
            int slot = index;
            s.Button.onClick.AddListener(() => { _input?.PressSkill(slot); s.Clicked = true; });

            // 파티원 초상화(바탕에 흐리게, 글자 뒤)
            s.Portrait = NewImage("Portrait", s.Body.rectTransform, Color.white);
            s.Portrait.preserveAspect = true;
            Stretch(s.Portrait.rectTransform, 14f);

            var mask = NewImage("Cooldown", s.Body.rectTransform, new Color(0f, 0f, 0f, 0.6f));
            s.CooldownMask = mask.rectTransform;
            s.CooldownMask.anchorMin = Vector2.zero;
            s.CooldownMask.anchorMax = new Vector2(1f, 0f);
            s.CooldownMask.offsetMin = s.CooldownMask.offsetMax = Vector2.zero;

            s.Label = NewText("Label", s.Body.rectTransform, font, "필살기", 32);
            Stretch(s.Label.rectTransform, 0f);
            s.CooldownText = NewText("CooldownText", s.Body.rectTransform, font, "", 44);
            var crt = s.CooldownText.rectTransform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(0f, -12f);
            crt.sizeDelta = new Vector2(size.x, 60f);

            var key = NewText("Key", s.Body.rectTransform, font, (index + 1).ToString(), 24);
            var krt = key.rectTransform;
            krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(0f, 1f);
            krt.anchoredPosition = new Vector2(8f, -4f);
            krt.sizeDelta = new Vector2(36f, 30f);
            key.alignment = TextAnchor.UpperLeft;
            key.color = new Color(1f, 1f, 1f, 0.7f);

            // 저지력 배지(오른쪽 위): 어두운 바탕 + 청록 망치 아이콘 + 숫자
            var badge = NewImage("StopBadge", s.Body.rectTransform, new Color(0.03f, 0.08f, 0.09f, 0.85f));
            s.StopBadge = badge.gameObject;
            var brt = badge.rectTransform;
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-4f, -4f);
            brt.sizeDelta = new Vector2(66f, 32f);
            UiKit.Style(badge, new Color(0.03f, 0.08f, 0.09f, 0.9f), new Color(StopPowerUi.Color.r, StopPowerUi.Color.g, StopPowerUi.Color.b, 0.8f), shadow: false);
            var icon = NewImage("Icon", brt, StopPowerUi.Color);
            icon.sprite = StopPowerUi.Icon;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(3f, 0f);
            irt.sizeDelta = new Vector2(26f, 26f);
            s.StopText = NewText("Stop", brt, font, "10", 24);
            var srt = s.StopText.rectTransform;
            srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = new Vector2(30f, 0f); srt.offsetMax = new Vector2(-2f, 0f);
            s.StopText.alignment = TextAnchor.MiddleCenter;
            s.StopText.color = StopPowerUi.Color;

            // 가운데: 망치 + 요구 저지력(끊기 기회·부족일 때만)
            var req = new GameObject("Req", typeof(RectTransform));
            req.transform.SetParent(s.Body.rectTransform, false);
            var rrt = (RectTransform)req.transform;
            rrt.anchorMin = rrt.anchorMax = rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0f, 26f);
            rrt.sizeDelta = new Vector2(100f, 36f);
            s.ReqGroup = req;
            s.ReqIcon = NewImage("ReqIcon", rrt, UiKit.Stop);
            s.ReqIcon.sprite = StopPowerUi.Icon;
            s.ReqIcon.preserveAspect = true;
            var qrt = s.ReqIcon.rectTransform;
            qrt.anchorMin = qrt.anchorMax = qrt.pivot = new Vector2(0f, 0.5f);
            qrt.anchoredPosition = new Vector2(16f, 0f);
            qrt.sizeDelta = new Vector2(28f, 28f);
            s.ReqText = NewText("ReqText", rrt, font, "", 24);
            var trt = s.ReqText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(48f, 0f); trt.offsetMax = new Vector2(-8f, 0f);
            s.ReqText.alignment = TextAnchor.MiddleLeft;
            UiKit.Soft(s.ReqText);
            req.SetActive(false);

            // 첫 판 글자(「지금 끊기!」·「저지 N 필요」)
            s.Hint = NewText("Hint", s.Body.rectTransform, font, "", 20);
            var hrt0 = s.Hint.rectTransform;
            hrt0.anchorMin = hrt0.anchorMax = hrt0.pivot = new Vector2(0.5f, 0f);
            hrt0.anchoredPosition = new Vector2(0f, 50f);
            hrt0.sizeDelta = new Vector2(140f, 24f);
            UiKit.Soft(s.Hint);

            // 체력 바(버튼 아래쪽 안)
            var hpBack = NewImage("HpBack", s.Body.rectTransform, new Color(0f, 0f, 0f, 0.7f));
            var hrt = hpBack.rectTransform;
            hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
            hrt.offsetMin = new Vector2(8f, 8f); hrt.offsetMax = new Vector2(-8f, 20f);
            var hpFill = NewImage("HpFill", hrt, hpColor);
            s.HpFill = hpFill.rectTransform;
            s.HpFill.anchorMin = Vector2.zero; s.HpFill.anchorMax = Vector2.one;
            s.HpFill.offsetMin = s.HpFill.offsetMax = Vector2.zero;

            // 시전 진행선(체력 바 바로 위, 시전 중에만)
            var castBack = NewImage("CastLine", s.Body.rectTransform, new Color(1f, 1f, 1f, 0.25f));
            var cbrt = castBack.rectTransform;
            cbrt.anchorMin = new Vector2(0f, 0f); cbrt.anchorMax = new Vector2(1f, 0f); cbrt.pivot = new Vector2(0.5f, 0f);
            cbrt.offsetMin = new Vector2(8f, 21f); cbrt.offsetMax = new Vector2(-8f, 24f);
            var castFill = NewImage("CastFill", cbrt, Color.white);
            s.CastFill = castFill.rectTransform;
            s.CastFill.anchorMin = Vector2.zero; s.CastFill.anchorMax = Vector2.one;
            s.CastFill.offsetMin = s.CastFill.offsetMax = Vector2.zero;
            s.CastLine = castBack.gameObject;
            s.CastLine.SetActive(false);

            // 마나: 아이콘 + 숫자(왼쪽 아래, 체력 바 위)
            var manaIcon = NewImage("ManaIcon", s.Body.rectTransform, Color.white);
            manaIcon.sprite = UiIcons.Of(UiIcons.Mana);
            var mirt = manaIcon.rectTransform;
            mirt.anchorMin = mirt.anchorMax = mirt.pivot = new Vector2(0f, 0f);
            mirt.anchoredPosition = new Vector2(8f, 24f);
            mirt.sizeDelta = new Vector2(24f, 24f);
            s.ManaText = NewText("Mana", s.Body.rectTransform, font, "", 22);
            s.ManaText.alignment = TextAnchor.MiddleLeft;
            var mtr = s.ManaText.rectTransform;
            mtr.anchorMin = mtr.anchorMax = mtr.pivot = new Vector2(0f, 0f);
            mtr.anchoredPosition = new Vector2(34f, 24f);
            mtr.sizeDelta = new Vector2(100f, 24f);
            UiKit.Soft(s.ManaText);
            UiKit.Soft(s.Label);

            // 재사용 대기 라디얼 링(테두리 위, 12시부터 시계 방향으로 찬다)
            s.Ring = NewImage("Ring", frt, frameColor);
            s.Ring.sprite = RingSprite;
            s.Ring.type = Image.Type.Filled;
            s.Ring.fillMethod = Image.FillMethod.Radial360;
            s.Ring.fillOrigin = (int)Image.Origin360.Top;
            s.Ring.fillClockwise = true;
            s.Ring.fillAmount = 0f;
            Stretch(s.Ring.rectTransform, 0f);
            s.Ring.enabled = false;

            // 끊기 기회 삼각형(버튼 위, 아래를 가리킴)
            s.Tri = NewImage("Tri", frt, UiKit.Stop);
            s.Tri.sprite = TriSprite;
            var xrt = s.Tri.rectTransform;
            xrt.anchorMin = xrt.anchorMax = new Vector2(0.5f, 1f);
            xrt.pivot = new Vector2(0.5f, 0f);
            xrt.anchoredPosition = new Vector2(0f, 14f);
            xrt.sizeDelta = new Vector2(24f, 12f);
            s.Tri.gameObject.SetActive(false);

            UiTooltip.Bind(s.Root, () => SkillTip(slot));

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

        // ───────── 코드로 만든 그림: 둥근 사각 링(라디얼 채움용)·아래로 향한 삼각형 ─────────
        static Sprite s_ring, s_tri;
        static Sprite RingSprite { get { if (!s_ring) MakeSprites(); return s_ring; } }
        static Sprite TriSprite { get { if (!s_tri) MakeSprites(); return s_tri; } }

        static void MakeSprites()
        {
            const int n = 160; const float r = 11f, thick = 6.4f;   // 150px 버튼에 두께 6px
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = RoundRectDist(x + 0.5f, y + 0.5f, n, n, r);   // 안쪽 음수, 가장자리 0
                    float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(d + thick + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            s_ring = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);

            const int tw = 24, th = 12;
            var tri = new Texture2D(tw, th, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    float v = (y + 0.5f) / th;             // 0 아래(꼭짓점) ~ 1 위(밑변)
                    float half = tw * 0.5f * v;
                    float dx = Mathf.Abs(x + 0.5f - tw * 0.5f);
                    tri.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(half - dx + 0.5f)));
                }
            tri.Apply();
            s_tri = Sprite.Create(tri, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), 100f);
        }

        // 둥근 사각형까지의 거리(안쪽 음수)
        static float RoundRectDist(float x, float y, float w, float h, float r)
        {
            float qx = Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r);
            float qy = Mathf.Abs(y - h * 0.5f) - (h * 0.5f - r);
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }
    }
}
