using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Core.ECS.Components;
using IntoTheDungeon.Features.Battle;
using IntoTheDungeon.Features.Attack;
using IntoTheDungeon.Features.Physics.Components;
using IntoTheDungeon.Features.Status;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 기절(저지로 걸린 그로기, HitStunComponent.GroggyTotal) 연출: 머리 둘레를 노란 도트 별이 빙글빙글 돌고,
    /// 그 위에 남은 기절 시간 게이지가 줄어든다. 패턴 파괴처럼 긴 기절은 별 3개, 평타 끊기는 2개. 처음엔 툭 튀어나온다.
    /// 살짝 움찔(짧은 경직)엔 안 나온다. 위치는 ECS 바닥 위치 + 높이 + 머리 높이.
    /// </summary>
    public sealed class StunFxRenderer : MonoBehaviour, IWorldInjectable
    {
        [SerializeField] float orbitX = 0.42f, orbitY = 0.13f;   // 별이 도는 타원(가로·세로 반지름)
        [SerializeField] float spinSpeed = 5f;                   // 초당 회전(라디안)
        [SerializeField] float starScale = 0.65f;                // 별 크기 배율(원래 0.4 월드 단위 기준)
        [SerializeField] Color gaugeColor = new(1f, 0.85f, 0.25f, 1f);
        [SerializeField] float gaugeWidth = 0.7f;

        sealed class Fx
        {
            public GameObject Root;
            public readonly List<SpriteRenderer> Stars = new();
            public SpriteRenderer GaugeBack, GaugeFill;
            public float Total, Age;
            public bool Used;
        }

        IWorld _world;
        BattleProgress _progress;
        int _layer;
        Sprite _pixel;
        readonly Dictionary<Entity, Fx> _fx = new();
        readonly List<Entity> _remove = new();

        public void Init(IWorld world)
        {
            _world = world;
            world.TryGet(out _progress);
            int id = SortingLayer.NameToID("Projectiles");   // 캐릭터보다 위
            _layer = SortingLayer.IsValid(id) ? id : 0;
            if (!_pixel)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var w = (Color32)Color.white;
                tex.SetPixels32(new[] { w, w, w, w });
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0f, 0.5f), 2f);   // 왼쪽 기준 1×1
            }
        }

        void LateUpdate()
        {
            foreach (var f in _fx.Values) f.Used = false;
            bool hidden = _world == null || (_progress != null && _progress.Phase == BattlePhase.Map);
            float dt = Time.deltaTime;   // 게임 시간(기절도 게임 시간으로 풀림)

            if (!hidden)
            {
                var em = _world.EntityManager;
                foreach (var chunk in em.GetChunks(typeof(HitStunComponent), typeof(TransformComponent), typeof(StatusComponent)))
                {
                    var stuns = chunk.GetComponentArray<HitStunComponent>();
                    var transforms = chunk.GetComponentArray<TransformComponent>();
                    var statuses = chunk.GetComponentArray<StatusComponent>();
                    var entities = chunk.GetEntities();
                    for (int i = 0; i < chunk.Count; i++)
                    {
                        var s = stuns[i];
                        if (s.GroggyTotal <= 0f || s.Remaining <= 0f || !statuses[i].IsAlive) continue;
                        var e = entities[i];
                        if (!_fx.TryGetValue(e, out var fx)) { fx = Create(); _fx[e] = fx; }
                        if (fx.Total != s.GroggyTotal) { fx.Total = s.GroggyTotal; fx.Age = 0f; SetStars(fx, s.GroggyTotal >= PatternBreak.PatternBreakStun ? 3 : 2); }
                        fx.Used = true;
                        fx.Age += dt;

                        float head = em.TryGetComponent<BodyComponent>(e, out var body) ? body.HeadHeight : 1.2f;
                        var t = transforms[i];
                        var center = new Vector3(t.Position.X, t.Position.Y + t.Height + head - 0.05f, 0f);
                        fx.Root.transform.position = center;
                        Animate(fx, s.Remaining / s.GroggyTotal, -t.Position.Y);
                    }
                }
            }

            _remove.Clear();
            foreach (var kv in _fx)
                if (!kv.Value.Used) { Destroy(kv.Value.Root); _remove.Add(kv.Key); }
            foreach (var e in _remove) _fx.Remove(e);
        }

        void Animate(Fx fx, float left, float depthOrder)
        {
            bool big = fx.Stars.Count > 2;
            float pop = fx.Age < 0.18f ? Mathf.Lerp(1.8f, 1f, fx.Age / 0.18f) : 1f;   // 처음엔 툭 튀어나옴
            float spin = Time.time * spinSpeed;
            float rx = orbitX * (big ? 1.15f : 1f), ry = orbitY * (big ? 1.15f : 1f);
            int baseOrder = 24000 + Mathf.Clamp(Mathf.RoundToInt(depthOrder * 10f), -1500, 1500) * 5;
            for (int k = 0; k < fx.Stars.Count; k++)
            {
                float a = spin + k * Mathf.PI * 2f / fx.Stars.Count;
                float front = Mathf.Sin(a);   // 아래쪽(앞)일수록 크고 진하게, 뒤쪽은 작고 흐리게
                var sr = fx.Stars[k];
                sr.transform.localPosition = new Vector3(Mathf.Cos(a) * rx, -front * ry, 0f);
                float sc = starScale * (big ? 1.1f : 1f) * pop * (0.8f + 0.25f * front);
                sr.transform.localScale = new Vector3(sc, sc, 1f);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg * 0.6f);
                sr.color = new Color(1f, 1f, 1f, 0.65f + 0.35f * front);
                sr.sortingOrder = baseOrder + (front > 0f ? 4 : -4);   // 앞쪽 별은 체력바·머리 위, 뒤쪽은 아래
            }

            // 남은 기절 게이지(별 위)
            float gy = ry + 0.28f;
            fx.GaugeBack.transform.localPosition = new Vector3(-gaugeWidth * 0.5f - 0.02f, gy, 0f);
            fx.GaugeBack.transform.localScale = new Vector3(gaugeWidth + 0.04f, 0.09f, 1f);
            fx.GaugeFill.transform.localPosition = new Vector3(-gaugeWidth * 0.5f, gy, 0f);
            fx.GaugeFill.transform.localScale = new Vector3(gaugeWidth * Mathf.Clamp01(left), 0.05f, 1f);
            float blink = left < 0.25f ? 0.6f + 0.4f * Mathf.Sin(Time.time * 20f) : 1f;   // 거의 풀리면 깜빡
            fx.GaugeFill.color = new Color(gaugeColor.r, gaugeColor.g, gaugeColor.b, blink);
            fx.GaugeBack.sortingOrder = baseOrder + 5;
            fx.GaugeFill.sortingOrder = baseOrder + 6;
        }

        Fx Create()
        {
            var fx = new Fx { Root = new GameObject("StunFx") };
            fx.Root.transform.SetParent(transform, false);
            fx.GaugeBack = NewRenderer(fx.Root.transform, _pixel, "GaugeBack");
            fx.GaugeBack.color = new Color(0.08f, 0.06f, 0.02f, 0.85f);
            fx.GaugeFill = NewRenderer(fx.Root.transform, _pixel, "GaugeFill");
            return fx;
        }

        void SetStars(Fx fx, int count)
        {
            while (fx.Stars.Count < count) fx.Stars.Add(NewRenderer(fx.Root.transform, FxSprites.StarSprite, "Star"));
            while (fx.Stars.Count > count) { Destroy(fx.Stars[^1].gameObject); fx.Stars.RemoveAt(fx.Stars.Count - 1); }
        }

        SpriteRenderer NewRenderer(Transform parent, Sprite sprite, string name)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = _layer;
            return sr;
        }
    }
}
