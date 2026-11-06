using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Core.Abstractions.World;
using IntoTheDungeon.Core.ECS.Abstractions;
using IntoTheDungeon.Features.Character;
using IntoTheDungeon.Unity.Bridge.View;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 캐릭터 그림 연출: 스쿼시(납작/길쭉), 실루엣 플래시, 팝. 정적으로 부르면 SpriteFxRunner가 그린다.
    /// SpriteRoot(CharacterAnimator가 붙은 트랜스폼)의 localScale만 소유한다 — HitShake는 localPosition, BossRenderer는 뷰 루트 스케일.
    /// 전부 실제 시간. 엔티티 그림이 없으면(죽어 사라짐·default) 조용히 무시.
    /// </summary>
    public static class SpriteFx
    {
        /// <summary>(sx, sy)배까지 inT초에 눌렸다가 outT초에 걸쳐 돌아온다.</summary>
        public static void Squash(Entity e, float sx, float sy, float inT, float outT)
            => SpriteFxRunner.Enqueue(new SpriteFxRunner.Request { Op = SpriteFxRunner.Op.Squash, E = e, A = sx, B = sy, C = inT, D = outT });

        /// <summary>실루엣이 흰색에서 c로 물들며 t초 동안 사라진다(적 피격·끊김). 실루엣 셰이더가 없으면 어두운 점멸로 대체.</summary>
        public static void Flash(Entity e, Color c, float t)
            => SpriteFxRunner.Enqueue(new SpriteFxRunner.Request { Op = SpriteFxRunner.Op.Flash, E = e, Col = c, C = t });

        /// <summary>from배에서 over배로 튀었다가 t초에 걸쳐 1배로(등장·강화 같은 강조).</summary>
        public static void Pop(Entity e, float from, float over, float t)
            => SpriteFxRunner.Enqueue(new SpriteFxRunner.Request { Op = SpriteFxRunner.Op.Pop, E = e, A = from, B = over, C = t });
    }

    /// <summary>SpriteFx 요청을 받아 매 프레임(LateUpdate, 실제 시간) 그린다. ViewBridge는 처음 쓸 때 찾는다(BossRenderer 방식).</summary>
    [AutoHud]
    public sealed class SpriteFxRunner : MonoBehaviour, IWorldInjectable
    {
        internal enum Op : byte { Squash, Flash, Pop }
        internal struct Request { public Op Op; public Entity E; public float A, B, C, D; public Color Col; }

        const int MaxPending = 64;
        const float FallbackTint = 0.35f, FallbackTime = 0.05f;   // 셰이더가 없을 때: 몸 색 RGB 0.35로 0.05초(알파 불변)

        sealed class Fx
        {
            public Transform Root;          // SpriteRoot
            public Vector3 Base;            // 처음 본 localScale(연출 전 기준)
            public SpriteRenderer Body, Flash;
            public bool SquashOn; public float Sx, Sy, InT, OutT, SquashAge;
            public bool PopOn; public float From, Over, PopT, PopAge;
            public bool FlashOn, Fallback; public Color FlashCol, Saved; public float FlashT, FlashAge;
        }

        static SpriteFxRunner s_instance;
        static readonly List<Request> s_pending = new(16);   // 러너가 아직 없을 때 들어온 요청
        static Material s_flashMat;
        static bool s_flashMatMissing;

        readonly Dictionary<Entity, Fx> _fx = new();
        readonly List<Entity> _done = new();
        ViewBridge _views;

        internal static void Enqueue(in Request r)
        {
            if (s_instance) { s_instance.Apply(r); return; }   // 바로 적용(같은 프레임에 시작)
            if (s_pending.Count >= MaxPending) s_pending.RemoveAt(0);
            s_pending.Add(r);
        }

        public void Init(IWorld world)
        {
            s_instance = this;
            s_pending.Clear();   // 씬이 바뀌면 지난 요청은 버린다
            RestoreAll();
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            RestoreAll();
        }

        void RestoreAll()
        {
            foreach (var fx in _fx.Values) Restore(fx);
            _fx.Clear();
        }

        // 텍스처 알파 × 꼭짓점 색 = 실루엣. TextMesh 글꼴 머티리얼이 쓰는 셰이더라 빌드에도 들어 있다. 없으면 null → 폴백
        static Material FlashMaterial
        {
            get
            {
                if (s_flashMat || s_flashMatMissing) return s_flashMat;
                var sh = Shader.Find("GUI/Text Shader");
                if (sh) s_flashMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                else s_flashMatMissing = true;
                return s_flashMat;
            }
        }

        void Apply(in Request r)
        {
            var fx = GetOrCreate(r.E);
            if (fx == null) return;
            switch (r.Op)
            {
                case Op.Squash:
                    // 눌리는 중에 더 약한 요청이 오면 그대로 둔다(연타로 떨리지 않게)
                    if (fx.SquashOn && fx.SquashAge < fx.InT && Mathf.Abs(fx.Sx - 1f) + Mathf.Abs(fx.Sy - 1f) >= Mathf.Abs(r.A - 1f) + Mathf.Abs(r.B - 1f)) return;
                    fx.SquashOn = true; fx.Sx = r.A; fx.Sy = r.B; fx.InT = Mathf.Max(0.01f, r.C); fx.OutT = Mathf.Max(0.01f, r.D); fx.SquashAge = 0f;
                    break;
                case Op.Pop:
                    fx.PopOn = true; fx.From = r.A; fx.Over = r.B; fx.PopT = Mathf.Max(0.01f, r.C); fx.PopAge = 0f;
                    break;
                case Op.Flash:
                    StartFlash(fx, r.Col, Mathf.Max(0.01f, r.C));
                    break;
            }
        }

        Fx GetOrCreate(Entity e)
        {
            if (_fx.TryGetValue(e, out var fx))
            {
                if (fx.Root && fx.Body) return fx;
                _fx.Remove(e);   // 그림이 사라짐
            }
            if (!_views) _views = FindFirstObjectByType<ViewBridge>();
            if (!_views || !_views.TryGetGameObject(e, out var go) || !go) return null;

            var anim = go.GetComponentInChildren<CharacterAnimator>();
            SpriteRenderer body = anim ? anim.GetComponent<SpriteRenderer>() : null;
            if (!body)
            {
                // CharacterAnimator가 없는 뷰: 그림자를 뺀 첫 SpriteRenderer(처음 한 번만 할당)
                foreach (var r in go.GetComponentsInChildren<SpriteRenderer>())
                    if (r && !r.GetComponent<GroundShadow>() && r.name != "Flash") { body = r; break; }
            }
            if (!body) return null;
            fx = new Fx { Root = body.transform, Base = body.transform.localScale, Body = body };
            _fx[e] = fx;
            return fx;
        }

        void StartFlash(Fx fx, Color c, float t)
        {
            if (fx.FlashOn && fx.FlashT - fx.FlashAge > t) return;   // 더 오래 남은 플래시가 있으면 그대로
            var mat = FlashMaterial;
            if (mat)
            {
                if (!fx.Flash) fx.Flash = MakeFlash(fx, mat);
                if (fx.Flash)
                {
                    if (fx.Fallback) EndFlash(fx);
                    fx.Fallback = false; fx.FlashOn = true; fx.FlashCol = c; fx.FlashT = t; fx.FlashAge = 0f;
                    return;
                }
            }
            // 폴백: 몸 색을 잠깐 어둡게(알파는 그대로). 이미 어두운 중이면 원래 색은 처음 저장한 것 유지
            if (!(fx.FlashOn && fx.Fallback)) fx.Saved = fx.Body.color;
            fx.Fallback = true; fx.FlashOn = true; fx.FlashCol = c; fx.FlashT = Mathf.Min(t, FallbackTime); fx.FlashAge = 0f;
            ApplyFallbackTint(fx);
        }

        static SpriteRenderer MakeFlash(Fx fx, Material mat)
        {
            if (!fx.Root) return null;
            var existing = fx.Root.Find("Flash");   // 이전 연출이 남긴 자식 재사용
            GameObject go;
            if (existing) go = existing.gameObject;
            else
            {
                go = new GameObject("Flash", typeof(SpriteRenderer));
                go.transform.SetParent(fx.Root, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
            var sr = go.GetComponent<SpriteRenderer>();
            if (!sr) sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.enabled = false;
            return sr;
        }

        void LateUpdate()
        {
            if (s_pending.Count > 0)
            {
                for (int i = 0; i < s_pending.Count; i++) Apply(s_pending[i]);
                s_pending.Clear();
            }

            float dt = Time.unscaledDeltaTime;
            foreach (var kv in _fx)
            {
                var fx = kv.Value;
                if (!fx.Root || !fx.Body) { _done.Add(kv.Key); continue; }
                if (!Animate(fx, dt)) { Restore(fx); _done.Add(kv.Key); }
            }
            for (int i = 0; i < _done.Count; i++) _fx.Remove(_done[i]);
            _done.Clear();
        }

        // 살아 있는 연출이 하나라도 있으면 true
        bool Animate(Fx fx, float dt)
        {
            bool any = false;
            var scale = fx.Base;

            if (fx.SquashOn)
            {
                fx.SquashAge += dt;
                float k;
                if (fx.SquashAge < fx.InT)
                {
                    float x = fx.SquashAge / fx.InT;
                    k = 1f - (1f - x) * (1f - x);   // 빠르게 눌림
                }
                else
                {
                    float x = (fx.SquashAge - fx.InT) / fx.OutT;
                    if (x >= 1f) { k = 0f; fx.SquashOn = false; }
                    else k = 1f - Mathf.SmoothStep(0f, 1f, x);   // 부드럽게 복귀
                }
                scale = new Vector3(scale.x * (1f + (fx.Sx - 1f) * k), scale.y * (1f + (fx.Sy - 1f) * k), scale.z);
                any |= fx.SquashOn;
            }

            if (fx.PopOn)
            {
                fx.PopAge += dt;
                float x = fx.PopAge / fx.PopT, f;
                if (x >= 1f) { f = 1f; fx.PopOn = false; }
                else if (x < 0.35f)
                {
                    float u = x / 0.35f;
                    f = Mathf.Lerp(fx.From, fx.Over, 1f - (1f - u) * (1f - u));
                }
                else f = Mathf.Lerp(fx.Over, 1f, Mathf.SmoothStep(0f, 1f, (x - 0.35f) / 0.65f));
                scale = new Vector3(scale.x * f, scale.y * f, scale.z);
                any |= fx.PopOn;
            }

            fx.Root.localScale = scale;

            if (fx.FlashOn)
            {
                fx.FlashAge += dt;
                float k = fx.FlashAge / fx.FlashT;
                if (k >= 1f) EndFlash(fx);
                else if (fx.Fallback) ApplyFallbackTint(fx);
                else if (fx.Flash)
                {
                    // 몸 그림을 그대로 따라가며(프레임·좌우·정렬) 흰 → 지정 색으로 물들고 옅어진다
                    var f = fx.Flash;
                    f.sprite = fx.Body.sprite;
                    f.flipX = fx.Body.flipX; f.flipY = fx.Body.flipY;
                    f.sortingLayerID = fx.Body.sortingLayerID;
                    f.sortingOrder = fx.Body.sortingOrder + 1;
                    var c = Color.Lerp(Color.white, fx.FlashCol, Mathf.Clamp01(k * 1.6f));
                    c.a = (k < 0.4f ? 1f : 1f - (k - 0.4f) / 0.6f) * fx.Body.color.a;
                    f.color = c;
                    if (!f.enabled) f.enabled = true;
                }
                else EndFlash(fx);
                any |= fx.FlashOn;
            }
            return any;
        }

        static void ApplyFallbackTint(Fx fx)
        {
            if (!fx.Body) return;
            var c = fx.Body.color;
            fx.Body.color = new Color(FallbackTint, FallbackTint, FallbackTint, c.a);
        }

        static void EndFlash(Fx fx)
        {
            if (fx.Fallback && fx.Body)
            {
                var c = fx.Body.color;
                fx.Body.color = new Color(fx.Saved.r, fx.Saved.g, fx.Saved.b, c.a);   // 알파는 지금 값(사라지는 중일 수 있음)
            }
            if (fx.Flash) fx.Flash.enabled = false;
            fx.FlashOn = false; fx.Fallback = false;
        }

        static void Restore(Fx fx)
        {
            if (fx.FlashOn) EndFlash(fx);
            if (fx.Root) fx.Root.localScale = fx.Base;
            fx.SquashOn = fx.PopOn = false;
        }
    }
}
