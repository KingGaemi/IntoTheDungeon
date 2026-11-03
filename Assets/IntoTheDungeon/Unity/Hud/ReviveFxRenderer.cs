using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Battle;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 무너진 해골(뼈 무더기) 위 예고: 「분쇄: 저지 15」(파티 최고 저지력으로 부술 수 있으면 청록, 모자라면 빨강) +
    /// 다시 일어나기까지 줄어드는 붉은 게이지. 무더기는 보랏빛으로 맥동한다. 거의 일어날 때 게이지가 깜빡인다.
    /// </summary>
    public sealed class ReviveFxRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] Color gaugeColor = new(0.85f, 0.3f, 1f, 1f);
        [SerializeField] float gaugeWidth = 0.9f;

        sealed class Fx
        {
            public GameObject Root;
            public SpriteRenderer Glow, GaugeBack, GaugeFill;
            public TextMesh Text, Shadow;
            public bool Used;
        }

        IWorld _world;
        BattleProgress _progress;
        int _layer;
        Sprite _pixel;
        Font _font;
        readonly Dictionary<Entity, Fx> _fx = new();
        readonly List<Entity> _remove = new();
        readonly List<Party.Member> _party = new(8);

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            int id = SortingLayer.NameToID("Projectiles");
            _layer = SortingLayer.IsValid(id) ? id : 0;
            if (!_font) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (!_pixel)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var w = (Color32)Color.white;
                tex.SetPixels32(new[] { w, w, w, w });
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0f, 0.5f), 2f);
            }
        }

        void LateUpdate()
        {
            foreach (var f in _fx.Values) f.Used = false;
            bool hidden = _world == null || (_progress != null && _progress.Phase == BattlePhase.Map);
            if (!hidden)
            {
                var em = _world.EntityManager;
                int best = -1;
                foreach (var chunk in em.GetChunks(typeof(ReviveComponent), typeof(TransformComponent)))
                {
                    var revives = chunk.GetComponentArray<ReviveComponent>();
                    var transforms = chunk.GetComponentArray<TransformComponent>();
                    var entities = chunk.GetEntities();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var rv = revives[i];
                        if (!rv.Downed) continue;
                        if (best < 0) best = StopPowerUi.BestPartyStop(em, _party);
                        var e = entities[i];
                        if (!_fx.TryGetValue(e, out var fx)) { fx = Create(); _fx[e] = fx; }
                        fx.Used = true;
                        var t = transforms[i];
                        fx.Root.transform.position = new Vector3(t.Position.X, t.Position.Y + t.Height, 0f);

                        float left = Mathf.Clamp01(1f - rv.Timer / rv.DownTime);
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
                        fx.Glow.color = new Color(0.7f, 0.25f, 1f, 0.25f + 0.2f * pulse);
                        fx.Glow.transform.localScale = new Vector3(1.6f + 0.15f * pulse, 0.55f, 1f);

                        bool can = best >= rv.StopRequired;
                        fx.Text.text = can ? $"분쇄: 저지 {rv.StopRequired}" : $"분쇄: 저지 {rv.StopRequired} 부족";
                        fx.Text.color = can ? StopPowerUi.Color : StopPowerUi.ShortColor;
                        fx.Shadow.text = fx.Text.text;

                        fx.GaugeFill.transform.localScale = new Vector3(gaugeWidth * left, 0.07f, 1f);
                        float blink = left < 0.25f ? 0.55f + 0.45f * Mathf.Sin(Time.time * 22f) : 1f;
                        fx.GaugeFill.color = new Color(gaugeColor.r, gaugeColor.g, gaugeColor.b, blink);
                        int order = 24000 + Mathf.Clamp(Mathf.RoundToInt(-t.Position.Y * 10f), -1500, 1500) * 5;
                        fx.Glow.sortingOrder = -29005;   // 바닥 바로 위(무더기 아래)
                        fx.GaugeBack.sortingOrder = order + 1;
                        fx.GaugeFill.sortingOrder = order + 2;
                        fx.Shadow.GetComponent<MeshRenderer>().sortingOrder = order + 3;
                        fx.Text.GetComponent<MeshRenderer>().sortingOrder = order + 4;
                    }
                }
            }
            _remove.Clear();
            foreach (var kv in _fx)
                if (!kv.Value.Used) { Destroy(kv.Value.Root); _remove.Add(kv.Key); }
            foreach (var e in _remove) _fx.Remove(e);
        }

        Fx Create()
        {
            var fx = new Fx { Root = new GameObject("ReviveFx") };
            fx.Root.transform.SetParent(transform, false);
            fx.Glow = NewSprite(fx.Root.transform, FxSprites.DotSprite, Vector3.zero);
            fx.Glow.sortingLayerID = 0;   // Default: 바닥 그림 위
            fx.GaugeBack = NewSprite(fx.Root.transform, _pixel, new Vector3(-gaugeWidth * 0.5f - 0.02f, 0.95f, 0f));
            fx.GaugeBack.transform.localScale = new Vector3(gaugeWidth + 0.04f, 0.11f, 1f);
            fx.GaugeBack.color = new Color(0.06f, 0.02f, 0.08f, 0.85f);
            fx.GaugeFill = NewSprite(fx.Root.transform, _pixel, new Vector3(-gaugeWidth * 0.5f, 0.95f, 0f));
            fx.Shadow = NewText(fx.Root.transform, new Vector3(0.03f, 1.05f - 0.03f, 0f));
            fx.Shadow.color = new Color(0f, 0f, 0f, 0.85f);
            fx.Text = NewText(fx.Root.transform, new Vector3(0f, 1.05f, 0f));
            return fx;
        }

        SpriteRenderer NewSprite(Transform parent, Sprite sprite, Vector3 local)
        {
            var go = new GameObject("Fx", typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = _layer;
            return sr;
        }

        TextMesh NewText(Transform parent, Vector3 local)
        {
            var go = new GameObject("T", typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.fontSize = 48;
            tm.characterSize = 0.045f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _font.material;
            mr.sortingLayerID = _layer;
            return tm;
        }
    }
}
