using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Run;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 바닥에 떨어진 아이템(LootDropComponent)을 등급 색 상자 + 아이콘(ItemIconTable)으로 그린다.
    /// 상자 크기는 가방에서 차지하는 칸 수에 비례, 떨어질 때 한 번 튀고 이후 살짝 떠서 반짝인다. 주우면(승리) 사라진다.
    /// 등급이 높을수록 눈에 띄게: 바닥 빛 웅덩이가 크고 진해지고, 고급부터 등급 색 빛기둥(희귀·영웅은 더 높게)과 반짝이,
    /// 땅에 닿는 순간 고리가 퍼지고(등급이 높을수록 크게), 영웅은 떨어진 직후 기둥이 크게 번쩍인다. 머리 위엔 이름표.
    /// 방을 깨면(Collecting) 하나씩 화면 오른쪽 아래 가방으로 휘어 날아 들어가고, 들어갈 때마다 가방이 통 튀며 개수가 오른다.
    /// </summary>
    public sealed class LootRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] float cellSize = 0.16f;      // 가방 한 칸 = 월드 이만큼
        [SerializeField] float hoverHeight = 0.15f;
        [SerializeField] float bounceHeight = 0.5f;
        [SerializeField] float bounceTime = 0.45f;

        [Header("등급별 연출(일반·고급·희귀·영웅 순)")]
        [SerializeField] float[] glowSize = { 0.5f, 0.8f, 1.1f, 1.5f };
        [SerializeField] float[] glowAlpha = { 0.2f, 0.4f, 0.55f, 0.75f };
        [SerializeField] float[] beamHeight = { 0f, 1.4f, 2.6f, 4.2f };
        [SerializeField] float[] beamWidth = { 0f, 0.35f, 0.55f, 0.8f };
        [SerializeField] float[] beamAlpha = { 0f, 0.35f, 0.5f, 0.65f };
        [SerializeField] int[] sparkles = { 0, 2, 5, 9 };
        [SerializeField] float[] landRing = { 0.8f, 1.2f, 1.8f, 2.8f };
        [Tooltip("떨어진 직후 기둥이 커졌다 가라앉는 시간")]
        [SerializeField] float flareTime = 1.2f;

        const int MaxSparkles = 9;
        const float LandRingTime = 0.6f;

        public static Color RarityColor(ItemRarity r) => r switch
        {
            ItemRarity.Uncommon => new Color(0.35f, 0.85f, 0.4f, 1f),
            ItemRarity.Rare => new Color(0.3f, 0.6f, 1f, 1f),
            ItemRarity.Epic => new Color(0.72f, 0.42f, 1f, 1f),
            _ => new Color(0.85f, 0.85f, 0.85f, 1f)
        };

        sealed class Icon
        {
            public Transform Root;
            public SpriteRenderer Border, Fill, Art;
            public SpriteRenderer Glow, Beam, Ring, Flash;
            public SpriteRenderer[] Sparkles;
            public TextMesh Label, LabelShadow;
            public MeshRenderer LabelRenderer, LabelShadowRenderer;
            public int ShownItem = -1;
        }

        [Header("가방으로 날아가기")]
        [SerializeField] float flyArc = 1.2f;   // 날아가는 길이 위로 휘는 높이(월드)

        IWorld _world;
        RectTransform _bag;
        Text _bagCount;
        float _bagPulse;
        int _arrived;
        readonly List<SpriteRenderer> _tmp = new();
        Sprite _sprite;
        Font _font;
        int _topLayer;
        readonly List<Icon> _pool = new();

        public void Init(IWorld world)
        {
            _world = world;
            if (!_sprite)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var white = (Color32)Color.white;
                tex.SetPixels32(new[] { white, white, white, white });
                tex.Apply();
                _sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0f), 2f);   // 아래 가운데 기준(바닥에 놓임)
            }
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int id = SortingLayer.NameToID("Projectiles");   // 이름표는 캐릭터보다 위
            _topLayer = SortingLayer.IsValid(id) ? id : 0;
            if (!_bag) BuildBag();
        }

        void LateUpdate()
        {
            int used = 0, collecting = 0, arrived = 0;
            if (_world != null)
            {
                float t = Time.time;
                var bagWorld = BagWorld();
                foreach (var chunk in _world.EntityManager.GetChunks(typeof(LootDropComponent)))
                {
                    var drops = chunk.GetComponentArray<LootDropComponent>();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var d = drops[i];
                        if (d.Collecting) { collecting++; if (d.Collected) arrived++; }
                        if (d.Collected) continue;
                        var item = ItemPool.Get(d.ItemIndex);
                        if (item == null) continue;
                        var icon = Get(used++);
                        Draw(icon, d, item, t);
                        if (d.Collecting && d.CollectAge >= d.CollectDelay) Fly(icon, d, bagWorld);
                    }
                }
            }
            for (int i = used; i < _pool.Count; i++)
                if (_pool[i].Root.gameObject.activeSelf) _pool[i].Root.gameObject.SetActive(false);
            UpdateBag(collecting, arrived);
        }

        // ───────── 가방으로 날아가기 ─────────

        // 처음엔 천천히, 끝으로 갈수록 빨라지며 위로 휜 길을 따라 작아지며 들어간다
        void Fly(Icon icon, in LootDropComponent d, Vector3 bag)
        {
            float k = Mathf.Clamp01((d.CollectAge - d.CollectDelay) / LootDropComponent.FlyTime);
            float e = k * k;
            var from = icon.Root.position;
            icon.Root.position = Vector3.Lerp(from, bag, e) + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * flyArc, 0f);
            icon.Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, e);

            // 바닥 연출은 떠나는 순간 꺼지고 아이템만 살짝 하얗게 빛나며 날아감
            icon.Glow.enabled = icon.Beam.enabled = icon.Ring.enabled = false;
            foreach (var s in icon.Sparkles) s.enabled = false;
            icon.Label.color = icon.LabelShadow.color = Color.clear;
            icon.Flash.enabled = true;
            icon.Flash.transform.localScale = Vector3.one * 0.6f;
            icon.Flash.color = new Color(1f, 1f, 1f, 0.5f * (1f - k));

            // 날아갈 땐 모든 것 위에
            const int top = 32500;
            icon.Root.GetComponentsInChildren(true, _tmp);
            foreach (var r in _tmp) r.sortingLayerID = _topLayer;
            icon.Border.sortingOrder = top; icon.Fill.sortingOrder = top + 1; icon.Art.sortingOrder = top + 2; icon.Flash.sortingOrder = top + 3;
        }

        Vector3 BagWorld()
        {
            var cam = Camera.main;
            if (!cam || !_bag) return Vector3.zero;
            var p = cam.ScreenToWorldPoint(new Vector3(_bag.position.x, _bag.position.y, -cam.transform.position.z));
            p.z = 0f;
            return p;
        }

        void UpdateBag(int collecting, int arrived)
        {
            if (!_bag) return;
            bool show = collecting > 0;
            if (_bag.gameObject.activeSelf != show) _bag.gameObject.SetActive(show);
            if (!show) { _arrived = 0; return; }
            if (arrived > _arrived) _bagPulse = 1f;   // 하나 들어갈 때마다 통
            _arrived = arrived;
            _bagCount.text = $"+{arrived}";
            _bagPulse = Mathf.Max(0f, _bagPulse - Time.deltaTime * 5f);
            _bag.localScale = Vector3.one * (1f + 0.25f * _bagPulse);
        }

        void BuildBag()
        {
            var canvasGo = new GameObject("LootBagCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 95;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 금테 두른 가죽 가방(덮개 + "가방" + 들어간 개수)
            var frame = UiImage(canvasGo.transform, "Bag", new Color(1f, 0.82f, 0.3f, 1f));
            frame.anchorMin = frame.anchorMax = new Vector2(1f, 0f);
            frame.anchoredPosition = new Vector2(-120f, 130f);
            frame.sizeDelta = new Vector2(140f, 120f);
            var body = UiImage(frame, "Body", new Color(0.32f, 0.2f, 0.12f, 1f));
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(4f, 4f); body.offsetMax = new Vector2(-4f, -4f);
            var flap = UiImage(frame, "Flap", new Color(0.45f, 0.28f, 0.16f, 1f));
            flap.anchorMin = new Vector2(0f, 1f); flap.anchorMax = Vector2.one;
            flap.offsetMin = new Vector2(4f, -42f); flap.offsetMax = new Vector2(-4f, -4f);

            var title = UiText(frame, "가방", 24, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -42f), new Vector2(0f, -4f));
            title.color = new Color(1f, 0.9f, 0.6f, 1f);
            _bagCount = UiText(frame, "", 34, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 72f));
            _bag = frame;
            frame.gameObject.SetActive(false);
        }

        static RectTransform UiImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        Text UiText(RectTransform parent, string text, int size, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var t = go.GetComponent<Text>();
            t.font = _font; t.text = text; t.fontSize = size; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.raycastTarget = false;
            return t;
        }

        void Draw(Icon icon, in LootDropComponent d, ItemDef item, float t)
        {
            int tier = Mathf.Clamp((int)item.Rarity, 0, 3);
            var c = RarityColor(item.Rarity);
            var ground = new Vector3(d.Position.X, d.Position.Y, 0f);
            icon.Root.position = ground;
            icon.Root.localScale = Vector3.one;
            icon.Root.GetComponentsInChildren(true, _tmp);
            foreach (var r in _tmp) r.sortingLayerID = 0;   // 날던 그림을 다른 아이템이 물려받았을 수 있으니 바닥 레이어로

            // 떨어질 때 포물선으로 한 번 튀고, 그 뒤엔 살짝 떠서 위아래로 흔들림
            float y = d.Age < bounceTime
                ? 4f * bounceHeight * (d.Age / bounceTime) * (1f - d.Age / bounceTime)
                : hoverHeight + Mathf.Sin(t * 3f + d.ItemIndex) * 0.04f;
            var body = new Vector3(0f, y, 0f);

            var size = new Vector2(item.W * cellSize, item.H * cellSize);
            icon.Border.transform.localScale = new Vector3(size.x + 0.04f, size.y + 0.04f, 1f);
            icon.Border.transform.localPosition = body + new Vector3(0f, -0.02f, 0f);
            icon.Fill.transform.localScale = new Vector3(size.x, size.y, 1f);
            icon.Fill.transform.localPosition = body;

            // 아이콘 그림(있으면 상자 안에 맞춰 크게)
            var art = ItemIconTable.Icon(item.Id);
            icon.Art.sprite = art;
            icon.Art.enabled = art;
            if (art)
            {
                var b = art.bounds.size;
                float k = Mathf.Min(size.x / Mathf.Max(b.x, 1e-4f), size.y / Mathf.Max(b.y, 1e-4f)) * 0.9f;
                icon.Art.transform.localScale = new Vector3(k, k, 1f);
                icon.Art.transform.localPosition = body + new Vector3(0f, size.y * 0.5f, 0f);
            }

            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 4f + d.ItemIndex * 1.7f);   // 반짝임
            icon.Fill.color = new Color(c.r * pulse, c.g * pulse, c.b * pulse, art ? 0.55f : 1f);   // 아이콘이 있으면 바탕은 옅게

            int order = Mathf.Clamp(Mathf.RoundToInt(-d.Position.Y * 100f), short.MinValue + 10, short.MaxValue - 10);
            icon.Glow.sortingOrder = order - 4;
            icon.Ring.sortingOrder = order - 3;
            icon.Beam.sortingOrder = order - 3;
            icon.Border.sortingOrder = order - 2;   // 같은 깊이 캐릭터보다 뒤(바닥에 놓인 것)
            icon.Fill.sortingOrder = order - 1;
            icon.Art.sortingOrder = order - 1;
            icon.Flash.sortingOrder = order;
            foreach (var s in icon.Sparkles) s.sortingOrder = order;

            float landed = d.Age - bounceTime;                                   // 땅에 닿은 뒤 지난 시간(음수면 아직 공중)
            float flare = landed < 0f ? 0f : Mathf.Clamp01(1f - landed / flareTime); // 닿는 순간 1 → 0
            float glowPulse = 0.85f + 0.15f * Mathf.Sin(t * 2.5f + d.ItemIndex);

            // 바닥 빛 웅덩이(납작한 타원)
            float gs = glowSize[tier] * (1f + 0.6f * flare);
            icon.Glow.enabled = true;
            icon.Glow.transform.localScale = new Vector3(gs, gs * 0.4f, 1f);
            icon.Glow.color = new Color(c.r, c.g, c.b, glowAlpha[tier] * glowPulse * (landed < 0f ? 0.4f : 1f) + 0.3f * flare);

            // 빛기둥(고급부터). 떨어진 직후엔 높고 진하게 번쩍였다가 가라앉음(영웅일수록 크게)
            bool beam = beamHeight[tier] > 0f;
            icon.Beam.enabled = beam;
            if (beam)
            {
                float boost = 1f + flare * (0.4f + 0.3f * tier);
                icon.Beam.transform.localScale = new Vector3(beamWidth[tier] * 2f * (0.9f + 0.1f * glowPulse), beamHeight[tier] * boost, 1f);
                icon.Beam.color = new Color(c.r, c.g, c.b, Mathf.Min(1f, beamAlpha[tier] * glowPulse * boost));
            }

            // 땅에 닿는 순간 퍼지는 고리 + 하얀 번쩍임
            bool ringOn = landed >= 0f && landed < LandRingTime;
            icon.Ring.enabled = icon.Flash.enabled = ringOn;
            if (ringOn)
            {
                float k = landed / LandRingTime;
                float rs = Mathf.Lerp(0.2f, landRing[tier], 1f - (1f - k) * (1f - k));
                icon.Ring.transform.localScale = new Vector3(rs, rs * 0.4f, 1f);
                icon.Ring.color = new Color(c.r, c.g, c.b, 1f - k);
                float fs = (0.5f + 0.25f * tier) * (1f - k * 0.5f);
                icon.Flash.transform.localScale = new Vector3(fs, fs, 1f);
                icon.Flash.transform.localPosition = body + new Vector3(0f, size.y * 0.5f, 0f);
                icon.Flash.color = new Color(1f, 1f, 1f, 0.9f * (1f - k) * (1f - k));
            }

            // 반짝이: 기둥을 따라 위로 올라가며 깜빡임(등급이 높을수록 많이)
            int n = sparkles[tier];
            for (int s = 0; s < icon.Sparkles.Length; s++)
            {
                var sp = icon.Sparkles[s];
                bool on = s < n && landed >= 0f;
                sp.enabled = on;
                if (!on) continue;
                float seed = d.ItemIndex * 0.37f + s * 1.618f;
                float phase = Mathf.Repeat(t * 0.6f + s / (float)n + seed, 1f);
                float h = Mathf.Max(0.8f, beamHeight[tier] * 0.7f);
                float x = Mathf.Sin(s * 2.4f + seed * 3f) * (0.12f + 0.08f * tier) + Mathf.Sin(t * 2f + s) * 0.04f;
                sp.transform.localPosition = new Vector3(x, 0.1f + phase * h, 0f);
                float a = Mathf.Sin(phase * Mathf.PI);
                sp.transform.localScale = Vector3.one * (0.05f + 0.03f * tier) * (0.6f + 0.4f * a);
                sp.color = new Color(Mathf.Lerp(1f, c.r, 0.4f), Mathf.Lerp(1f, c.g, 0.4f), Mathf.Lerp(1f, c.b, 0.4f), a);
            }

            // 이름표(등급 색)
            if (icon.ShownItem != item.Index)
            {
                icon.ShownItem = item.Index;
                icon.Label.text = icon.LabelShadow.text = item.Name;
            }
            float labelY = y + size.y + 0.12f;
            icon.Label.transform.localPosition = new Vector3(0f, labelY, 0f);
            icon.LabelShadow.transform.localPosition = new Vector3(0.015f, labelY - 0.015f, 0f);
            float la = landed < 0f ? 0f : Mathf.Clamp01(landed / 0.2f);
            icon.Label.color = new Color(Mathf.Lerp(c.r, 1f, 0.2f), Mathf.Lerp(c.g, 1f, 0.2f), Mathf.Lerp(c.b, 1f, 0.2f), la);
            icon.LabelShadow.color = new Color(0f, 0f, 0f, 0.85f * la);
            icon.LabelShadowRenderer.sortingOrder = 32000 + order % 300;
            icon.LabelRenderer.sortingOrder = icon.LabelShadowRenderer.sortingOrder + 1;
        }

        Icon Get(int index)
        {
            if (index < _pool.Count)
            {
                var ic = _pool[index];
                if (!ic.Root.gameObject.activeSelf) ic.Root.gameObject.SetActive(true);
                return ic;
            }
            var root = new GameObject("Loot").transform;
            root.SetParent(transform, false);
            var icon = new Icon { Root = root };
            icon.Glow = Part(root, "Glow", FxSprites.DotSprite, Color.white);
            icon.Beam = Part(root, "Beam", FxSprites.BeamSprite, Color.white);
            icon.Ring = Part(root, "LandRing", FxSprites.RingSprite, Color.white);
            icon.Border = Part(root, "Border", _sprite, new Color(0f, 0f, 0f, 0.9f));
            icon.Fill = Part(root, "Fill", _sprite, Color.white);
            icon.Art = Part(root, "Art", null, Color.white);
            icon.Flash = Part(root, "Flash", FxSprites.DotSprite, Color.white);
            icon.Sparkles = new SpriteRenderer[MaxSparkles];
            for (int i = 0; i < MaxSparkles; i++) icon.Sparkles[i] = Part(root, "Sparkle", FxSprites.DotSprite, Color.white);
            icon.LabelShadow = Label(root, "LabelShadow", out icon.LabelShadowRenderer);
            icon.Label = Label(root, "Label", out icon.LabelRenderer);
            _pool.Add(icon);
            return icon;
        }

        SpriteRenderer Part(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            return r;
        }

        TextMesh Label(Transform parent, string name, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 48;
            tm.characterSize = 0.03f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _font.material;
            renderer.sortingLayerID = _topLayer;
            return tm;
        }
    }
}
