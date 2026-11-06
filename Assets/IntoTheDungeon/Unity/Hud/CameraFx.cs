using System.Collections.Generic;
using UnityEngine;
using IntoTheDungeon.Features.Settings;

namespace IntoTheDungeon.Unity.Hud
{
    /// <summary>
    /// 카메라 연출(흔들림·줌 펀치)의 정적 상태. CameraController.LateUpdate가 매 프레임 Tick(실제 시간)한 뒤
    /// Offset을 위치에, ZoomDelta를 orthographicSize에 더한다.
    /// 흔들림은 여러 개가 겹치면 max 합성, 상한 MaxAmplitude, 감쇠 e^(−Decay·t), 세기 × GameSettings.ShakeScale
    /// (0이면 흔들림 전부 끔, 줌 펀치는 유지). 씬이 바뀌면 CameraController.Init이 Reset한다.
    /// </summary>
    public static class CameraFx
    {
        /// <summary>이번 프레임 카메라 위치 보정(월드 단위).</summary>
        public static Vector2 Offset;
        /// <summary>이번 프레임 orthographicSize 보정(음수 = 당겨 봄).</summary>
        public static float ZoomDelta;

        public const float MaxAmplitude = 0.3f;   // 월드 단위 상한 (임시값)
        const float Decay = 9f;                   // 세기 × e^(−9t)
        const float Frequency = 26f;              // 방향이 있는 흔들림의 초당 왕복 횟수 (임시값)
        const int MaxShakes = 6;

        struct ShakeItem { public float Amp, Duration, Age, Phase; public Vector2 Dir; }
        struct Zoom { public float Delta, In, Out, Age; public bool On; }

        static readonly List<ShakeItem> s_shakes = new(MaxShakes);
        static Zoom s_zoom;
        static readonly System.Random s_rng = new(20251106);   // 연출 전용(판 난수 RunData.Rng와 무관)
        static Vector2 s_jitter = Vector2.right;

        /// <summary>amplitude(월드 단위)로 duration초 흔든다. dir이 있으면 그 축으로 왕복, 없으면 무작위 지터.</summary>
        public static void Shake(float amplitude, float duration, Vector2 dir = default)
        {
            if (amplitude <= 0f || duration <= 0f) return;
            float scale = GameSettings.ShakeScale;   // 설정 화면의 흔들림 세기(0 = 카메라 흔들림 끔)
            if (scale <= 0f) return;
            var s = new ShakeItem
            {
                Amp = Mathf.Min(amplitude * scale, MaxAmplitude),
                Duration = duration,
                Dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.zero,
                Phase = (float)s_rng.NextDouble() * Mathf.PI * 2f,
            };
            if (s_shakes.Count >= MaxShakes)
            {
                // 자리가 없으면 가장 약한 것을 밀어낸다(그보다 약하면 무시)
                int weakest = 0; float min = float.MaxValue;
                for (int i = 0; i < s_shakes.Count; i++)
                {
                    float a = Current(s_shakes[i]);
                    if (a < min) { min = a; weakest = i; }
                }
                if (min >= s.Amp) return;
                s_shakes.RemoveAt(weakest);
            }
            s_shakes.Add(s);
        }

        /// <summary>orthographicSize를 inTime초에 delta만큼 바꿨다가 outTime초에 걸쳐 되돌린다(음수 = 당겨 봄). 새 호출이 이전 것을 대체.</summary>
        public static void ZoomPunch(float delta, float inTime, float outTime)
        {
            if (delta == 0f) return;
            s_zoom = new Zoom { Delta = delta, In = Mathf.Max(0.01f, inTime), Out = Mathf.Max(0.01f, outTime), Age = 0f, On = true };
        }

        static float Current(in ShakeItem s) => s.Age >= s.Duration ? 0f : s.Amp * Mathf.Exp(-Decay * s.Age);

        /// <summary>매 프레임 한 번(CameraController.LateUpdate), 실제 시간으로.</summary>
        public static void Tick(float unscaledDt)
        {
            float dt = Mathf.Max(0f, unscaledDt);

            // 흔들림: 가장 센 것의 세기(max 합성). 방향은 그것의 축으로 왕복 + 무작위 지터
            float best = 0f; bool hasBest = false; ShakeItem bestItem = default;   // 제거로 인덱스가 밀리므로 값으로 들고 있는다
            for (int i = s_shakes.Count - 1; i >= 0; i--)
            {
                var s = s_shakes[i];
                s.Age += dt;
                float a = Current(s);
                if (a < 0.002f) { s_shakes.RemoveAt(i); continue; }
                s_shakes[i] = s;
                if (a > best) { best = a; bestItem = s; hasBest = true; }
            }
            if (!hasBest) Offset = Vector2.zero;
            else
            {
                var s = bestItem;
                float ang = Mathf.Atan2(s_jitter.y, s_jitter.x) + Mathf.PI * 0.5f + (float)s_rng.NextDouble() * Mathf.PI;   // 직전과 90도 이상 다르게
                s_jitter = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                Vector2 v = s.Dir == Vector2.zero
                    ? s_jitter
                    : s.Dir * (Mathf.Cos(s.Phase + s.Age * Frequency * Mathf.PI * 2f) * 0.75f) + s_jitter * 0.35f;
                Offset = v * best;
            }

            // 줌 펀치: In 동안 Delta까지 빠르게, Out 동안 0으로 부드럽게
            if (!s_zoom.On) { ZoomDelta = 0f; return; }
            s_zoom.Age += dt;
            float k;
            if (s_zoom.Age < s_zoom.In)
            {
                float x = s_zoom.Age / s_zoom.In;
                k = 1f - (1f - x) * (1f - x);
            }
            else
            {
                float x = (s_zoom.Age - s_zoom.In) / s_zoom.Out;
                if (x >= 1f) { s_zoom.On = false; ZoomDelta = 0f; return; }
                k = 1f - Mathf.SmoothStep(0f, 1f, x);
            }
            ZoomDelta = s_zoom.Delta * k;
        }

        /// <summary>씬 재로드 때(CameraController.Init) 지난 연출을 전부 지운다.</summary>
        public static void Reset()
        {
            s_shakes.Clear();
            s_zoom = default;
            Offset = Vector2.zero;
            ZoomDelta = 0f;
        }
    }
}
